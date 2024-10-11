using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class Field
{
    [XmlAttribute(AttributeName = "name")] public string? Nome { get; set; }
}