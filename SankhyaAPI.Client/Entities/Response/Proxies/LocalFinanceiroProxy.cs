using System.Xml.Serialization;

namespace SankhyaAPI.Client.Entities.Response.Proxies;

public enum LocalFinanceiroElementName
{
    None,
    f0,
    CODLOCAL,
}

public class LocalFinanceiroProxy
{
    [XmlElement("f0")]
    [XmlElement("CODLOCAL")]
    [XmlChoiceIdentifier(nameof(CodLocalElementName))]
    public long CodLocal { get; set; }

    [XmlIgnore] public LocalFinanceiroElementName CodLocalElementName { get; set; }
}