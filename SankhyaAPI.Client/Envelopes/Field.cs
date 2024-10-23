using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class Field
{
    public Field()
    {
    }
    [XmlAttribute(AttributeName = "name")] public string? Nome { get; set; }
}