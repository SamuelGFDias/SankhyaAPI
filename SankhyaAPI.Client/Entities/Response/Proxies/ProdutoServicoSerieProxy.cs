using System.Xml.Serialization;

namespace SankhyaAPI.Client.Entities.Response.Proxies;

public enum ProdutoServicoSerieElementName
{
    None,
    f0,
    SERIE,
    f1,
    CODPROD,
    f2,
    NUMCONTRATO
}

public class ProdutoServicoSerieProxy
{
    [XmlElement("f0")]
    [XmlElement("SERIE")]
    [XmlChoiceIdentifier(nameof(SerieElementName))]
    public string? Serie { get; set; }

    [XmlIgnore] public ProdutoServicoSerieElementName SerieElementName { get; set; }

    [XmlElement("f1")]
    [XmlElement("CODPROD")]
    [XmlChoiceIdentifier(nameof(CodProdElementName))]
    public long CodProd { get; set; }

    [XmlIgnore] public ProdutoServicoSerieElementName CodProdElementName { get; set; }


    [XmlElement("f2")]
    [XmlElement("NUMCONTRATO")]
    [XmlChoiceIdentifier(nameof(NumContratoElementName))]
    public long NumContrato { get; set; }

    [XmlIgnore] public ProdutoServicoSerieElementName NumContratoElementName { get; set; }
}