using System;
using System.Configuration;



namespace HIT.REST.Client.config {

  /// <summary>
  /// Pfad für allgemeines Logfile.
  /// </summary> 
  public sealed class LogFileElement : ConfigurationElement {
//--------------------------------------------------------------------

    public LogFileElement() : base() {}

    public LogFileElement(String pstrPath) : this()  {
      path  = pstrPath;
    }



//--------------------------------------------------------------------

    /// <summary>
    /// Basispfad zum Zugriffspunkt der HIT-REST-Schnittstelle.
    /// Muss jeweils mit einem "/" beginnen und enden.
    /// </summary>
    [ConfigurationProperty("path",DefaultValue = null)]
    public String path {
      get {        
        return Convert.ToString(base["path"]);
      }
      set {
        if (value != null)  {
          if (!value.StartsWith("/")) throw new ArgumentException("<add path=\"\"> muss mit '/' beginnen!");
          if (!value.EndsWith("/"))   throw new ArgumentException("<add path=\"\"> muss mit '/' enden!");
        }

        base["path"] = value;
      }
    }



    /// <summary>
    /// Soll an ein bestehendes Logfile angehängt werden oder nicht?
    /// </summary>
    [ConfigurationProperty("append",DefaultValue = false)]
    public bool append {
      get {
        return Convert.ToBoolean(base["append"]);
      }
      set {
        base["append"] = value;
      }
    }



    public bool hasLogfilePath()  {
      return (path != null);
    }


        
//--------------------------------------------------------------------
  }

}
