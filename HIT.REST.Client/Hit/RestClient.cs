using System;
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

    private bool                boolThisNewLoginRequired;
    private Credentials         objThisCredentials;



//--------------------------------------------------------------------

    public RestClient(HitSettingsSection pobjConfig) {
      if (pobjConfig == null) throw new ArgumentNullException();
      config = pobjConfig;

      objThisUA           = null; // wird erst angelegt, wenn benötigt
      intThisBaseUrlIndex = -1;   // -1 = "versuche den nächsten"

      boolThisNewLoginRequired  = true;
      objThisCredentials        = null;
    }



//--------------------------------------------------------------------

    /// <summary>
    /// Liefere aktuellen HttpClient oder den nächsten
    /// aus der Liste der BaseUrls.
    /// </summary>
    public HttpClient CurrentHttpClient { get {
      if (objThisUA == null)  {
        // da ist noch keiner, also anlegen

        // ist der Index "verbraucht", dann gibt's keinen weiteren Versuch mehr
        if (intThisBaseUrlIndex >= config.BaseUrls.Count) return null;

        // der nächste:
        intThisBaseUrlIndex++;
        BaseUrlElement  objBaseUrl = config.BaseUrls[intThisBaseUrlIndex];

        // Anlegen inkl. vorbereiteter Header für Authentication
        objThisUA = new HttpClient();
        // generelle Parameter setzen
        objThisUA.BaseAddress = new Uri(objBaseUrl.SchemeAndDomainUrl);
        objThisUA.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Mit Credentials speziell für den zu verwendenden
        // Autorisierungsmodus weitere Angaben setzen
        if (Credentials != null) switch (Credentials.AuthenticationMode) {
          case AuthMode.AuthenticationHeader:
            // mit dem Authentication-Header setzen wir "BNR:MBN:PIN"
            // fehlt nur noch der Timeout, der kommt per eigenem Header
            if (strThisCurrentSecret == null) {
              objThisUA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic",$"{Credentials.Betriebsnummer}:{Credentials.Mitbenutzer}:{Credentials.PIN}");
              objThisUA.DefaultRequestHeaders.Remove(HTTP_HEADER_AUTH_SECRET);
            }
            else  {
              // da Secret bekannt, wird nur BNR und MBN geliefert, die PIN bleibt leer (also "bnr:mbn:" statt "bnr:mbn:pin")
              objThisUA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic",$"{Credentials.Betriebsnummer}:{Credentials.Mitbenutzer}:");
              objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_SECRET,strThisCurrentSecret);
            }
            objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_TIMEOUT,Credentials.Timeout.ToString());
            break;

          case AuthMode.SelfmadeHeader:
            // mit unseren eigenen Headern die Credentials setzen
            objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_BNR,Credentials.Betriebsnummer);
            objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_MBN,Credentials.Mitbenutzer);
            if (strThisCurrentSecret == null) {
              objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_PIN,Credentials.PIN);
              objThisUA.DefaultRequestHeaders.Remove(HTTP_HEADER_AUTH_SECRET);
            }
            else  {
              objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_SECRET,strThisCurrentSecret);
            }
            objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_TIMEOUT,Credentials.Timeout.ToString());
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
      return objThisUA;
    }}


    /// <summary>
    /// Erzeuge URI als Vorlage anhand des aktuellen HttpClients.
    /// Scheme, Host und BasePath werden gesetzt; RestPath und Query muss noch nachgezogen werden.
    /// </summary>
    /// <returns><see cref="URI"/></returns>
    public URI CreateURI()  {
      // erst Client besorgen, damit wir wissen, welche URL wir aufbauen sollen
      HttpClient objClient = CurrentHttpClient;
      // gibt's keinen, dann war's das
      if (objClient == null)  return null;

      // mit der aktuellen BaseUrl frisch anlegen
      BaseUrlElement  objBaseUrl = config.BaseUrls[intThisBaseUrlIndex];

      URI objUri = new URI();
      objUri.Scheme   = objBaseUrl.UseHttps ? "https" : "http";
      objUri.Host     = objBaseUrl.Domain;
      objUri.BasePath = config.BasePath.path;   // das ist der feste Pfadbestandteil

      return objUri;
    }


    public Credentials Credentials {
      get {
        return objThisCredentials;
      }
      set {
        objThisCredentials        = value;

        // wurden neue Credentials gesetzt, dann merken wir uns das,
        // damit wir z.B. eine neue Session starten können
        boolThisNewLoginRequired  = true;
      }
    }

    public void ensureCredentials() {
      if (Credentials == null)  throw new InvalidOperationException("Ohne Anmeldedaten kein HIT3-REST!");
    }


    /// <summary>
    /// Liefere aktuellen Secret. Kann auch <tt>null</tt> sein, wenn keiner geliefert oder gewünscht.
    /// </summary>
    public String Secret { get {
      return strThisCurrentSecret;
    }}



    /// <summary>
    /// Sende Anfrage an HIT3-REST, ggf. mit nötigem Login.
    /// </summary>
    /// <param name="pobjTask"><see cref="Task"/>, der gesendet werden soll</param>
    /// <returns>die vom HIT3-REST-Service erhaltene Antwort in Rohform</returns>
    public HttpResponseMessage send(URI pobjUri)  {
      HttpResponseMessage objResponse = null;




      throw new NotImplementedException();
    }




    private HttpResponseMessage sendToREST() {
      //try {
      //  objResponse = client.GetAsync(config.BasePath.path).Result;
      //  message.EnsureSuccessStatusCode();
      //  if (message.Content.ReadAsAsync<bool>().Result) {
      //    Console.WriteLine("-> verbunden!");
      //    return client;
      //  }
      //}
      //catch (Exception e) {
      //    Console.WriteLine("-> Fehler: "+e.Message);
      //}

      throw new NotImplementedException();
    }



//--------------------------------------------------------------------
  }
}
