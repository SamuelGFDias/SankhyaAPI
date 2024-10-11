using System.Xml.Serialization;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Entities.Request;

public class ItemCabecalhoNotaRequest : XmlSerialable, IObjectWithKey
{
    [XmlIgnore]
    [PrimaryKeyElement("NUNOTA")]
    public long? NuNota { get; set; }

    [XmlIgnore]
    [PrimaryKeyElement("SEQUENCIA")]
    public long? Sequencia { get; set; }
}