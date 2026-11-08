using System.Xml.Serialization;
using SankhyaAPI.Client.Mapping;

namespace SankhyaAPI.Client.Benchmarks;

/// <summary>
/// Modelo mapeado por atributos (caminho da 5.x): entidade Produto.
/// </summary>
[SankhyaEntity("Produto")]
public class ProdutoAtributos
{
    [Key("CODPROD", true)] public NullableState<long> CodProd { get; set; }
    [XmlElement("DESCRPROD")] public NullableState<string> DescrProd { get; set; }
    [XmlElement("PRECO")] public NullableState<decimal> Preco { get; set; }
    [XmlElement("DTALTER")] public NullableState<DateTime> DtAlter { get; set; }
    [XmlElement("ATIVO")] public NullableState<bool> Ativo { get; set; }
}

/// <summary>
/// O mesmo produto sem anotações, mapeado pelo mapa fluente <see cref="ProdutoPocoMap"/>.
/// </summary>
public class ProdutoPoco
{
    public long CodProd { get; set; }
    public string? DescrProd { get; set; }
    public decimal? Preco { get; set; }
    public DateTime? DtAlter { get; set; }
    public bool? Ativo { get; set; }
}

public sealed class ProdutoPocoMap : SankhyaEntityMap<ProdutoPoco>
{
    public ProdutoPocoMap()
    {
        Entity("Produto");
        Key(x => x.CodProd, "CODPROD").AutoNumbered();
        Property(x => x.DescrProd, "DESCRPROD");
        Property(x => x.Preco, "PRECO");
        Property(x => x.DtAlter, "DTALTER");
        Property(x => x.Ativo, "ATIVO");
    }
}

/// <summary>
/// Linha de executeQuery mapeada por atributos (<c>[XmlElement]</c> com o nome da coluna).
/// </summary>
public class ProdutoLinha
{
    [XmlElement("CODPROD")] public long? CodProd { get; set; }
    [XmlElement("DESCRPROD")] public string? DescrProd { get; set; }
    [XmlElement("PRECO")] public decimal? Preco { get; set; }
    [XmlElement("DTALTER")] public DateTime? DtAlter { get; set; }
    [XmlElement("ATIVO")] public bool? Ativo { get; set; }
}
