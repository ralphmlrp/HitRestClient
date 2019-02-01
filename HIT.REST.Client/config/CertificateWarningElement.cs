using System;
using System.Configuration;



namespace HIT.REST.Client.config {

  public class CertificateWarningElement : ConfigurationElement {
//--------------------------------------------------------------------

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


       
//--------------------------------------------------------------------
  }

}
