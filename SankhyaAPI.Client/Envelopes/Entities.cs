using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using SankhyaAPI.Client.Utils;

namespace SankhyaAPI.Client.Envelopes;

public class Entities<TEntity> : IXmlSerializable where TEntity : class
{
    [XmlAttribute(AttributeName = "total")]
    public string? Total { get; set; }

    [XmlElement(ElementName = "metadata")] public Metadata? Metadata { get; set; }

    [XmlElement(ElementName = "entity")] public List<TEntity> Entity { get; set; } = new();

    public XmlSchema? GetSchema()
    {
        return null;
    }

    public void ReadXml(XmlReader reader)
    {
        // Lê o atributo "total" no elemento <entities>
        if (reader.MoveToAttribute("total"))
        {
            Total = reader.Value;
            reader.MoveToElement(); // Retorna para o elemento <entities>
        }

            // Lê cada <entity> dentro de <entities>
            while (reader.ReadToFollowing("entity"))
            {
                var entityInstance = Activator.CreateInstance<TEntity>();
                PropertyInfo[] properties = entityInstance.GetType().GetProperties();
                reader.Read(); // Entra no elemento <entity>

                // Itera sobre as propriedades e mapeia cada uma delas para o XML
                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    string? xmlElementName = property.GetXmlElementName();

                    if (xmlElementName == null ||
                        !reader.IsStartElement(xmlElementName) && !reader.IsStartElement($"f{i}")) continue;

                    string value = reader.ReadElementContentAsString();
                    object? convertedValue = ObjectUtilsMethods.ConvertForPropertyType(value, property);

                    property.SetValue(entityInstance, convertedValue);
                }

                // Adiciona a instância preenchida à lista de entidades
                Entity.Add(entityInstance);
            }
    }

    public void WriteXml(XmlWriter writer)
    {
        if (Total != null)
            writer.WriteAttributeString("total", Total);

        if (Metadata != null)
        {
            writer.WriteStartElement("metadata");
            var serializer = new XmlSerializer(typeof(Metadata));
            serializer.Serialize(writer, Metadata);
            writer.WriteEndElement();
        }

        foreach (TEntity? entity in Entity)
        {
            writer.WriteStartElement("entity");

            PropertyInfo[] properties = entity.GetType().GetProperties();

            foreach (PropertyInfo? property in properties)
            {
                string? xmlElementName = property.GetXmlElementName();

                // Escreve o valor da propriedade no XML
                if (xmlElementName == null) continue;

                object? value = property.GetValue(entity);
                if (value != null)
                {
                    writer.WriteElementString(xmlElementName, value.ToString());
                }
            }

            writer.WriteEndElement();
        }
    }
}