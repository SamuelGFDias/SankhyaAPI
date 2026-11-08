using System.Xml.Serialization;



namespace SankhyaAPI.Client.Tests.Support;

/// <summary>
/// Usuário do Sankhya (tabela TSIUSU, entidade "Usuario"). O CODUSU 0 (SUP) existe em toda instalação,
/// por isso serve de alvo para testes de leitura contra uma base real.
/// </summary>
[SankhyaEntity("Usuario")]
public class Usuario
{
    [Key("CODUSU")] public NullableState<int> CodUsu { get; set; }
    [XmlElement("NOMEUSU")] public NullableState<string> NomeUsu { get; set; }
}

/// <summary>
/// Modelo com chave auto-enumerada, para exercitar insert sem chave e update com chave.
/// </summary>
[SankhyaEntity("Produto")]
public class Produto
{
    [Key("CODPROD", true)] public NullableState<long> CodProd { get; set; }
    [XmlElement("DESCRPROD")] public NullableState<string> DescrProd { get; set; }
    [XmlElement("PRECO")] public NullableState<decimal> Preco { get; set; }
    [XmlElement("DTALTER")] public NullableState<DateTime> DtAlter { get; set; }
    [XmlElement("ATIVO")] public NullableState<bool> Ativo { get; set; }
}

/// <summary>
/// Linha tipada de consulta SQL (QueryRawAsync). Não precisa herdar de SankhyaModelBase.
/// </summary>
public class UsuarioLinha
{
    [XmlElement("CODUSU")] public int? CodUsu { get; set; }
    [XmlElement("NOMEUSU")] public string? NomeUsu { get; set; }
}

/// <summary>
/// Tabela AD_TESTE da homologação, criada só para testes reais de escrita: uma coluna de cada tipo de
/// campo que o pacote converte. Só ID é obrigatório.
/// </summary>
[SankhyaEntity("AD_TESTE")]
public class AdTeste
{
    [Key("ID")] public NullableState<int> Id { get; set; }
    [XmlElement("TEXTO")] public NullableState<string> Texto { get; set; }
    [XmlElement("INTEIRO")] public NullableState<int> Inteiro { get; set; }
    [XmlElement("AD_DECIMAL")] public NullableState<decimal> Decimal { get; set; }
    [XmlElement("AD_DATA")] public NullableState<DateOnly> Data { get; set; }
    [XmlElement("DATAHORA")] public NullableState<DateTime> DataHora { get; set; }
    [XmlElement("HORA")] public NullableState<TimeOnly> Hora { get; set; }
    [XmlElement("ATIVO")] public NullableState<bool> Ativo { get; set; }
    [XmlElement("SITUACAO")] public NullableState<SituacaoTeste> Situacao { get; set; }
}

/// <summary>
/// Lista de opções do campo SITUACAO da AD_TESTE.
/// </summary>
public enum SituacaoTeste
{
    [XmlEnum("A")] Ativo,
    [XmlEnum("I")] Inativo,
    [XmlEnum("P")] Pendente
}

/// <summary>
/// Cidade (TSICID, entidade "Cidade"). Na homologação passa de 10 mil linhas: serve para testar limites só lendo.
/// </summary>
[SankhyaEntity("Cidade")]
public class Cidade
{
    [Key("CODCID")] public NullableState<int> CodCid { get; set; }
    [XmlElement("NOMECID")] public NullableState<string> NomeCid { get; set; }
}

public class CidadeLinha
{
    [XmlElement("CODCID")] public int? CodCid { get; set; }
}

/// <summary>
/// Chave composta com um único campo auto-numerado (como item de nota: NUNOTA + SEQUENCIA).
/// </summary>
[SankhyaEntity("ItemNota")]
public class ItemNota
{
    [Key("NUNOTA")] public NullableState<long> NuNota { get; set; }
    [Key("SEQUENCIA", true)] public NullableState<int> Sequencia { get; set; }
    [XmlElement("QTDNEG")] public NullableState<decimal> Quantidade { get; set; }
}

/// <summary>
/// Modelo de domínio sem anotações, mapeado por <see cref="OrdemServicoMap"/>.
/// </summary>
public class OrdemServico
{
    public int Numero { get; set; }
    public string? Descricao { get; set; }
    public DateTime? DataExecucao { get; set; }
    public StatusOs Status { get; set; }
    public string? NomeParceiro { get; set; }
    public string? NomeUsuario { get; set; }
}

public enum StatusOs { Aberta, Fechada }

public sealed class OrdemServicoMap : SankhyaEntityMap<OrdemServico>
{
    public OrdemServicoMap()
    {
        Entity("OrdemServico");
        Key(x => x.Numero, "NUMOS").AutoNumbered();
        Property(x => x.Descricao, "DESCRICAO");
        Computed(x => x.DataExecucao)
            .Writes("DT_FINAL", SankhyaType.Date)
            .Writes("HR_FINAL", SankhyaType.Time)
            .Reads<DateOnly?, TimeOnly?>("DT_FINAL", "HR_FINAL", (d, h) => d?.ToDateTime(h ?? TimeOnly.MinValue));
        Property(x => x.Status, "STATUS").Options(("A", StatusOs.Aberta), ("F", StatusOs.Fechada));
        Joined(x => x.NomeParceiro, "Parceiro", "NOMEPARC");
        Presentation(x => x.NomeUsuario, "Usuario_NOMEUSU");
    }
}

/// <summary>
/// O mesmo <see cref="Produto"/>, sem anotações, para comparar mapa fluente e atributos.
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
/// AD_TESTE vista por um modelo de domínio sem anotações: <see cref="Quando"/> é gravado em duas colunas
/// (AD_DATA + HORA) e recomposto na leitura.
/// </summary>
public class TesteDominio
{
    public int Id { get; set; }
    public string? Texto { get; set; }
    public DateTime? Quando { get; set; }
    public Situacao Situacao { get; set; }
    public bool? Ativo { get; set; }
}

public enum Situacao { Ativa, Inativa, Pendente }

public sealed class TesteDominioMap : SankhyaEntityMap<TesteDominio>
{
    public TesteDominioMap()
    {
        Entity("AD_TESTE");
        Key(x => x.Id, "ID");
        Property(x => x.Texto, "TEXTO");
        Computed(x => x.Quando)
            .Writes("AD_DATA", SankhyaType.Date)
            .Writes("HORA", SankhyaType.Time)
            .Reads<DateOnly?, TimeOnly?>("AD_DATA", "HORA", (d, h) => d?.ToDateTime(h ?? TimeOnly.MinValue));
        Property(x => x.Situacao, "SITUACAO").Options(("A", Situacao.Ativa), ("I", Situacao.Inativa), ("P", Situacao.Pendente));
        Property(x => x.Ativo, "ATIVO");
    }
}
