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

      objThisCredentials        = null;
    }



//--------------------------------------------------------------------

    /// <summary>
    /// Liefere aktuellen HttpClient oder den nächsten
    /// aus der Liste der BaseUrls.
    /// </summary>
    /// <param name="pboolEndSession">Wird <tt>true</tt> angegeben, wird eine URI so gebildet, dass eine ggf. bestehende Sitzung durch explizites Setzen eines negativen Timeouts beendet wird.</param>
    public HttpClient CurrentHttpClient(bool pboolEndSession = false) {
      if (objThisUA == null)  {
        // da ist noch keiner, also anlegen
        intThisBaseUrlIndex++;

        // ist der Index "verbraucht", dann gibt's keinen weiteren Versuch mehr
        if (intThisBaseUrlIndex >= config.BaseUrls.Count) return null;

        // der nächste:
        BaseUrlElement  objBaseUrl = config.BaseUrls[intThisBaseUrlIndex];
Program.log("Try BaseUrl "+objBaseUrl.BaseUrl);

        // Anlegen inkl. vorbereiteter Header für Authentication
        objThisUA = new HttpClient();
        // generelle Parameter setzen
        objThisUA.BaseAddress = new Uri(objBaseUrl.BaseUrl);
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
            // der Timeout muss immer geschickt werden, weil der steuert, ob Sitzung bestehen bleibt oder nicht
            objThisUA.DefaultRequestHeaders.Add(HTTP_HEADER_AUTH_TIMEOUT,(pboolEndSession ? -1 : Credentials.Timeout).ToString());
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
Program.log(objThisUA.DefaultRequestHeaders.ToString());
Program.log(objThisUA.BaseAddress);
      }
      return objThisUA;
    }


    /// <summary>
    /// Lege den aktuellen HitClient tot, so dass der nächste
    /// in der Konfiguration vermerkte REST-Service probiert wird
    /// </summary>
    public void forceNextHost() {
      objThisUA             = null;
      strThisCurrentSecret  = null;
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
        objThisCredentials        = value;

        // wurden neue Credentials gesetzt, dann gilt ein vorhandenes Secret nicht mehr
        strThisCurrentSecret  = null;
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
    /// Sende Anfrage an HIT3-REST.
    /// </summary>
    /// <param name="pobjUri"><see cref="URI"/>, die gesendet werden soll</param>
    /// <param name="pobjResponse">Die <see cref="HttpRequestMessage"/></param>
    /// <returns>die vom HIT3-REST-Service erhaltene Antwort in Rohform oder <tt>null</tt>, wenn kein Content gelesen werden konnte</returns>
    public Dictionary<String,Object> send(URI pobjUri,out HttpResponseMessage pobjResponse)  {
      if (pobjUri == null)  throw new ArgumentNullException(nameof(pobjUri),"Eine URI ist unabdingbar!");

      // standardmäßig erst mal keine Antwort
      pobjResponse = null;

      Dictionary<String,Object> objContent = null;

      HttpClient objUA = CurrentHttpClient();
      if (objUA == null)  {
        Program.log("#> No UA available!");
        // gar kein HIT3-REST-Service war erreichbar
        return null;
      }

      try {
        Program.log("#> send "+pobjUri.ToString());
        pobjResponse = objUA.GetAsync(pobjUri.ToString()).Result;
        Program.log("#> rcvd HTTP Status "+((int)pobjResponse.StatusCode)+" "+pobjResponse.ReasonPhrase);
        Program.log("#> grab response");
        objContent = pobjResponse.Content.ReadAsAsync<Dictionary<String,Object>>().Result;
      }
      catch (Exception e)  {
        Program.log("Keine Verbindung zu "+pobjUri+" möglich!");
      }

      // wenn wir noch kein Secret haben, dann versuche, den zu extrahieren
      if (strThisCurrentSecret == null && objContent.ContainsKey("cache_secret")) {
        String  strCacheSecret  = (String)objContent["cache_secret"];
        if (!String.IsNullOrEmpty(strCacheSecret))  strThisCurrentSecret = strCacheSecret;
      }
      
      return objContent;
    }



//--------------------------------------------------------------------
  }
}
