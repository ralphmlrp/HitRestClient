using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using HIT.REST.Client.config;



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
/*
    internal enum State {
      /// <summary>Vor dem ersten Aufruf, d.h. es existiert kein Http-Client und auch keine aktuelle Session</summary>
      Start,
      /// <summary>Http-Client existiert, aber man ist nicht mehr angemeldet, da Session beendet</summary>
      NotLoggedIn,

      LoggedIn
    }
*/


//--------------------------------------------------------------------

    private HitSettingsSection  config;

//    private State               CurrentState { get; set; }


    /// <summary>HTTP User Agent</summary>
    private HttpClient          objThisUA;

    /// <summary>
    /// Index des in der <see cref="HitSettingsSection"/> gerade/zuletzt verwendeten
    /// <see cref="BaseUrlElement"/>s. 
    /// Ist er kleiner 0, dann wurde noch gar keiner verwendet.
    /// Ist er größer oder gleich der Anzahl möglicher BaseUrls,
    /// dann konnte sich keiner verbinden.
    /// </summary>
    private int                 intThisBaseUrlIndex;

    /// <summary>Das aktuelle Secret der REST-Sitzung.</summary>
    private String              strThisCurrentSecret;

    private Credentials         objThisCredentials;



//--------------------------------------------------------------------

    public RestClient(HitSettingsSection pobjConfig) {
      if (pobjConfig == null) throw new ArgumentNullException();
      config = pobjConfig;

      objThisUA           = null; // wird erst angelegt, wenn benötigt
      intThisBaseUrlIndex = -1;   // -1 = "versuche den nächsten"

      Credentials         = null;
    }



//--------------------------------------------------------------------

    /// <summary>
    /// Liefere aktuellen HttpClient oder den nächsten
    /// aus der Liste der BaseUrls.
    /// </summary>
    /// <param name="pboolEndSession">Wird <tt>true</tt> angegeben, wird eine URI so gebildet, dass eine ggf. bestehende Sitzung durch explizites Setzen eines negativen Timeouts beendet wird.</param>
    public HttpClient CurrentHttpClient(bool pboolEndSession = false) {
Program.log("RestClient.CurrentHttpClient("+(pboolEndSession?"true":"false")+") ...");
      if (objThisUA == null)  {
        // da ist noch keiner, also anlegen
        intThisBaseUrlIndex++;

        // ist der Index "verbraucht", dann gibt's keinen weiteren Versuch mehr
        if (intThisBaseUrlIndex >= config.BaseUrls.Count) return null;

        // der nächste:
        BaseUrlElement  objBaseUrl = config.BaseUrls[intThisBaseUrlIndex];
Program.log("\tTry BaseUrl \""+objBaseUrl.BaseUrl+"\" for new connection ...");

        // Anlegen inkl. vorbereiteter Header für Authentication
        objThisUA = new HttpClient();
        // generelle Parameter setzen
        objThisUA.BaseAddress = new Uri(objBaseUrl.BaseUrl);
        objThisUA.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      }
      return objThisUA;
    }


    public void prepareHttpClient(bool pboolEndSession = false) {
      // gibt's noch keinen Useragenten, dann tu nichts
      if (objThisUA == null)  return;

Program.log("RestClient.prepareHttpClient("+(pboolEndSession?"true":"false")+") with secret="+(Secret==null?"<null>":Secret)+" ...");

      // Mit Credentials speziell für den zu verwendenden
      // Autorisierungsmodus weitere Angaben setzen
      if (Credentials == null) {
Program.log("No Credentials for HttpClient!");
      }
      else  {
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
            objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_TIMEOUT,(pboolEndSession ? -1 : Credentials.Timeout).ToString());
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
            objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_TIMEOUT,(pboolEndSession ? -1 : Credentials.Timeout).ToString());
            break;

          case AuthMode.QueryString:
            // die müssen in der URL angehängt werden, nicht hier
            break;

          case AuthMode.NoAuth:
          default:
            // keine extra Header
            break;
        }
      }
Program.log("HttpClient:\n-----");
Program.log("VERB "+objThisUA.BaseAddress);
Program.log(objThisUA.DefaultRequestHeaders.ToString());
Program.log("-----");
    }


    /// <summary>
    /// Lege den aktuellen HitClient tot, so dass der nächste
    /// in der Konfiguration vermerkte REST-Service probiert wird
    /// </summary>
    public void forceNextHost() {
      objThisUA = null;
      Secret    = null;
    }


    /// <summary>
    /// Erzeuge URI als Vorlage anhand des aktuellen HttpClients.
    /// Scheme, Host und BasePath werden gesetzt; RestPath, Query und ggf. die Credentials müssen noch nachgezogen werden.
    /// </summary>
    /// <param name="pboolEndSession">Wird <tt>true</tt> angegeben, wird eine URI so gebildet, dass eine ggf. bestehende Sitzung durch explizites Setzen eines negativen Timeouts beendet wird.</param>
    /// <returns><see cref="URI"/></returns>
    public URI CreateURI(bool pboolEndSession = false)  {
      // erst Client besorgen, damit wir wissen, welche URL wir aufbauen sollen
      HttpClient objClient = CurrentHttpClient(pboolEndSession);
      // gibt's keinen, dann war's das
      if (objClient == null)  return null;

      // übernehme Login-Credentials je nach AuthMode
      prepareHttpClient(pboolEndSession);

      // mit der aktuellen BaseUrl frisch anlegen
      BaseUrlElement  objBaseUrl = config.BaseUrls[intThisBaseUrlIndex];

      URI objUri = new URI();
      objUri.Scheme   = objBaseUrl.UseHttps ? "https" : "http";
      objUri.Host     = objBaseUrl.Domain;
      objUri.Port     = objBaseUrl.Port;
      objUri.BasePath = (objBaseUrl.RootPath == null ? "" : "/"+objBaseUrl.RootPath)+config.BasePath.path;   // das ist der feste Pfadbestandteil

Program.log("RestClient.CreateURI() -> "+objUri);
      return objUri;
    }


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

    public void ensureCredentials() {
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
Program.log("RestClient.Secret set to "+(strThisCurrentSecret==null?"<null>":strThisCurrentSecret)+" at\n"+new System.Diagnostics.StackTrace());
      }
    }



    /// <summary>
    /// Sende Anfrage an HIT3-REST.
    /// </summary>
    /// <param name="pobjUri"><see cref="URI"/>, die gesendet werden soll</param>
    /// <param name="pobjResponse">Die <see cref="HttpRequestMessage"/></param>
    /// <returns>die vom HIT3-REST-Service erhaltene Antwort in Rohform oder <tt>null</tt>, wenn kein Content gelesen werden konnte</returns>
    private Dictionary<String,Object> send(Verb penumVerb,URI pobjUri,HttpContent pobjRequestContent,out HttpResponseMessage pobjResponse)  {
      if (pobjUri == null)  throw new ArgumentNullException(nameof(pobjUri),"Eine URI ist unabdingbar!");

      // bei GET gibt es keinen HttpContent, bei den anderen muss einer dabei sein
      switch (penumVerb) {
        case Verb.Get:
        case Verb.Delete:
          if (pobjRequestContent != null) throw new ArgumentNullException(nameof(pobjRequestContent),"Eine "+penumVerb+"-Anfrage hat keinen Inhalt, sondern nur Query-Strings!");
          break;

        case Verb.Put:
        case Verb.Post:
          if (pobjRequestContent == null) throw new ArgumentNullException(nameof(pobjRequestContent),"Ohne Inhalt keine "+penumVerb+"-Anfrage!");
          break;
      }
      
      // standardmäßig erst mal keine Antwort
      pobjResponse = null;

      Dictionary<String,Object> objContent = null;

      HttpClient objUA = CurrentHttpClient();
      if (objUA == null)  {
        Program.log("#> No UA available!");
        // gar kein HIT3-REST-Service war erreichbar
        return null;
      }

      // übernehme Login-Credentials je nach AuthMode
      prepareHttpClient();

      try {
        Program.log("#> send "+penumVerb+" "+pobjUri.ToString());
        switch (penumVerb)  {
          case Verb.Get:
            pobjResponse = objUA.GetAsync(pobjUri.ToString()).Result;
            break;

          case Verb.Put:
            pobjResponse = objUA.PutAsync(pobjUri.ToString(),pobjRequestContent).Result;
            break;

          case Verb.Post:
            pobjResponse = objUA.PostAsync(pobjUri.ToString(),pobjRequestContent).Result;
            break;

          case Verb.Delete:
            pobjResponse = objUA.DeleteAsync(pobjUri.ToString()).Result;
            break;

          default:
            pobjResponse = null;
            break;
        }

        Program.log("#> rcvd HTTP Status "+((int)pobjResponse.StatusCode)+" "+pobjResponse.ReasonPhrase);
        Program.log("#> grab response");
        objContent = pobjResponse.Content.ReadAsAsync<Dictionary<String,Object>>().Result;
      }
      catch (Exception e)  {
        Program.log("Keine Verbindung zu "+pobjUri+" möglich!");
        return null;
      }

      // wenn wir noch kein Secret haben, dann versuche, den zu extrahieren
      if (Secret == null && objContent.ContainsKey("cache_secret")) {
        String  strCacheSecret  = (String)objContent["cache_secret"];
        if (!String.IsNullOrEmpty(strCacheSecret))  Secret = strCacheSecret;
      }

if (objContent != null) foreach (KeyValuePair<string,object> pair in objContent)  {
  Program.log(pair.Key+"\t=> "+pair.Value+" ["+pair.Value?.GetType()?.FullName+"]");
}
      
      return objContent;
    }



    public Dictionary<String,Object> sendGET(URI pobjUri,out HttpResponseMessage pobjResponse)  {
      return send(Verb.Get,pobjUri,null,out pobjResponse);
    }

    public Dictionary<String,Object> sendPUT(URI pobjUri,HttpContent pobjRequestContent,out HttpResponseMessage pobjResponse)  {
      return send(Verb.Put,pobjUri,pobjRequestContent,out pobjResponse);
    }

    public Dictionary<String,Object> sendPOST(URI pobjUri,HttpContent pobjRequestContent,out HttpResponseMessage pobjResponse)  {
      return send(Verb.Post,pobjUri,pobjRequestContent,out pobjResponse);
    }

    public Dictionary<String,Object> sendDELETE(URI pobjUri,out HttpResponseMessage pobjResponse)  {
      return send(Verb.Delete,pobjUri,null,out pobjResponse);
    }
    

    //--------------------------------------------------------------------
  }
}
