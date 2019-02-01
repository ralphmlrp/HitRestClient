using System;
using System.Configuration;



namespace HIT.REST.Client.config {

  /// <summary>
  /// Pfad für Basis-Route. Daran anschließend kommen die einzelnen
  /// Aktionen der REST-Schnittstelle.
  /// </summary> 
  public sealed class BasePathElement : ConfigurationElement {
//--------------------------------------------------------------------

    public BasePathElement() : base() {}

    public BasePathElement(String pstrPath) : this()  {
      path  = pstrPath;
    }



//--------------------------------------------------------------------

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


        
//--------------------------------------------------------------------
  }

}
