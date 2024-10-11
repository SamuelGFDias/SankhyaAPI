using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class Metadata
{
    [XmlArrayItem("field")]
    [XmlArray("fields")]
    public List<Field>? Fields { get; set; }
}