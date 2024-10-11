using System.Xml.Linq;
using System.Xml.Serialization;

namespace SankhyaAPI.Client.Envelopes;

public class DataRow<TEntity> where TEntity : class
{
    [XmlElement(ElementName = "localFields")]
    public TEntity? Entity { get; set; }

    [XmlAnyElement(Name = "key")] public XElement Key { get; set; } = new("key");
}