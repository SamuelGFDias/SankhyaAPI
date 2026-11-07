using System.Xml.Serialization;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Services;

namespace SankhyaAPI.Client.Tests.Support;

/// <summary>
/// Usuário do Sankhya (tabela TSIUSU, entidade "Usuario"). O CODUSU 0 (SUP) existe em toda instalação,
/// por isso serve de alvo para testes de leitura contra uma base real.
/// </summary>
public class Usuario : SankhyaModelBase
{
    [Key("CODUSU")] public NullableState<int> CodUsu { get; set; }
    [XmlElement("NOMEUSU")] public NullableState<string> NomeUsu { get; set; }
}

/// <summary>
/// Modelo com chave auto-enumerada, para exercitar insert sem chave e update com chave.
/// </summary>
public class Produto : SankhyaModelBase
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
public class AdTeste : SankhyaModelBase
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

public class AdTesteService(string baseUrl, string userName, string password)
    : BaseService<AdTeste>(baseUrl, userName, password, "AD_TESTE");

/// <summary>
/// Cidade (TSICID, entidade "Cidade"). Na homologação passa de 10 mil linhas: serve para testar limites só lendo.
/// </summary>
public class Cidade : SankhyaModelBase
{
    [Key("CODCID")] public NullableState<int> CodCid { get; set; }
    [XmlElement("NOMECID")] public NullableState<string> NomeCid { get; set; }
}

public class CidadeLinha
{
    [XmlElement("CODCID")] public int? CodCid { get; set; }
}

public class CidadeService(string baseUrl, string userName, string password)
    : BaseService<Cidade>(baseUrl, userName, password, "Cidade");

public class UsuarioService(string baseUrl, string userName, string password)
    : BaseService<Usuario>(baseUrl, userName, password, "Usuario");

public class ProdutoService(string baseUrl, string userName, string password)
    : BaseService<Produto>(baseUrl, userName, password, "Produto");
