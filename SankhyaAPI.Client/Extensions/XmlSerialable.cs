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
    }

    public virtual void WriteXml(XmlWriter writer)
    {
        var properties = GetType().GetProperties();
        foreach (var property in properties)
        {
            if (!ShouldSerializeProperty(property)) continue;
            var value = property.GetValue(this);
            if (value == null || string.IsNullOrEmpty(value.ToString())) continue;
            var xmlElementAttr = property.GetCustomAttribute<XmlElementAttribute>();
            var elementName = xmlElementAttr?.ElementName ?? property.Name;

            if (string.IsNullOrEmpty(elementName)) continue;
            writer.WriteStartElement(elementName);
            writer.WriteString(value.ToString());
            writer.WriteEndElement();
        }
    }

    private bool ShouldSerializeProperty(PropertyInfo property)
    {
        if (property.GetCustomAttribute<XmlIgnoreAttribute>() != null) return false;
        var value = property.GetValue(this);
        return value != null;
    }

    private static bool IsDefaultValue(object value, Type type)
    {
        if (type == typeof(string)) return string.IsNullOrEmpty((string)value);

        return Nullable.GetUnderlyingType(type) != null
            ? IsDefaultValue(value, Nullable.GetUnderlyingType(type)!)
            : value.Equals(Activator.CreateInstance(type));
    }
}