using System.Xml.Linq;
using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class DataRow
{
    [XmlAnyElement(Name = "localFields")]
    public XElement? Entity { get; set; } = new("key");

    [XmlAnyElement(Name = "key")] public XElement Key { get; set; } = new("key");
}