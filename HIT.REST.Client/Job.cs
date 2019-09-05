using System;
using System.Collections.Generic;
using System.IO;
using HIT.REST.Client.Hit;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace HIT.REST.Client {

  /// <summary>
  /// Beschreibt für ein Login die durchzuführenden Anfragen
  /// (Meldungen oder Abfragen).
  /// </summary>
  public class Job {
//--------------------------------------------------------------------

    /// <summary>
    /// Anmeldedaten für die <see cref="Tasks"/>.
    /// </summary>
    public Credentials  Credentials { get; set; }

    /// <summary>
    /// 0..n <see cref="Tasks"/> zum Durchführen
    /// </summary>
    public List<Task>   Tasks       { get; set; }



//--------------------------------------------------------------------

    /// <summary>
    /// Job anlegen
    /// </summary>
    public Job() {
      Credentials = new Credentials();
      Tasks       = new List<Task>();
    }



    public static Job fromFile(String pstrPath) {
      // JSON in Instanz wandeln
      string json;
      using (StreamReader reader = new StreamReader(pstrPath)) {
        json = reader.ReadToEnd();
      }
      // Der StringEnumConverter konvertiert Enum-Werte zusätzlich als Namen
      // und nicht nur als numerische Werte
      try {
        return JsonConvert.DeserializeObject<Job>(json,new StringEnumConverter());
      }
      catch (Exception e) {
        Program.tee("Fehler beim Einlesen der Job-Datei: "+e.Message);
      }
      return null;
    }



//--------------------------------------------------------------------

    /// <summary>
    /// Demo-Aufruf zum automatischen Erzeugen der Job-Datei.
    /// </summary>
    /// <param name="pstrFilename">Zielpfad, wohin geschrieben werden soll</param>
    /// <param name="pstrFolder">in welchem Ordner die Job-Datei und dazugehörigen
    ///   Datendateien liegen. Vorabe ist "Daten".</param>
    public static void ToJSON(String pstrFilename,String pstrFolder = "Daten") {
      if (String.IsNullOrWhiteSpace(pstrFilename))  throw new ArgumentException("Leer?");
      if (pstrFilename.Contains(Path.DirectorySeparatorChar.ToString())) throw new ArgumentException("Nur Dateiname ist zulässig!");

      String strTemp = pstrFolder + Path.DirectorySeparatorChar + pstrFilename;


      Job info = new Job();
      info.Credentials.Betriebsnummer   = "09 000 000 0015";
      info.Credentials.Mitbenutzer      = "0";
      info.Credentials.PIN              = "900015";
      info.Credentials.TimeoutInSeconds = 20;

      info.Tasks.Add(new Task() {
        Action      = "RS",
        Entity      = "Geburt",
        InputPath   = pstrFolder + Path.DirectorySeparatorChar + "GeburtRequest.txt",
        OutputPath  = pstrFolder + Path.DirectorySeparatorChar + "GeburtResponse.txt"
      });

      info.Tasks.Add(new Task() {
        Action      = "RS",
        Entity      = "Zugang",
        InputPath   = pstrFolder + Path.DirectorySeparatorChar + "ZugangRequest.txt",
        OutputPath  = pstrFolder + Path.DirectorySeparatorChar + "ZugangResponse.txt"
      });

      string jsonJob = JsonConvert.SerializeObject(info);
      File.WriteAllText(strTemp,jsonJob);
    }



//--------------------------------------------------------------------
  }





}
