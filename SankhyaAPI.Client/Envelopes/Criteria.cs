using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class Criteria
{
    [XmlElement(ElementName = "expression")]
    public string? Expression { get; set; }

    [XmlElement(ElementName = "parameter")]
    public List<Parameter>? Parameter { get; set; }
}