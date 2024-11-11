using SankhyaAPI.Client.Utils;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SankhyaAPI.Client.Extensions;


[Obsolete]
public abstract class XmlSerialable : IXmlSerializable
{
    public virtual XmlSchema? GetSchema()
    {
        return null;
    }

    public virtual void ReadXml(XmlReader reader)
    {
        var properties = GetType().GetProperties();

        while (reader.ReadToFollowing("entity"))
        {
            reader.Read();

            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                string? xmlElementName = property.GetXmlElementName();

                if (xmlElementName == null ||
                    !reader.IsStartElement(xmlElementName) && !reader.IsStartElement($"f{i}")) continue;

                string value = reader.ReadElementContentAsString();
                var convertedValue = ObjectUtilsMethods.ConvertForPropertyType(value, property);

                property.SetValue(this, convertedValue);
            }
        }
    }

    public virtual void WriteXml(XmlWriter writer)
    {
    }
}