using HIT.REST.Client.config;

using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;



namespace HIT.REST.Client.Hit {

  /// <summary>
  /// Client, der mit dem HIT3-REST-Service kommuniziert.
  /// Beinhaltet eine Ablaufverfolgung bzgl. Session-Handling
  /// </summary>
  public class RestClient {
//--------------------------------------------------------------------

    /// <summary>Eigener HTTP Header für die Betriebsnummer</summary>
    public const String HTTP_HEADER_AUTH_BNR      = "hit-bnr";        // HTTP-Header mit "x-" sind nicht mehr zulässig (https://tools.ietf.org/html/rfc6648)
    /// <summary>Eigener HTTP Header für Mitbenutzerkennung</summary>
    public const String HTTP_HEADER_AUTH_MBN      = "hit-mbn";
    /// <summary>Eigener HTTP Header für Passwort/PIN</summary>
    public const String HTTP_HEADER_AUTH_PIN      = "hit-pin";
    /// <summary>Eigener HTTP Header für die ID der aktuellen Sitzung</summary>
    public const String HTTP_HEADER_AUTH_SECRET   = "hit-secret";
    /// <summary>Eigener HTTP Header für den Timeout der aktuellen Sitzung</summary>
    public const String HTTP_HEADER_AUTH_TIMEOUT  = "hit-timeout";



    public enum Verb  {
      /// <summary>Keine/unbekannte Aktion</summary>
      None = 0,

      /// <summary>Abfrage</summary>
      Get = 1,

      /// <summary>Einfügen</summary>
      Put,

      /// <summary>Änderung</summary>
      Post,

      /// <summary>Stornieren</summary>
      Delete

    }



//--------------------------------------------------------------------

    /// <summary>HTTP User Agent mit der Basisadresse</summary>
    private readonly HttpClient          objThisUA;

    /// <summary>Basispfad unterhalb der Basisadresse; wird für Anfrage-URI benötigt</summary>
    private readonly String              strThisBasePath;


    /// <summary>Das aktuelle Secret der REST-Sitzung.</summary>
    private String              strThisCurrentSecret;

    private Credentials         objThisCredentials;



//--------------------------------------------------------------------

    /// <summary>
    /// Legt neuen HTTP-Client an, der die REST-Kommunikation abwickelt.
    /// Erwartet eine BaseUrl (URI der Webanwendung als solches) und den BasePath
    /// (der "fixe" Pfad in der Webanwendung zum REST-Interface); beides aus der <see cref="HitSettingsSection"/> der <tt>app.config</tt>
    /// </summary>
    /// <param name="pobjBaseUrl">eine der <see cref="BaseUrlElement"/>e der <see cref="HitSettingsSection"/></param>
    /// <param name="pobjBasePath">das <see cref="BasePathElement"/>e der <see cref="HitSettingsSection"/> mit dem fixen Pfad innerhalb der Webanwendung</param>
    public RestClient(BaseUrlElement pobjBaseUrl,BasePathElement pobjBasePath) {
      if (pobjBaseUrl == null) throw new ArgumentNullException();
//Program.log("NEW RestClient() ...");

      strThisBasePath       = (pobjBasePath == null) ? "" : pobjBasePath.path;
      strThisCurrentSecret  = null;
      Credentials           = null;

      // Anlegen inkl. vorbereiteter Basisadresse
      objThisUA = new HttpClient {
        // generelle Parameter setzen
        BaseAddress = new Uri(pobjBaseUrl.BaseUrl+strThisBasePath.Substring(strThisBasePath.StartsWith("/") ? 1 : 0)),
        Timeout     = new TimeSpan(0,5,0)
      };
      objThisUA.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

Program.log("Set up RestClient with "+objThisUA.BaseAddress+" ...");
    }



//--------------------------------------------------------------------

    public Credentials Credentials {
      get {
        return objThisCredentials;
      }
      set {
        objThisCredentials  = value;

        // wurden neue Credentials gesetzt, dann gilt ein vorhandenes Secret nicht mehr
        Secret              = null;
      }
    }

    public void EnsureCredentials() {
      if (Credentials == null)  throw new InvalidOperationException("Ohne Anmeldedaten kein HIT3-REST!");
    }


    /// <summary>
    /// Liefere aktuellen Secret. Kann auch <tt>null</tt> sein, wenn keiner geliefert oder gewünscht.
    /// </summary>
    public String Secret {
      get {
        return strThisCurrentSecret;
      }
      private set {
        strThisCurrentSecret = value; 
//Program.log("RestClient.Secret set to "+(strThisCurrentSecret==null?"<null>":strThisCurrentSecret)+" at\n"+new System.Diagnostics.StackTrace());
      }
    }


//--------------------------------------------------------------------

    /// <summary>
    /// Bereite den HTTP-Client für die Anfrage vor und liefere den elementaren Teil der Anfrage-URI.
    /// </summary>
    /// <param name="pobjTask"><see cref="Task"/> zum Vorbereiten oder bei <tt>null</tt> wird der HTTP-Client für ein Beenden der Session vorbereitet</param>
    /// <returns><see cref="URI"/></returns>
    public URI PrepareFor(Task pobjTask) {
      bool endSession = (pobjTask == null);
//Program.log("RestClient.prepareFor(Task "+(endSession?"<logout>":"'"+pobjTask.Display+"'")+") with secret="+(Secret==null?"<null>":Secret)+" ...");

      // Mit Credentials speziell für den zu verwendenden
      // Autorisierungsmodus weitere Angaben setzen
      if (Credentials != null) {
        // die Header erst wieder entfernen, bevor wir sie neu setzen, da es kein Überschreiben gibt!
        objThisUA.DefaultRequestHeaders.Authorization = null;
        objThisUA.DefaultRequestHeaders.Remove(HTTP_HEADER_AUTH_BNR);
        objThisUA.DefaultRequestHeaders.Remove(HTTP_HEADER_AUTH_MBN);
        objThisUA.DefaultRequestHeaders.Remove(HTTP_HEADER_AUTH_PIN);
        objThisUA.DefaultRequestHeaders.Remove(HTTP_HEADER_AUTH_SECRET);
        objThisUA.DefaultRequestHeaders.Remove(HTTP_HEADER_AUTH_TIMEOUT);

        switch (Credentials.AuthenticationMode) {
          case AuthMode.AuthenticationHeader:
            // mit dem Authentication-Header setzen wir "BNR:MBN:PIN"
            // fehlt nur noch der Timeout, der kommt per eigenem Header
            // (QueryString wäre auch möglich, wir nutzen hier aber unseren Header)
            if (Secret == null) {
              objThisUA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic",$"{Credentials.Betriebsnummer}:{Credentials.Mitbenutzer}:{Credentials.PIN}");
              objThisUA.DefaultRequestHeaders.Remove(HTTP_HEADER_AUTH_SECRET);
            }
            else  {
              // da Secret bekannt, wird nur BNR und MBN geliefert, die PIN bleibt leer (also "bnr:mbn:" statt "bnr:mbn:pin")
              objThisUA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic",$"{Credentials.Betriebsnummer}:{Credentials.Mitbenutzer}:");
              objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_SECRET,Secret);
            }
            // der Timeout muss immer geschickt werden, weil der steuert, ob Sitzung bestehen bleibt oder nicht
            if (!endSession)  objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_TIMEOUT,Credentials.TimeoutInSeconds.ToString());
            break;

          case AuthMode.SelfmadeHeader:
            // mit unseren eigenen Headern die Credentials setzen
            objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_BNR,Credentials.Betriebsnummer);
            objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_MBN,Credentials.Mitbenutzer);
            if (Secret == null) {
              objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_PIN,Credentials.PIN);
              objThisUA.DefaultRequestHeaders.Remove(HTTP_HEADER_AUTH_SECRET);
            }
            else  {
              objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_SECRET,Secret);
            }
            // der Timeout muss immer geschickt werden, weil der steuert, ob Sitzung bestehen bleibt oder nicht
            if (!endSession)  objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_TIMEOUT,Credentials.TimeoutInSeconds.ToString());
            break;

          case AuthMode.QueryString:
            // die müssen an der URL angehängt werden, nicht hier
            break;

          case AuthMode.NoAuth:
          default:
            // keine extra Header
            break;
        }
      }

      // hier ist der Client vorbereitet, jetzt die Anfrage-URI bauen
      // die ist relativ zur BaseAddress des HttpClient

      URI objURI = new URI();

      if (endSession)  {
        // das Ende der Session benötigt nichts weiter
      }
      else  {
        // der Task gibt uns vor, was zu tun ist
        objURI.RestPath = pobjTask.Entity;
        // die Datenzeilen oder Abfragen kommen nachträglich

        if (Credentials != null)  switch (Credentials.AuthenticationMode) {
          case AuthMode.AuthenticationHeader:
            // der HttpClient hat bereits BNR, MBN und PIN via Authorization-Header
            // gesetzt, ebenso den Timeout und ggf. Secret per eigenem Header
            // -> da ist nichts weiter zu tun
            break;

          case AuthMode.SelfmadeHeader:
            // der HttpClient hat bereits BNR, MBN und PIN via eigener Header gesetzt,
            // ebenso den Timeout und ggf. Secret
            // -> da ist nichts weiter zu tun
            break;

          case AuthMode.QueryString:
            // Query-Namen exakt genau so wie in HIT.Meldeprogramm.Web.Controllers.RESTv2.HitController !
            // zusätzlich zur Anfrage dazuhängen
            objURI.Query["bnr"] = Credentials.Betriebsnummer;
            if (!String.IsNullOrWhiteSpace(Credentials.Mitbenutzer))  {
              objURI.Query["mbn"] = Credentials.Mitbenutzer;
            }
            if (Secret == null) {
              // ohne Secret brauchen wir die PIN
              objURI.Query["pin"] = Credentials.PIN;
            }
            else  {
              objURI.Query["cacheSecret"] = Secret;     
            }
            // der Timeout muss immer geschickt werden, weil der steuert, ob Sitzung bestehen bleibt oder nicht
            objURI.Query["cacheTimeout"]  = Credentials.TimeoutInSeconds.ToString();
            break;

          case AuthMode.NoAuth:
          default:
            // nichts weiter
            break;
        }

      }


//Program.log("HttpClient:\n-----");
//Program.log("VERB "+objThisUA.BaseAddress);
//Program.log(objThisUA.DefaultRequestHeaders.ToString());
//Program.log("-----");
//Program.log("URI: "+objURI);
//Program.log("-----");

      return objURI;
    }





    public T SendGET<T>(URI pobjUri,out HttpResponseMessage pobjResponse) {
      return SendOne<T>(Verb.Get,pobjUri,null,out pobjResponse);
    }

    public T SendPUT<T>(URI pobjUri,HttpContent pobjRequestContent,out HttpResponseMessage pobjResponse)  {
      return SendOne<T>(Verb.Put,pobjUri,pobjRequestContent,out pobjResponse);
    }

    public T SendPOST<T>(URI pobjUri,HttpContent pobjRequestContent,out HttpResponseMessage pobjResponse) {
      return SendOne<T>(Verb.Post,pobjUri,pobjRequestContent,out pobjResponse);
    }

    public T SendDELETE<T>(URI pobjUri,HttpContent pobjRequestContent,out HttpResponseMessage pobjResponse) {
      return SendOne<T>(Verb.Delete,pobjUri,pobjRequestContent,out pobjResponse);
    }

    

    /// <summary>
    /// Sende Anfrage an HIT3-REST.
    /// </summary>
    /// <typeparam name="T">Gibt an, als welchen Typ die Daten geliefert werden sollen; meist ist es <code>Dictionary&lt;String,Object&gt;</code></typeparam>
    /// <param name="pobjUri"><see cref="URI"/>, die gesendet werden soll</param>
    /// <param name="pobjResponse">Die <see cref="HttpRequestMessage"/></param>
    /// <returns>die vom HIT3-REST-Service erhaltene Antwort in Rohform oder <tt>null</tt>, wenn kein Content gelesen werden konnte</returns>
    private T SendOne<T>(Verb penumVerb,URI pobjUri,HttpContent pobjRequestContent,out HttpResponseMessage pobjResponse) {
      if (pobjUri == null)  throw new ArgumentNullException(nameof(pobjUri),"Eine URI ist unabdingbar!");

      // bei GET gibt es keinen HttpContent, bei den anderen muss einer dabei sein
      switch (penumVerb) {
        case Verb.Get:
          if (pobjRequestContent != null) throw new ArgumentException(nameof(pobjRequestContent),"Eine "+penumVerb+"-Anfrage hat keinen Inhalt, sondern nur Query-Strings!");
          break;

        case Verb.Put:
        case Verb.Post:
          if (pobjRequestContent == null) throw new ArgumentException(nameof(pobjRequestContent),"Ohne Inhalt keine "+penumVerb+"-Anfrage!");
          break;

        case Verb.Delete:
          if (pobjRequestContent == null) pobjRequestContent = new StringContent("{}"); // leeres JSON-Objekt anlegen, wenn keines mitgegeben
          break;
      }

Program.log("#> send("+penumVerb+"): URI "+pobjUri.ToString()+"\n\tvia "+objThisUA.BaseAddress);
     
      // standardmäßig erst mal keine Antwort
      pobjResponse = null;

      T objContent = default(T);
      try {
        switch (penumVerb)  {
          case Verb.Get:
            pobjResponse = objThisUA.GetAsync(pobjUri.ToString()).Result;
            break;

          case Verb.Put:
            pobjResponse = objThisUA.PutAsync(pobjUri.ToString(),pobjRequestContent).Result;
            break;

          case Verb.Post:
            pobjResponse = objThisUA.PostAsync(pobjUri.ToString(),pobjRequestContent).Result;
            break;

          case Verb.Delete:
            // Note: DELETE does NOT support entity body, therefore there's no "content" parameter!
            //            pobjResponse = objThisUA.DeleteAsync(pobjUri.ToString()).Result;
            // since DELETE does not support a entity body, we set it up on our own:
            HttpRequestMessage objReq = new HttpRequestMessage(HttpMethod.Delete,pobjUri.ToString()) {
              Content = pobjRequestContent
            };
            pobjResponse = objThisUA.SendAsync(objReq).Result;
            break;

          default:
            pobjResponse = null;
            break;
        }
/*
Program.log("#> rcvd HTTP Status "+((int)pobjResponse.StatusCode)+" "+pobjResponse.ReasonPhrase);
*/
        if (pobjResponse.IsSuccessStatusCode) {
          Program.log("Erfolgreich: "+penumVerb+" "+objThisUA.BaseAddress+pobjUri);
/*
Program.log("#> grab response");
*/
          objContent = pobjResponse.Content.ReadAsAsync<T>().Result;    // war <Dictionary<String,Object>>
/*
if (objContent is Dictionary<String,Object>) foreach (KeyValuePair<string,object> pair in (objContent as Dictionary<String,Object>))  {
  Program.log(pair.Key+"\t=> "+pair.Value+" ["+pair.Value?.GetType()?.FullName+"]");
}
else if (objContent != null)  {
  Program.log(objContent.GetType()+": "+objContent.ToString());
}
*/
          // wenn wir noch kein Secret haben, dann versuche, den zu extrahieren
          // (aber auch nur, wenn wir das dürfen)
          Dictionary<String,Object>  objDictContent = objContent as Dictionary<String,Object>;
          if (Credentials != null && Credentials.UseSecret && Secret == null && objDictContent != null && objDictContent.ContainsKey("cache_secret")) {
            String  strCacheSecret  = (String)objDictContent["cache_secret"];
            if (!String.IsNullOrEmpty(strCacheSecret))  Secret = strCacheSecret;
          }
        }
        else  {
          Program.log("Verbindung zu "+objThisUA.BaseAddress+" fehlgeschlagen: HTTP Status "+((int)pobjResponse.StatusCode)+" "+pobjResponse.ReasonPhrase+"");
          // Fehlermeldung holen; die wird als Fehlerlist geliefert
         Dictionary<String,Object> objErrors = pobjResponse.Content.ReadAsAsync<Dictionary<String,Object>>().Result;
Program.tee("> HTTP Fehler: "+objErrors["Message"]);
          objContent = default(T);
        }
      }
      catch (Exception)  {
//Program.tee(e.Message+"\n\n"+e.StackTrace.ToString());
//        String strStatus = "[null response?!]";
//        if (pobjResponse != null) {
//          strStatus = ((int)pobjResponse.StatusCode)+" "+pobjResponse.ReasonPhrase;
//        }
        Program.log("Keine Verbindung zu "+objThisUA.BaseAddress+" möglich!? "/*+strStatus+"\n"+e*/);
        objContent = default(T);
      }

      return objContent;
    }



//--------------------------------------------------------------------
  }
}
