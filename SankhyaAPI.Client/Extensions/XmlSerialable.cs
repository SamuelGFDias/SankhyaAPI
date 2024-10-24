using SankhyaAPI.Client.Utils;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SankhyaAPI.Client.Extensions;

public abstract class XmlSerialable : IXmlSerializable
{
    public virtual XmlSchema? GetSchema()
    {
        return null;
    }

    public virtual void ReadXml(XmlReader reader)
    {
        var properties = GetType().GetProperties();
        reader.Read();

        for (int i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            string? xmlElementName = property.GetXmlElementName();

            if (xmlElementName == null || !reader.IsStartElement(xmlElementName) && !reader.IsStartElement($"f{i}")) continue;

            string? value = reader.ReadElementContentAsString();
            var convertedValue = ObjectUtilsMethods.ConvertForPropertyType(value, property);

            property.SetValue(this, convertedValue);
        }
    }

    public virtual void WriteXml(XmlWriter writer)
    {
        var properties = GetType().GetProperties();
        foreach (var property in properties)
        {
            if (!ShouldSerializeProperty(property)) continue;

            object? value = property.GetValue(this);
            if (value == null || string.IsNullOrWhiteSpace(value.ToString())) continue;

            var xmlElementAttr = property.GetCustomAttribute<XmlElementAttribute>();
            string? elementName = xmlElementAttr?.ElementName;

            var primaryKeyAttr = property.GetCustomAttribute<PrimaryKeyElementAttribute>();
            if (primaryKeyAttr is { AutoEnumerable: false })
            {
                elementName = primaryKeyAttr.ElementName;
            }

            if (string.IsNullOrEmpty(elementName)) continue;

            string newValue = ObjectUtilsMethods.GetFormattedString(value);

            writer.WriteStartElement(elementName);
            writer.WriteString(newValue);
            writer.WriteEndElement();
        }
    }


    private bool ShouldSerializeProperty(PropertyInfo property)
    {
        var xmlIgnoreAttr = property.GetCustomAttribute<XmlIgnoreAttribute>();
        if (xmlIgnoreAttr != null) return false;

        var primaryKeyAttr = property.GetCustomAttribute<PrimaryKeyElementAttribute>();
        if (primaryKeyAttr is { AutoEnumerable: true }) return false;

        object? value = property.GetValue(this);
        return value != null && !string.IsNullOrWhiteSpace(value.ToString()) ;
    }
}