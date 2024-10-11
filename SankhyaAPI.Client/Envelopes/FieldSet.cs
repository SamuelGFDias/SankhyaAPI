using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class FieldSet
{
    [XmlAttribute(AttributeName = "list")] public string List { get; set; } = "*";
}