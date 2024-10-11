using System.Xml.Serialization;
using Newtonsoft.Json;

namespace SankhyaAPI.Client.Entities.Response.Proxies;

public enum ProdutoElementName
{
    None,
    f0,
    CODPROD,
    f1,
    CODVOL,
    f2,
    DESCRPROD,
    f3,
    REFFORN
}

public class ProdutoProxy
{
    private string? _codVol;
    private string? _descrProd;
    private string? _refForn;

    [XmlElement("f0")]
    [XmlElement("CODPROD")]
    [XmlChoiceIdentifier(nameof(CodProdElementName))]
    public long CodProd { get; set; }

    [XmlIgnore][JsonIgnore] public ProdutoElementName CodProdElementName { get; set; }

    [XmlElement("f1")]
    [XmlElement("CODVOL")]
    [XmlChoiceIdentifier(nameof(CodVolElementName))]
    public string? CodVol
    {
        get => _codVol;
        set => _codVol = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [XmlIgnore][JsonIgnore] public ProdutoElementName CodVolElementName { get; set; }

    [XmlElement("f2")]
    [XmlElement("DESCRPROD")]
    [XmlChoiceIdentifier(nameof(DescrProdElementName))]
    public string? DescrProd
    {
        get => _descrProd;
        set => _descrProd = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [XmlIgnore][JsonIgnore] public ProdutoElementName DescrProdElementName { get; set; }

    [XmlElement("f3")]
    [XmlElement("REFFORN")]
    [XmlChoiceIdentifier(nameof(RefFornElementName))]
    public string? RefForn
    {
        get => _refForn;
        set => _refForn = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [XmlIgnore][JsonIgnore] public ProdutoElementName RefFornElementName { get; set; }
}