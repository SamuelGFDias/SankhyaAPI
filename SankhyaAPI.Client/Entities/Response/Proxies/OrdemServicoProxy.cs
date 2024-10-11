using System.Xml.Serialization;

namespace SankhyaAPI.Client.Entities.Response.Proxies;

public enum OrdemServicoElementName
{
    None,
    f0,
    NUMOS,
}

public class OrdemServicoProxy
{
    [XmlElement("f0")]
    [XmlElement("NUMOS")]
    [XmlChoiceIdentifier(nameof(NumOsElementName))]
    public long NumOs { get; set; }

    [XmlIgnore] public OrdemServicoElementName NumOsElementName { get; set; }
}