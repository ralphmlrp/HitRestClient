using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace HIT.REST.Client
{
    public class HitSettingsSection : IConfigurationSectionHandler
    {
        public object Create(object parent, object configContext, XmlNode section)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ApiInformation));
            
            using (XmlNodeReader reader = new XmlNodeReader(section.FirstChild))
            {
                return serializer.Deserialize(reader);
            }             
        }
    }
}
