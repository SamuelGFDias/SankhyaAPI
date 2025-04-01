using System.Reflection;
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
        PropertyInfo[] properties = GetType().GetProperties();

        while (reader.ReadToFollowing("entity"))
        {
            reader.Read();

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                string? xmlElementName = property.GetXmlElementName();

                if (xmlElementName == null ||
                    !reader.IsStartElement(xmlElementName) && !reader.IsStartElement($"f{i}")) continue;

                string value = reader.ReadElementContentAsString();
                object? convertedValue = ObjectUtilsMethods.ConvertForPropertyType(value, property);

                property.SetValue(this, convertedValue);
            }
        }
    }

    public virtual void WriteXml(XmlWriter writer)
    {
        PropertyInfo[] properties = GetType().GetProperties();

        foreach (PropertyInfo property in properties)
        {
            object? value = property.GetValue(this);
            if (value == null) continue;

            string formattedValue = ObjectUtilsMethods.GetFormattedString(value);

            PrimaryKeyElementAttribute? keyAttribute = property.GetCustomAttribute<PrimaryKeyElementAttribute>();
            if (keyAttribute is { AutoEnumerable: true }) continue;

            XmlAttributeAttribute? xmlAttribute = property.GetCustomAttribute<XmlAttributeAttribute>();
            if (xmlAttribute != null)
            {
                writer.WriteAttributeString(xmlAttribute.AttributeName, formattedValue);
            }
            else if (keyAttribute != null)
            {
                writer.WriteElementString(keyAttribute.ElementName, formattedValue);
            }
            else
            {
                string? xmlElementName = property.GetXmlElementName();
                if (string.IsNullOrWhiteSpace(xmlElementName)) continue;

                writer.WriteStartElement(xmlElementName);
                writer.WriteValue(formattedValue);
                writer.WriteEndElement();
            }
        }
    }
}