using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace SankhyaAPI.Client.Entities.Response.Proxies;

public enum CabecalhoNotaElementName
{
    None,
    f0,
    NUNOTA,
}

public class CabecalhoNotaProxy
{
    [XmlElement("f0")]
    [XmlElement("NUNOTA")]
    [XmlChoiceIdentifier(nameof(NuNotaElementName))]
    public long NuNota { get; set; }

    [XmlIgnore] [JsonIgnore] public CabecalhoNotaElementName NuNotaElementName { get; set; }
}