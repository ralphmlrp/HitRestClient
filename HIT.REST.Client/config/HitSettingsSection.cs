using System.Configuration;



namespace HIT.REST.Client.config {

  /// <summary>
  /// <para>
  /// Definiert einen app.config-Abschnitt für die eigene Anwendung.
  /// </para>
  /// <para>
  /// Diese Klasse muss als<br>
  ///    <tt>&lt;section name="hitSettings" type="HIT.REST.Client.config.HitSettingsSection, HIT.REST.Client" /&gt;</tt> <br>
  /// in app.config eingebunden werden!
  /// </para>
  /// </summary>
  /// <remarks>
  /// Anmerkung:
  /// Das Verarbeiten einer ConfigurationSection wird nicht mehr via
  /// IConfigurationSectionHandler abgewickelt, weil der Handler seit .NET 2.0
  /// als [Obsolete] gekennzeichnet ist!
  /// Es werden daher klassisch die einzelnen Elemente als ConfigurationElement
  /// definiert und in einer ConfigurationSection zusammengefasst.
  ///
  /// Und ganz wichtig:
  /// Der Name der ConfigurationProperty muss EXAKT (case sensitive) dem entsprechen,
  /// unter dem er in der eigenen ConfigurationCollection gespeichert wird!
  /// (hier z.B. bei "BaseUrls.https", auch wenn die dazugehörige Property "UseHttps" heißt!)
  ///
  /// In der vorherigen Version gab es noch ein XmlSerialize zum
  /// Erzeugen einer Section. Die wurde entfernt, weil man aus einer
  /// ConfigurationSection kein serialisiertes XML erzeugen kann!
  /// (es erfordert einen "default indexer" in einer untergeordneten
  /// Configuration-Klasse, welche man nicht nachträglich hinzufügen kann)
  /// </remarks>
  public sealed class HitSettingsSection : ConfigurationSection  {
//--------------------------------------------------------------------
    // with help of   https://stackoverflow.com/questions/3935331/how-to-implement-a-configurationsection-with-a-configurationelementcollection



    /// <summary>
    /// Optionaler Pfad zu einem allgemeinen Logfile.
    /// </summary>
    [ConfigurationProperty("LogFile")]
    public LogFileElement LogFile {
      get {
        return (LogFileElement)(base["LogFile"]);
      }
      set {
        base["LogFile"] = value;
      }
    }

    

    /// <summary>
    /// Liste von Basis-URLs zu HIT-REST-Schnittstellen.
    /// Sie besteht jeweils aus <tt>schema://domain/</tt>;
    /// <tt>schema</tt> ist entweder <tt>http</tt> oder <tt>https</tt>.
    /// </summary>
    /// <remarks>
    /// Property ist als ConfigurationCollection definiert, d.h. mit &lt;BaseUrls ...&gt;
    /// wird eine interne Liste angelegt, die mit enthaltenden &lt;add ...&gt; gefüttert wird
    /// </remarks>
    [ConfigurationProperty("BaseUrls", IsDefaultCollection = false)]
    [ConfigurationCollection(typeof(BaseUrlsCollection))]
    public BaseUrlsCollection BaseUrls  {
      get {
        return (BaseUrlsCollection)base["BaseUrls"];
      }
    }



    /// <summary>
    /// Konfigurationeelement für den BasePath
    /// </summary>
    [ConfigurationProperty("BasePath",IsRequired = true)]
    public BasePathElement BasePath {
      get {
        return (BasePathElement)(base["BasePath"]);
      }
      set {
        base["BasePath"] = value;
      }
    }



    /// <summary>
    /// Wie mit Zertifikatsfehlern bei HTTPS umgehen?
    /// </summary>
    [ConfigurationProperty("CertificateWarning",IsRequired = false)]
    public CertificateWarningElement CertificateWarning {
      get {
        return (CertificateWarningElement)(base["CertificateWarning"]);
      }
      set {
        base["CertificateWarning"] = value;
      }
    }




/*
Lässt sich nicht so umsetzen:
Es erfordert einen "default indexer" in einer untergeordneten
Configuration-Klasse, welche man nicht nachträglich hinzufügen kann!

Aufruf wäre:
HitSettingsSection.CreateXml("hitSettings.section.txt");


    public static void CreateXml(string pstrPath) {
      if (String.IsNullOrWhiteSpace(pstrPath))  throw new ArgumentNullException();

      HitSettingsSection sectionData = new HitSettingsSection();
      sectionData.LogFile.path              = "HIT.REST.Client.log";
      sectionData.LogFile.append            = true;
      sectionData.BasePath.path             = "/api/mlrp/";
      sectionData.CertificateWarning.ignore = true;
      sectionData.BaseUrls.Add(new BaseUrlElement() { Domain = "localhost",               UseHttps = false, Port = 5592 });
      sectionData.BaseUrls.Add(new BaseUrlElement() { Domain = "www.hi-tier.bybn.de",     UseHttps = true               });
      sectionData.BaseUrls.Add(new BaseUrlElement() { Domain = "www-dev.hi-tier.bybn.de", UseHttps = true               });

      XmlSerializer serializer = new XmlSerializer(sectionData.GetType());
      using (FileStream fs = new FileStream(pstrPath,System.IO.FileMode.Create)) {
        serializer.Serialize(fs,sectionData);     // scheitert hier 
      }
    }


 */   



//--------------------------------------------------------------------
  }

}