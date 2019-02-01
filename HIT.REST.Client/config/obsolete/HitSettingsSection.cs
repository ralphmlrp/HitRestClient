using System;
using System.Configuration;
using System.Xml;
using System.Xml.Serialization;



namespace HIT.REST.Client.config.obsolete {

  /// <summary>
  /// <para>
  /// Definiert einen Handler, der unseren eigenen Configuration-Abschnitt
  /// in app.config verarbeiten kann.
  /// </para>
  /// <para>
  /// Diese Klasse muss als<br>
  ///    <tt>&lt;section name="hitSettings" type="HIT.REST.Client.config.HitSettingsSection, HIT.REST.Client" /&gt;</tt> <br>
  /// in app.config eingebunden werden!
  /// </para>
  /// </summary>
  [Obsolete("In .NET Framework version 2.0 and above, you must instead derive from the ConfigurationSection class to implement the related configuration section handler.")]
  public class HitSettingsSection_Obsolete : IConfigurationSectionHandler {

    public object Create(object parent,object configContext,XmlNode section) {
      XmlSerializer serializer = new XmlSerializer(typeof(ApiInformation));

      using (XmlNodeReader reader = new XmlNodeReader(section.FirstChild)) {
        return serializer.Deserialize(reader);
      }
    }

  }

}
