using System;
using System.Collections.Generic;



namespace HIT.REST.Client.config.obsolete {

  /// <summary>
  /// Beschreibt eine 
  /// </summary>
  [Obsolete("Since HitSettingsSection derived from IConfigurationSectionHandler is obsolete, this class is obsolete too. See new HitSettingsSection derived from ConfigurationSection")]
  public class ApiInformation {

    /// <summary>
    /// Liste von Basis-URLs zu HIT-REST-Schnittstellen. Sie besteht jeweils aus <tt>schema://domain/</tt>;
    /// <tt>schema</tt> ist entweder <tt>http</tt> oder <tt>https</tt>.
    /// </summary>
    public List<String> BaseUrls { get; set; }

    /// <summary>
    /// Basispfad zum Zugriffspunkt der HIT-REST-Schnittstelle.
    /// Muss jeweils mit einem "/" beginnen und enden.
    /// </summary>
    public string BasePath {
      get { return strBasePath; }
      set {
        if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException("Leerer BasePath?!",(Exception)null);
        if (!value.StartsWith("/")) throw new ArgumentException("BasePath muss mit '/' beginnen!");
        if (!value.EndsWith("/"))   throw new ArgumentException("BasePath muss mit '/' enden!");
        strBasePath = value;
      }
    }
    private string strBasePath;

    /// <summary>
    /// Sollen Zertifikatsfehler bei HTTPS ignoriert werden?
    /// </summary>
    public bool SuppressCertificateWarning { get; set; }
  }

}
