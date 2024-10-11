using System.Xml.Serialization;

namespace SankhyaAPI.Client.Entities.Response.Proxies;

public enum PedidoOsItemElementName
{
    None,
    f0,
    NUNOTA
}

public class PedidoOsItemProxy
{
    [XmlElement("f0")]
    [XmlElement("NUNOTA")]
    [XmlChoiceIdentifier(nameof(NunotaElementName))]
    public long Nunota { get; set; }

    [XmlIgnore] public PedidoOsItemElementName NunotaElementName { get; set; }
}