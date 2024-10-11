using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class SankhyaEntity
{
    [XmlAttribute(AttributeName = "path")] public string? Path { get; set; }
    [XmlElement(ElementName = "field")] public List<Field>? Field { get; set; }
    [XmlElement(ElementName = "fieldset")] public FieldSet? FieldSet { get; set; }
}