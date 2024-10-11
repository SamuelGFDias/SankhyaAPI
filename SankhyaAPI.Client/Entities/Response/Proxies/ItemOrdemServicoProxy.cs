using System.Xml.Serialization;
using Newtonsoft.Json;

namespace SankhyaAPI.Client.Entities.Response.Proxies;

public enum OsUpdateElementName
{
    None,
    f0,
    NUMOS,
    f1,
    NUMITEM,
}

public class ItemOrdemServicoProxy
{
    [XmlElement("NUMOS")]
    [XmlElement("f0")]
    [XmlChoiceIdentifier(nameof(NumOsElementName))]
    public long NumOs { get; set; }

    [XmlIgnore] [JsonIgnore] public OsUpdateElementName NumOsElementName { get; set; }

    [XmlElement("NUMITEM")]
    [XmlElement("f1")]
    [XmlChoiceIdentifier(nameof(NumItemElementName))]
    public long NumItem { get; set; }

    [XmlIgnore] [JsonIgnore] public OsUpdateElementName NumItemElementName { get; set; }
}