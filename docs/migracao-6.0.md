# Migração 5.x → 6.0

Este guia leva o código da 5.x (a 5.2.0, último release da linha 5.x) para a 6.0.

A 6.0 **quebra a API e não tem camada de compatibilidade**: o código 5.x não compila contra a 6.0 sem mudanças. Cada funcionalidade tem o código 5.x e o equivalente 6.0 lado a lado. As seções seguem a tabela F1–F18 da [proposta 6.0](https://github.com/SamuelGFDias/SankhyaAPI/blob/main/docs/proposta-6.0.md). O detalhe de cada mudança está no [CHANGELOG](https://github.com/SamuelGFDias/SankhyaAPI/blob/main/CHANGELOG.md).

Os alvos de framework mudaram: a 5.x tinha `net8.0`; a 6.0 tem `net8.0` e `net10.0`.

## Antes de começar

### Namespaces que mudaram

| 5.x | 6.0 |
|---|---|
| `SankhyaAPI.Client.Services` (`BaseService<T>`, `SessionService`, `RelatorioService`) | removidos (ver [O que saiu](#o-que-saiu)) |
| `SankhyaAPI.Client.Envelopes` (`Parameter`, `SankhyaPage<T>`) | `SankhyaAPI.Client.Repositories` |
| `SankhyaAPI.Client.Envelopes` (`Parametro`) | `SankhyaAPI.Client.Reports` (`ReportParameter`) |
| `SankhyaAPI.Client.MetaData` (`NullableState<T>`) | `SankhyaAPI.Client.Mapping` |
| `SankhyaAPI.Client.Extensions` (`KeyAttribute`, `EPropertyState`, `NullableStateConverterFactory`) | `SankhyaAPI.Client.Mapping` (`KeyAttribute`, `EPropertyState`, `NullableStateJsonConverterFactory`) |
| `SankhyaAPI.Client.MetaData` (`EServiceNames`) | removido; use `SankhyaAPI.Client.Transport.SankhyaServices` |
| `SankhyaAPI.Client.Extensions` (`SankhyaModelBase`, `XmlSerialable`, `SankhyaApiExtensions`) | removidos |
| `SankhyaAPI.Client.Exceptions` | `SankhyaAPI.Client.Exceptions` (igual, com `SankhyaTruncatedResultException`) |
| — | `SankhyaAPI.Client` (`SankhyaClient`, `AddSankhya`), `.Configuration`, `.Session`, `.Sql`, `.Transport`, `.Diagnostics` |

### Modelos de exemplo

Os exemplos usam o mesmo produto nas duas versões. Na 5.x:

```csharp
// 5.x
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Services;
using System.Xml.Serialization;

public class ProdutoEntity : SankhyaModelBase
{
    [Key("CODPROD", true)] public NullableState<long> CodProd { get; set; }
    [XmlElement("DESCRPROD")] public NullableState<string> DescrProd { get; set; }
    [XmlElement("PRECO")] public NullableState<decimal> Preco { get; set; }
    [XmlElement("ATIVO")] public NullableState<bool> Ativo { get; set; }
}

public class ProdutoService(string url, string usuario, string senha)
    : BaseService<ProdutoEntity>(url, usuario, senha, "Produto") { }

public class ProdutoLinha
{
    [XmlElement("CODPROD")] public long? CodProd { get; set; }
    [XmlElement("DESCRPROD")] public string? DescrProd { get; set; }
}

var produtoService = new ProdutoService(url, usuario, senha);
```

Na 6.0, o mesmo produto é um POCO com mapa fluente:

```csharp
// 6.0
using SankhyaAPI.Client;
using SankhyaAPI.Client.Mapping;
using SankhyaAPI.Client.Repositories;
using SankhyaAPI.Client.Sql;
using System.Xml.Serialization;

public class Produto
{
    public long CodProd { get; set; }
    public string? DescrProd { get; set; }
    public decimal? Preco { get; set; }
    public bool? Ativo { get; set; }
}

public sealed class ProdutoMap : SankhyaEntityMap<Produto>
{
    public ProdutoMap()
    {
        Entity("Produto");
        Key(x => x.CodProd, "CODPROD").AutoNumbered();
        Property(x => x.DescrProd, "DESCRPROD");
        Property(x => x.Preco, "PRECO");
        Property(x => x.Ativo, "ATIVO");
    }
}

public class ProdutoLinha
{
    [XmlElement("CODPROD")] public long? CodProd { get; set; }
    [XmlElement("DESCRPROD")] public string? DescrProd { get; set; }
}
```

### Configuração e injeção de dependência

Na 5.x, a URL e as credenciais iam no construtor do serviço, e o registro de cada serviço era manual. `AddSankhyaClient` registrava, como `scoped`, os tipos do próprio pacote que implementam `ISankhyaApi` (`SessionService` e `RelatorioService`). Os construtores pedem URL e credenciais, então o contêiner não sabia criá-los.

```csharp
// 5.x
builder.Services.AddSankhyaClient();
builder.Services.AddScoped(_ => new ProdutoService(url, usuario, senha));
```

Na 6.0, as opções vêm da configuração, a senha vem do cofre ou do user-secrets, e o registro é feito pelo builder:

```csharp
// 6.0
builder.Services
    .AddSankhya(o => builder.Configuration.GetSection("Sankhya").Bind(o))
    .AddEntity<Produto, ProdutoMap>();
```

A `BaseUrl` da 6.0 tem que terminar em `/mge` e usar `https`, salvo `AllowHttp` para desenvolvimento. A 5.x não exigia nenhum dos dois.

Sem injeção de dependência, a 6.0 usa `SankhyaClient.Create(opcoes)`. Um mapa fluente precisa ser registrado antes de `Repository<T>()`, com `SankhyaMaps.Register<ProdutoMap>()`. Um modelo com atributos não precisa de registro.

## F1: Login e logout explícitos

Na 5.x, `LoginSankhya` e `LogoutSankhya` eram públicos, mas só o `RelatorioService` aproveitava a sessão aberta. As operações do `BaseService` faziam login e logout por conta própria.

```csharp
// 5.x
await produtoService.LoginSankhya(usuario, senha);
// ...
await produtoService.LogoutSankhya();
```

```csharp
// 6.0
await sankhya.Session.LoginAsync(ct);   // as credenciais vêm da configuração
// as operações feitas aqui usam a mesma sessão, uma por vez
await sankhya.Session.LogoutAsync(ct);
```

`sankhya` é um `SankhyaClient`. Com injeção de dependência, injete `ISankhyaSession`. A chamada não recebe usuário nem senha.

## F2: Sessão por operação

Na 5.x era o único comportamento: cada operação fazia login, executava e fazia logout. Não havia nada a chamar.

```csharp
// 5.x
var produtos = await produtoService.FindAsync("this.ATIVO = 'S'");   // login, consulta e logout
```

Na 6.0, o padrão é `Shared`, que reaproveita sessões. Para o comportamento da 5.x, escolha `PerOperation`:

```csharp
// 6.0
var opcoes = new SankhyaClientOptions
{
    BaseUrl = url,
    Usuario = usuario,
    Senha = senha,
    SessionMode = SessionMode.PerOperation,
};
```

Na configuração, a mesma opção é `"SessionMode": "PerOperation"`.

## F3: Busca por expressão crua

```csharp
// 5.x
List<ProdutoEntity> produtos = await produtoService.FindAsync("this.CODPROD = 1");
```

```csharp
// 6.0: o método do repositório continua existindo
List<Produto> produtos = await repo.FindAsync("this.CODPROD = 1");

// 6.0: ou a consulta fluente, que aceita valores separados da expressão
List<Produto> produtos2 = await repo.Query().Where("this.CODPROD = ?", 1L).ToListAsync();
```

O pacote não valida a expressão. Não concatene valores vindos do usuário nela.

## F4: Busca parametrizada com ordenação

```csharp
// 5.x
string padrao = busca + "%";
List<ProdutoEntity> produtos = await produtoService.FindAsync(
    "this.DESCRPROD LIKE ? AND this.ATIVO = ?",
    [Parameter.Texto(padrao), Parameter.Texto("S")],
    "this.CODPROD");
```

```csharp
// 6.0: o mesmo método, com o namespace novo
string padrao = busca + "%";
List<Produto> produtos = await repo.FindAsync(
    "this.DESCRPROD LIKE ? AND this.ATIVO = ?",
    [Parameter.Texto(padrao), Parameter.Texto("S")],
    "this.CODPROD");

// 6.0, recomendado: a ordenação vem da propriedade mapeada, não de texto
List<Produto> produtos2 = await repo.Query()
    .Where("this.DESCRPROD LIKE ? AND this.ATIVO = ?", busca + "%", "S")
    .OrderBy(x => x.CodProd)
    .ToListAsync();
```

A ordenação deixou de ser texto livre na consulta fluente: `OrderBy` recebe uma expressão sobre a propriedade e usa a coluna do mapa.

## F5: Paginação

```csharp
// 5.x
int pagina = 0;
SankhyaPage<ProdutoEntity> resultado;
do
{
    resultado = await produtoService.FindPageAsync("this.ATIVO = ?", [Parameter.Texto("S")], pagina++, "this.CODPROD");
    Processar(resultado.Items);   // List<T>
} while (resultado.HasMoreResult);
```

```csharp
// 6.0: o mesmo método. Items agora é IReadOnlyList<T>; use .ToList() se Processar pedir List<T>.
int pagina = 0;
SankhyaPage<Produto> resultado;
do
{
    resultado = await repo.FindPageAsync("this.ATIVO = ?", [Parameter.Texto("S")], pagina++, "this.CODPROD");
    Processar(resultado.Items);
} while (resultado.HasMoreResult);

// 6.0, novo: percorre tudo sem carregar tudo em memória
await foreach (Produto p in repo.Query()
                   .Where("this.ATIVO = ?", "S")
                   .OrderBy(x => x.CodProd)
                   .StreamAsync(ct))
{
    // ...
}
```

## F6: Insert

```csharp
// 5.x
var novos = new List<ProdutoEntity> { new() { DescrProd = "Produto A" } };
List<ProdutoEntity> gravados = await produtoService.CreateManyAsync(novos);
```

```csharp
// 6.0: mesmos nomes. A chave gerada é preenchida no próprio objeto.
var novos = new List<Produto> { new() { DescrProd = "Produto A" } };
List<Produto> gravados = await repo.CreateManyAsync(novos);
// novos[0].CodProd recebe a chave gerada
```

Com POCO, todo campo de tipo de valor não anulável vai no insert, mesmo com o valor padrão. Para não enviar um campo, declare-o com tipo anulável.

## F7: Update parcial

Na 5.x, o update mandava só as propriedades `NullableState<T>` que estavam em `Set` ou `Clear`:

```csharp
// 5.x
var produto = new ProdutoEntity { CodProd = 10, DescrProd = "Parafuso M8" };   // Preco fica UnSet: não vai
produto.Preco = NullableState<decimal>.Clear();                                // vai vazio: apaga no servidor
await produtoService.UpdateAsync(produto);
```

A 6.0 tem três formas, todas com o mesmo envelope.

```csharp
// 6.0, 1: campos explícitos (padrão)
await repo.UpdateAsync(
    new Produto { CodProd = 10, DescrProd = "Parafuso M8" },
    u => u.Set(x => x.DescrProd).Clear(x => x.Preco));
```

```csharp
// 6.0, 2: rastreamento por cópia
Produto? atual = await repo.GetAsync([10L]);
if (atual is null) return;

Produto produto = repo.Track(atual);
produto.DescrProd = "Parafuso M8 zincado";
produto.Preco = null;              // null vira Clear
await repo.UpdateAsync(produto);   // manda só o que mudou
```

```csharp
// 6.0, 3: NullableState<T> no modelo (imutável)
[SankhyaEntity("Produto")]
public class ProdutoAtualizavel
{
    [Key("CODPROD", true)] public NullableState<long> CodProd { get; set; }
    [XmlElement("DESCRPROD")] public NullableState<string> DescrProd { get; set; }
    [XmlElement("PRECO")] public NullableState<decimal> Preco { get; set; }
}

var atualizavel = new ProdutoAtualizavel { CodProd = 10, DescrProd = "Parafuso M8" };
atualizavel.Preco = NullableState<decimal>.Clear();
await repo2.UpdateAsync(atualizavel);   // repo2 é ISankhyaRepository<ProdutoAtualizavel>
```

`UpdateAllAsync` manda todos os campos mapeados, e valor `null` é apagado. Isso sobrescreve o que outros tiverem alterado no servidor. Um `UpdateAsync(entidade)` sem `Track` e sem `NullableState<T>` lança `InvalidOperationException`.

## F8: Delete

```csharp
// 5.x
await produtoService.DeleteAsync(produto);
await produtoService.DeleteAsync(listaDeProdutos);   // sobrecarga com List<T>
```

```csharp
// 6.0
await repo.DeleteAsync(produto);
await repo.DeleteManyAsync(listaDeProdutos);         // antes DeleteAsync(List<T>)
```

## F9: Chave simples, composta e auto-numerada

Na 5.x, a chave é um atributo com `NullableState`:

```csharp
// 5.x
public class ItemNotaEntity : SankhyaModelBase
{
    [Key("NUNOTA")] public NullableState<long> NuNota { get; set; }
    [Key("SEQUENCIA", true)] public NullableState<int> Sequencia { get; set; }
    [XmlElement("QTDNEG")] public NullableState<decimal> Quantidade { get; set; }
}
```

Na 6.0, o atributo continua, sem `NullableState`, ou o mapa fluente declara a chave. Chave composta é uma chamada por campo, e só um pode ser auto-numerado:

```csharp
// 6.0, por atributos
[SankhyaEntity("ItemNota")]
public class ItemNota
{
    [Key("NUNOTA")] public long NuNota { get; set; }
    [Key("SEQUENCIA", true)] public int Sequencia { get; set; }
    [XmlElement("QTDNEG")] public decimal Quantidade { get; set; }
}

// 6.0, pelo mapa fluente
public sealed class ItemNotaMap : SankhyaEntityMap<ItemNota>
{
    public ItemNotaMap()
    {
        Entity("ItemNota");
        Key(x => x.NuNota, "NUNOTA");
        Key(x => x.Sequencia, "SEQUENCIA").AutoNumbered();
        Property(x => x.Quantidade, "QTDNEG");
    }
}
```

## F10: Validações

As regras de chave continuam. A 6.0 valida a mesma coisa, mas a regra de `NullableState<T>` obrigatório saiu, e a montagem do mapa passou a validar.

| Regra | 5.x | 6.0 |
|---|---|---|
| Chave obrigatória em update e delete | validada antes de enviar | a mesma regra; `ArgumentNullException` com o nome do campo |
| Chave auto-numerada preenchida no insert | recusada antes de enviar | `ArgumentException` |
| Chave sem auto-numeração vazia no insert | recusada antes de enviar | `ArgumentException` |
| Chave em estado `Clear` | recusada | `ArgumentException` |
| Propriedade que não é `NullableState<T>` | `SankhyaModelBase` recusa (`ArgumentException`) | a regra não existe: POCO é aceito |
| Mapa com dois campos auto-numerados na chave, ou coluna escrita duas vezes | não se aplica | `InvalidOperationException` ao usar o mapa; `SankhyaMaps.Validate<T>()` valida antes |

A 6.0 não traz analisadores de código: as validações são feitas em tempo de execução.

## F11: Conversão de tipos

A 5.x inferia o formato pelo tipo da propriedade, com os mesmos formatos da 6.0: `DateTime` em `dd/MM/yyyy HH:mm:ss`, `DateOnly` em `dd/MM/yyyy`, `TimeOnly` em HHMM, `bool` em `S`/`N`, e enum pelo `[XmlEnum]`.

```csharp
// 5.x
[XmlElement("HORA")] public NullableState<TimeOnly> Hora { get; set; }
[XmlElement("ATIVO")] public NullableState<bool> Ativo { get; set; }
```

```csharp
// 6.0: a inferência continua, e o tipo pode ser explícito no mapa
Property(x => x.Hora, "HORA", SankhyaType.Time);
Property(x => x.Ativo, "ATIVO", SankhyaType.YesNo);
```

A diferença está no que é extensível: na 6.0, `SankhyaType` é público, e um formato próprio entra por `ISankhyaConverter<T>`, com `Format(T)` (o texto a enviar, ou `null`) e `Parse(string)` (o valor a partir do texto).

## F12: SQL cru

```csharp
// 5.x
List<ProdutoLinha> linhas = await produtoService.QueryRawAsync<ProdutoLinha>(
    "SELECT CODPROD, DESCRPROD FROM TGFPRO WHERE ATIVO = 'S'");
List<Dictionary<string, object?>> dicionarios = await produtoService.QueryAsDictionaryAsync(
    "SELECT CODPROD, DESCRPROD FROM TGFPRO WHERE ATIVO = 'S'");
```

```csharp
// 6.0: o SQL vai pelo Sql do cliente
List<ProdutoLinha> linhas = await sankhya.Sql.QueryRawAsync<ProdutoLinha>(
    SankhyaSql.Raw("SELECT CODPROD, DESCRPROD FROM TGFPRO WHERE ATIVO = 'S'"));

string ativo = "S";
List<ProdutoLinha> filtradas = await sankhya.Sql.QueryRawAsync<ProdutoLinha>(
    $"SELECT CODPROD, DESCRPROD FROM TGFPRO WHERE ATIVO = {ativo}");

List<Dictionary<string, object?>> dicionarios2 = await sankhya.Sql.QueryAsDictionaryAsync(
    SankhyaSql.Raw("SELECT CODPROD, DESCRPROD FROM TGFPRO WHERE ATIVO = 'S'"));
```

Na 6.0, a string interpolada escapa cada valor pelo tipo. Uma string comum também é aceita, mas nesse caso o SQL é usado como está. Não guarde a interpolação numa variável `string` antes de passá-la: a variável já é texto, e o valor sai sem escape.

## F13: Corte de 5000 linhas

```csharp
// 5.x: o corte lança SankhyaException
try
{
    var linhas = await produtoService.QueryRawAsync<ProdutoLinha>(sql);
}
catch (SankhyaException)
{
    // pagine com ORDER BY ... OFFSET ... FETCH NEXT
}
```

```csharp
// 6.0: o corte lança a subclasse SankhyaTruncatedResultException
try
{
    var linhas = await sankhya.Sql.QueryRawAsync<ProdutoLinha>(sql);
}
catch (SankhyaTruncatedResultException)
{
    // ...
}

// 6.0: ou percorre o resultado inteiro, paginado pelo pacote
await foreach (ProdutoLinha linha in sankhya.Sql.QueryPagedAsync<ProdutoLinha>(
                   SankhyaSql.Raw("SELECT CODPROD, DESCRPROD FROM TGFPRO WHERE ATIVO = 'S'"),
                   "CODPROD"))
{
    // ...
}
```

No `QueryPagedAsync`, o SQL não pode ter `ORDER BY` próprio, e a ordenação vai no argumento `orderBy`.

## F14: Relatórios

```csharp
// 5.x: duas chamadas; a sessão aberta em GerarRelatorio só é encerrada em ObterRelatorio
var relatorios = new RelatorioService(url, usuario, senha);
string chave = await relatorios.GerarRelatorio(42, [new Parametro("java.math.BigDecimal", "CODEMP", "1")]);
Stream arquivo = await relatorios.ObterRelatorio(chave);
```

```csharp
// 6.0: uma chamada, na mesma sessão. Em PerOperation ela é encerrada ao fim; em Shared volta ao pool
await using SankhyaReportFile relatorio = await sankhya.Reports.GenerateAsync(
    42,
    [new ReportParameter("CODEMP", "java.math.BigDecimal", "1")],
    ct);
await relatorio.Content.CopyToAsync(destino, ct);
```

Atenção à ordem dos argumentos: na 5.x, `Parametro` recebia `(classe, nome, valor)`; na 6.0, `ReportParameter` recebe `(Name, Class, Value)`, ou seja, nome primeiro.

## F15: Erros

Os tipos continuam os mesmos. A 6.0 acrescenta `SankhyaTruncatedResultException`, subclasse de `SankhyaException`.

```csharp
// 5.x e 6.0
try
{
    // ...
}
catch (SankhyaSessionExpiredException)
{
    // 5.x: a sessão expirou. 6.0, no modo Shared: o pacote já renovou e repetiu uma vez.
}
catch (SankhyaException ex)
{
    string? status = ex.Status;
    string? servico = ex.ServiceName;
    string? codigo = ex.TsErrorCode;
}
```

Na 6.0, falha de transporte que estoura o tempo também chega como `HttpRequestException`, com a mensagem "Tempo esgotado na chamada ao Sankhya". Erro de conversão de campo chega como `FormatException` sem o valor, que fica em `ex.Data["Valor"]`.

## F16: NullableState em JSON

```csharp
// 5.x: extensão que usa camelCase e indentação
string json = produtoEntity.JsonSerialize();
```

```csharp
// 6.0: o conversor já vem no tipo NullableState<T>
string json = JsonSerializer.Serialize(atualizavel);
```

Na 6.0 o JSON usa os nomes das propriedades como estão. Se o consumidor depende de camelCase, passe `new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }`. O valor de `NullableState<T>` continua sendo o valor puro, e `null` quando não há valor.

## F17: Campos de apresentação

Na 5.x, todo loadRecords pedia `includePresentationFields="S"`, e o campo de apresentação era uma propriedade `[XmlElement]` com o nome da coluna que vem na resposta. Isso foi inferido do código, sem teste em homologação.

```csharp
// 5.x (inferido do código)
public class UsuarioEntity : SankhyaModelBase
{
    [Key("CODUSU", true)] public NullableState<int> CodUsu { get; set; }
    [XmlElement("GrupoUsuario_NOMEGRUPO")] public NullableState<string> NomeGrupo { get; set; }
}
```

Na 6.0, o campo é de apresentação, só leitura, e o pacote pede `includePresentationFields` só para os mapas que têm um:

```csharp
// 6.0, pelo mapa fluente
Presentation(x => x.NomeGrupo, "GrupoUsuario_NOMEGRUPO");

// 6.0, pelo atributo
[SankhyaPresentation("GrupoUsuario_NOMEGRUPO")] public string? NomeGrupo { get; set; }
```

Um campo `[XmlElement]` com esse nome, na 6.0, passa a ser pedido e gravado como campo comum. Migre para `[SankhyaPresentation]`.

## F18: Modelos com atributos

Os atributos continuam aceitos. O que sai é a base `SankhyaModelBase`, o `NullableState` passa a ser opcional, e o nome da entidade vai para o atributo `[SankhyaEntity]`, ou para o registro.

```csharp
// 5.x
public class ProdutoEntity : SankhyaModelBase
{
    [Key("CODPROD", true)] public NullableState<long> CodProd { get; set; }
    [XmlElement("DESCRPROD")] public NullableState<string> DescrProd { get; set; }
}

public class ProdutoService(string url, string usuario, string senha)
    : BaseService<ProdutoEntity>(url, usuario, senha, "Produto") { }
```

```csharp
// 6.0
[SankhyaEntity("Produto")]
public class ProdutoAtributos
{
    [Key("CODPROD", true)] public long CodProd { get; set; }
    [XmlElement("DESCRPROD")] public string? DescrProd { get; set; }
}

// registro: AddRepository<ProdutoAtributos>() no builder do AddSankhya,
// e a injeção de ISankhyaRepository<ProdutoAtributos> no consumidor
```

## O que saiu

Itens da 5.x que não existem na 6.0, e o que usar no lugar:

| 5.x | Na 6.0 |
|---|---|
| `BaseService<T>` | `ISankhyaRepository<T>` (`sankhya.Repository<T>()` ou injeção) |
| `SessionService` (`LoginSankhya`, `LogoutSankhya`) | `ISankhyaSession` (`sankhya.Session`) |
| `RelatorioService` (`GerarRelatorio`, `ObterRelatorio`) | `ISankhyaReports` (`sankhya.Reports.GenerateAsync`) |
| Construtores `(baseUrl, usuario, senha)` e `(…, entidade)` | opções na configuração (`AddSankhya` ou `SankhyaClientOptions`) e `[SankhyaEntity]`, ou o nome passado em `Repository<T>("…")` |
| `AddSankhyaClient()` | `AddSankhya(...)`, com `AddEntity` ou `AddRepository` |
| `XmlSerialable` | sem substituto: mapeie com `[XmlElement]`, `[Key]` ou `SankhyaEntityMap<T>` |
| `IProxysable<TResponse, TProxy>` (obsoleto na 5.x) | sem substituto: a classe de domínio é o próprio modelo |
| `SankhyaModelBase`, `IModelBase`, `INullableState` | sem base: o modelo é um POCO |
| Envelopes públicos (`ServiceRequest`, `ServiceResponse`, `DataSet`, `DataRow`, `Criteria`, `ResponseBody`, `Entities`, `Metadata`, `SankhyaEntity`, `Field`, `FieldSet`, `Chave`, `Relatorio`, `Parametro`, `SankhyaObjectKey` e outros) | internos. Para chamada crua, use `ISankhyaTransport` |
| `LoginEntity`, `ResponseEntity` | `SankhyaSessionInfo`, devolvido por `Session.LoginAsync` |
| Helpers públicos (`ObjectUtilsMethods`, `NullableState.GetUnderlyingType`) | `SankhyaType` e `ISankhyaConverter<T>` |
| `SerializerServiceExtension` (`XmlSerializer`, `JsonSerialize`) | `System.Text.Json` direto |
| `ISankhyaServiceClient`, `SankhyaApiClientFactory`, `DefaultClientService` (Refit) | `ISankhyaTransport`. Sai o Refit |
| `EServiceNames` | `SankhyaServices` (constantes dos nomes dos serviços) |
| `ISankhyaApi` (interface vazia de marcação) | sem substituto |

## Mudanças de comportamento

Estas mudanças não aparecem como erro de compilação. Revise-as depois de compilar.

1. **Sessão compartilhada por padrão.** O modo `Shared` mantém até `MaxSessions` (4) sessões abertas e as reaproveita. A 5.x fazia login e logout em cada operação. Para o comportamento antigo, use `PerOperation`. O registro no contêiner é `singleton`, então as sessões valem para a aplicação inteira. O `AddSankhyaClient` da 5.x registrava os tipos do pacote como `scoped`.
2. **`NullableState<T>` imutável.** O construtor público e os setters de `Value` e `State` saíram. `HasValue` passou a exigir `State == Set`; na 5.x bastava `Value` não ser `null`. Código que escreve `.Value` ou `.State` não compila. Use `=`, ou `Set`, `Clear` e `Unset`.
3. **Update sem campos.** `UpdateAsync(entidade)` sem `Track` e sem `NullableState<T>` no modelo lança `InvalidOperationException`. Informe os campos, rastreie o objeto ou use `UpdateAllAsync`.
4. **POCO no insert.** Campo de tipo de valor não anulável vai sempre. Use tipo anulável para não enviar.
5. **Nomes.** `DeleteAsync(List<T>)` virou `DeleteManyAsync(IEnumerable<T>)`. `CreateManyAsync` e `UpdateManyAsync` aceitam `IEnumerable<T>` e devolvem `List<T>`.
6. **Timeout.** A 5.x esperava até 1 dia por chamada. A 6.0 usa 100 s por tentativa, configurável em `Timeout`.
7. **Novas tentativas.** A 6.0 repete falha transitória em operações idempotentes (login, loadRecords e executeQuery), até `MaxRetries` vezes (padrão 2). Escritas não se repetem por falha de transporte. A 5.x não repetia.
8. **`SankhyaPage<T>.Items`** é `IReadOnlyList<T>`. Na 5.x era `List<T>`.
9. **Dicionário do SQL cru.** Os valores são `object?` (na 5.x, `dynamic?`), e os inteiros voltam como `long`.
10. **Campos de apresentação.** O pacote pede `includePresentationFields` só quando o mapa tem um campo de apresentação. Na 5.x, sempre.
11. **Sessão expirada.** No modo `Shared`, a 6.0 repete a operação uma vez antes de lançar `SankhyaSessionExpiredException`.
12. **Relatórios.** `GenerateAsync` gera e baixa numa chamada, na mesma sessão. Em `PerOperation` a sessão é encerrada ao fim; em `Shared` ela volta ao pool, como as demais operações. O arquivo fica em memória (`Content`), e o descarte libera a memória.
13. **Validação da configuração.** `BaseUrl` terminada em `/mge` e `https` (salvo `AllowHttp`) são exigidos na inicialização.
14. **Cancelamento.** Todos os métodos públicos aceitam `CancellationToken` (opcional, então o código 5.x continua compilando onde não usa o parâmetro).
15. **Observabilidade.** Há `ActivitySource`, `Meter` e logs estruturados com o nome `SankhyaAPI.Client`. Não há equivalente na 5.x.

## Checklist

- [ ] Trocar `BaseService`, `SessionService` e `RelatorioService` por `ISankhyaRepository<T>`, `ISankhyaSession` e `ISankhyaReports`.
- [ ] Mover URL, usuário e senha para `SankhyaClientOptions` na configuração. A senha vai para o cofre ou o user-secrets.
- [ ] Conferir que a `BaseUrl` termina em `/mge`.
- [ ] Trocar `AddSankhyaClient()` por `AddSankhya(...)`, com `AddEntity` ou `AddRepository`.
- [ ] Trocar `SankhyaModelBase` por POCO, ou manter `NullableState<T>` onde houver update parcial.
- [ ] Trocar atribuições em `.Value` e `.State` de `NullableState<T>` por `=`, `Set`, `Clear` ou `Unset`.
- [ ] Rever cada update: informar os campos, usar `Track` ou `UpdateAllAsync`.
- [ ] Renomear `DeleteAsync(lista)` para `DeleteManyAsync`.
- [ ] Trocar `Envelopes.Parameter` por `Repositories.Parameter`, e `Parametro` por `ReportParameter`, conferindo a ordem dos argumentos.
- [ ] Onde o código pede `List<T>` de `SankhyaPage<T>.Items`, use `.ToList()`.
- [ ] Trocar `dynamic?` por `object?` no dicionário do SQL cru, e rever o tratamento de inteiros (agora `long`).
- [ ] Trocar campos de apresentação `[XmlElement]` por `[SankhyaPresentation]` ou `Presentation`.
- [ ] Rever o timeout: 100 s por tentativa, no lugar de 1 dia.
- [ ] Revisar o SQL cru: valores pela interpolação de `SankhyaSql`, ou `SankhyaSql.Raw` para SQL fixo.
- [ ] Rodar os testes de integração contra a homologação.
