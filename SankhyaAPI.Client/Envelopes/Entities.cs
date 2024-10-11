using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class Entities<TEntity> where TEntity : class
{
    [XmlAttribute(AttributeName = "total")]
    public string? Total { get; set; }

    [XmlElement(ElementName = "metadata")] public Metadata? Metadata { get; set; }

    [XmlElement(ElementName = "entity")] public List<TEntity>? Entity { get; set; }
}