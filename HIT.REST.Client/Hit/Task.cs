using System;



namespace HIT.REST.Client.Hit {

  /// <summary>
  /// Abzusetzende Anfrage.
  /// </summary>
  public class Task {
//--------------------------------------------------------------------

    /// <summary>
    /// Eine Beschreibung des Tasks; wird nur zur Anzeige verwendet
    /// </summary>
    public String Description { get; set; }


    /// <summary>
    /// Aktion ist der HITP "command", nicht das HTTP-Verb für REST!
    /// </summary>
    public String Action    { get; set; }

    /// <summary>
    /// HITP-Subcodes für Aktion
    /// </summary>
    public String Subcodes  { get; set; }

    /// <summary>
    /// Für welche Entität soll gemeldet bzw. abgefragt werden?
    /// </summary>
    public String Entity    { get; set; }

    /// <summary>
    /// In welcher Datei stehen die zu meldenden Daten bzw. Abfragen?
    /// </summary>
    public String InputPath  { get; set; }

    /// <summary>
    /// In welche Datei soll das Ergebnis?
    /// </summary>
    public String OutputPath  { get; set; }



//--------------------------------------------------------------------

    /// <summary>
    /// Liefere Anzeigetext für den Task
    /// </summary>
    public String Display { get {
      if (String.IsNullOrWhiteSpace(Description)) {
        return "mit "+GetHitCommand()+":"+Entity;
      }
      return Description;
    }}



    /// <summary>
    /// Prüfe, ob Aktion gültig ist.
    /// </summary>
    /// <returns>ja/nein</returns>
    public bool isValidAction() {
      if (String.IsNullOrWhiteSpace(Action))  return false;
      if (Action.Length != 2) return false;

      Action = Action.ToUpper();

      char charCmd        = Action[0];    // R, I, X, S, U, D, C ...
      char charBlockSize  = Action[1];    // S, B, T

      bool valid;
      switch (charCmd)  {
        case 'R':     // Abfragen. Datei bei InputPath enthält dabei in der ersten Zeile die Ausgabespalte und in den weiteren die Abfragebedingungen
        case 'I':     // neuen Datensatz einfügen
        case 'X':     // entweder neuen Datensatz einfügen oder wenn (mit Schlüsselfeldern) bereits vorhanden, aktualisiere ihn
        case 'S':     // storniere Datensatz, mache ihn ungültig
        case 'C':     // bestätige Datensatz oder veranlasse eine erweiterte Prüfung
        case 'U':     // i.d.R. überschreibe Datensatz "in-place"
        case 'D':     // lösche Datensatz tatsächlich; ist nur im Testsystem möglich
          valid = true;   break;

        default:
          valid = false;  break;
      }

      if (valid)  {
        // prüfe noch die Blockgröße
        switch (charBlockSize)   {
          case 'S':     // satzweise senden
          case 'B':     // blockweise senden
          case 'T':     // blockweise in einer Transaktion senden
            valid = true;   break;

          default:
            valid = false;  break;
        }
      }

      return valid;
    }


    public char GetHitAction()  {
      if (!isValidAction()) return default(char);
      return Action[0];
    }




    /// <summary>
    /// Ermittle anhand der HIT-Aktion, welches HTTP-Verb verwendet werden soll
    /// </summary>
    /// <returns><see cref="RestClient.Verb"/></returns>
    public RestClient.Verb GetVerb() {
      if (!isValidAction()) return RestClient.Verb.None;

      RestClient.Verb enumVerb;
      switch (GetHitAction()) {
        case 'R':
          enumVerb =  RestClient.Verb.Get;     break;

        case 'I':
          enumVerb =  RestClient.Verb.Put;     break;

        case 'X':
        case 'U':
        case 'C':
          enumVerb =  RestClient.Verb.Post;    break;

        case 'S':
        case 'D':
          enumVerb =  RestClient.Verb.Delete;  break;

        default:
          enumVerb =  RestClient.Verb.None;    break;
      }

      return enumVerb;
    }



    /// <summary>
    /// Liefere den kompletten HITP-Befehl.
    /// </summary>
    /// <returns>Befehl, ggf, mit Subcodes</returns>
    public String GetHitCommand() {
      // wenn die Aktion nicht stimmt, lass es gut sein
      if (!isValidAction()) return null;

      String strCmd = Action.ToUpper();
      if (!String.IsNullOrEmpty(Subcodes))  {
        strCmd += "/" + Subcodes;
      }

      return strCmd;
    }



    public URI CreateURI(RestClient pobjClient,Credentials pobjCred) {
      if (pobjClient == null)  throw new ArgumentNullException(nameof(pobjClient),"We need a RestClient for it");

      // der Client gibt die URI vor
      URI pobjURI = pobjClient.CreateURI();
      pobjURI.RestPath = Entity;

      // die URI für den Task füllen und dabei AuthenticationMode berücksichtigen
      if (pobjCred != null) switch (pobjCred.AuthenticationMode) {
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
          pobjURI.Query["bnr"] = pobjCred.Betriebsnummer;
          if (!String.IsNullOrWhiteSpace(pobjCred.Mitbenutzer))  {
            pobjURI.Query["mbn"] = pobjCred.Mitbenutzer;
          }
          if (pobjClient.Secret == null) {
            // ohne Secret brauchen wir die PIN
            pobjURI.Query["pin"] = pobjCred.PIN;
          }
          else  {
            pobjURI.Query["cacheSecret"] = pobjClient.Secret;     
          }
          // der Timeout muss immer geschickt werden, weil der steuert, ob Sitzung bestehen bleibt oder nicht
          pobjURI.Query["cacheTimeout"]  = pobjCred.Timeout.ToString();
          break;

        case AuthMode.NoAuth:
        default:
          // nichts weiter
          break;
      }

      return pobjURI;
    }


//--------------------------------------------------------------------
  }
}
