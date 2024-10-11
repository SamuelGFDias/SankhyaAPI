using System.Xml.Serialization;

namespace SankhyaAPI.Client.Entities.Response.Proxies;

public enum ItemCabacalhoNotaElementName
{
    None,
    f0,
    NUNOTA,
    f1,
    SEQUENCIA,
}

public class ItemCabecalhoNotaProxy
{
    [XmlElement("f0")]
    [XmlElement("NUNOTA")]
    [XmlChoiceIdentifier(nameof(NuCabacalhoNotaElementName))]
    public long NuNota { get; set; }

    [XmlIgnore] public ItemCabacalhoNotaElementName NuCabacalhoNotaElementName { get; set; }

    [XmlElement("f1")]
    [XmlElement("SEQUENCIA")]
    [XmlChoiceIdentifier(nameof(SequenciaElementName))]
    public long Sequencia { get; set; }

    [XmlIgnore] public ItemCabacalhoNotaElementName SequenciaElementName { get; set; }
}