using System;
using System.Configuration;



namespace HIT.REST.Client.config {

  /// <summary>
  /// Beschreibt Basisadresse der URL zum HIT3-REST-Interface.
  /// Sie besteht lediglich aus Schema und Hostname, ggf. mit Portnummer.
  /// Man erhält somit maximal <tt>schema:://hostname:port/</tt>.
  /// </summary>
  public class BaseUrlElement : ConfigurationElement  {
//--------------------------------------------------------------------

    public BaseUrlElement() { }

    /// <summary>
    /// Manuell anlegen.
    /// </summary>
    /// <param name="pstrDomain"></param>
    /// <param name="pboolUseHttps"></param>
    /// <param name="pintPort"></param>
    public BaseUrlElement(String pstrDomain,bool pboolUseHttps = true,int pintPort = -1,String pstrRootPath = null) : this()  {
      UseHttps  = pboolUseHttps;
      Domain    = pstrDomain;
      Port      = pintPort;
      RootPath  = pstrRootPath;
    }
    
    /// <summary>
    /// URL
    /// </summary>
    [ConfigurationProperty("https", DefaultValue = true)]   // IMPORTANT: der Name hier im ConfigurationProperty() muss mit dem Namen beim this[]-Indexer exakt übereinstimmen!
    public bool UseHttps  {
      get { return (bool)this["https"]; }
      set { this["https"] = value; }
    }

    [ConfigurationProperty("domain", DefaultValue = null, IsRequired = true)]
    public string Domain  {
      get { return (String)this["domain"]; }
      set { this["domain"] = value; }
    }

    [ConfigurationProperty("port", DefaultValue = 0)]
    public int Port {
      get { return (int)this["port"]; }
      set {
        if (value <= 0    ) value = 0;  // 0 or negative means "no port set"
        if (value >= 65536) throw new ArgumentException("There are no ports >= 65536!");
        this["port"] = value;
      }
    }

    [ConfigurationProperty("path", DefaultValue = null)]
    public String RootPath  {
      get { return (String)this["path"]; }
      set {
        if (value != null)  {
          while (value.StartsWith("/")) value = value.Substring(1);
          while (value.EndsWith  ("/")) value = value.Substring(0,value.Length-1);
          if (value.Length == 0)  value = null;
        }
        this["path"] = value;
      }
    }



    /// <summary>
    /// <para>
    /// Liefere vollständige URL mit Schema + Domain, einschließlich des "/" am Ende.
    /// Danach folgt direkt der Pfad samt QueryString und Anker.
    /// </para>
    /// <para>
    /// Wird auch als Schlüssel für die übergeordnete <see cref="ConfigurationElementCollection"/> verwendet.
    /// </para>
    /// </summary>
    public String BaseUrl {
      get {
        String strScheme  = UseHttps ? "https" : "http";
        String strPort    = "";
        if (Port > 0) {
          bool addPort = false;
          addPort |= (Port !=  80 && !UseHttps);
          addPort |= (Port != 443 && UseHttps);
          if (addPort) strPort = ":"+Port;
        }
        String strRootPath    = "";
        if (!String.IsNullOrEmpty(RootPath))  {
          strRootPath = RootPath+"/";
        }

        return strScheme+"://"+Domain+strPort+"/"+strRootPath;
      }
    }



//--------------------------------------------------------------------
  }

}
