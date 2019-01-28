using System;
using System.Collections.Generic;
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
  /// (hier z.B. bei BaseUrls.https)
  /// </remarks>
  public sealed class HitSettingsSection : ConfigurationSection {
//--------------------------------------------------------------------
    // with help of   https://stackoverflow.com/questions/3935331/how-to-implement-a-configurationsection-with-a-configurationelementcollection



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
    /// Basispfad zum Zugriffspunkt der HIT-REST-Schnittstelle.
    /// Muss jeweils mit einem "/" beginnen und enden.
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



//--------------------------------------------------------------------
  }





  /// <summary>
  /// Beschreibt Element &lt;BaseUrls&gt; als Collection.
  /// </summary>
  public class BaseUrlsCollection : ConfigurationElementCollection  {
//--------------------------------------------------------------------

    /// <summary>
    /// Legt neues Element für die Collection an, welches anschließend
    /// über dessen ConfigurationProperty mit Werten gefüllt wird
    /// </summary>
    /// <returns></returns>
    protected override ConfigurationElement CreateNewElement() {
      return new BaseUrlElement();
    }

    /// <summary>
    /// Da das ConfigurationElement in einer Map gespeichert wird,
    /// muss die ConfigurationElementCollection wissen, unter welchem Namen.
    /// Der wird mit diesem hier geliefert: hier ist es die Kombination
    /// aller Properties als URL.
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    protected override object GetElementKey(ConfigurationElement element) {
//      if (element == null)  {
//Console.WriteLine(new System.Diagnostics.StackTrace().ToString());
//        return null;
//      }
      return ((BaseUrlElement)element).SchemeAndDomainUrl;
    }



//--------------------------------------------------------------------
  }



  public class BaseUrlElement : ConfigurationElement {
//--------------------------------------------------------------------

    public BaseUrlElement() {
      
    }

    /// <summary>
    /// URL
    /// </summary>
    [ConfigurationProperty("https", DefaultValue = true)]   // IMPORTANT: der Name hier muss mit dem Namen beim this[]-Indexer exakt übereinstimmen!
    public bool UseHttps  {
      get {
        return (bool)this["https"];
      }
      set { this["https"] = value; }
    }

    [ConfigurationProperty("domain", DefaultValue = null, IsRequired = true)]
    public string Domain  {
      get { return (String)this["domain"]; }
      set { this["domain"] = value; }
    }

    [ConfigurationProperty("port", DefaultValue = -1)]
    public int Port {
      get { return (int)this["port"]; }
      set {
        if (value <= 0    ) value = 0;  // 0 or negative means "no port set"
        if (value >= 65536) throw new ArgumentException("There are no ports >= 65536!");
        this["port"] = value;
      }
    }


    /// <summary>
    /// Liefere vollständige URL mit Schema + Domain, einschließlich des "/" am Ende.
    /// Danach folgt direkt der 
    /// </summary>
    public String SchemeAndDomainUrl {
      get {
        String strScheme  = UseHttps ? "https" : "http";
        String strPort    = "";
        if (Port == 0)  {
          // use default
        }
        else if (UseHttps && Port != 443) {
          // if HTTPS & not port 443 then amend the port number
          strPort = ":"+Port;
        }
        else if (!(!UseHttps && Port == 80) || !(UseHttps && Port == 443)) {
          // if HTTP & not port 80 then amend the port number
          strPort = ":"+Port;
        }
        else  {
        }

        return strScheme+"://"+Domain+strPort+"/";
      }
    }



//--------------------------------------------------------------------
  }



  /// <summary>
  /// Pfad für Basis-Route. Daran anschließend kommen die einzelnen
  /// Aktionen der REST-Schnittstelle.
  /// </summary>
  public sealed class BasePathElement : ConfigurationElement  {

    public BasePathElement() : base() {}

    public BasePathElement(String pstrPath) : this()  {
      path  = pstrPath;
    }



    /// <summary>
    /// Basispfad zum Zugriffspunkt der HIT-REST-Schnittstelle.
    /// Muss jeweils mit einem "/" beginnen und enden.
    /// </summary>
    [ConfigurationProperty("path",IsRequired = true)]
    public String path {
      get {        
        return Convert.ToString(base["path"]);
      }
      set {
        if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException("Leerer BasePath?!",(Exception)null);
        if (!value.StartsWith("/")) throw new ArgumentException("<add path=\"\"> muss mit '/' beginnen!");
        if (!value.EndsWith("/"))   throw new ArgumentException("<add path=\"\"> muss mit '/' enden!");

        base["path"] = value;
      }
    }
  }




  public class CertificateWarningElement : ConfigurationElement {

    /// <summary>
    /// Sollen Zertifikatsfehler bei HTTPS ignoriert werden?
    /// </summary>
    [ConfigurationProperty("ignore",IsRequired = true)]
    public bool ignore {
      get {
        return Convert.ToBoolean(base["ignore"]);
      }
      set {
        base["ignore"] = value;
      }
    }

  }


}