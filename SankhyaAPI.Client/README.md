# 👜 SankhyaAPI.Client

![GitHub issues](https://img.shields.io/github/issues/SamuelGFDias/SankhyaAPI)
![GitHub forks](https://img.shields.io/github/forks/SamuelGFDias/SankhyaAPI)
![GitHub stars](https://img.shields.io/github/stars/SamuelGFDias/SankhyaAPI)
![GitHub license](https://img.shields.io/github/license/SamuelGFDias/SankhyaAPI)
![GitHub Languages](https://img.shields.io/github/languages/top/SamuelGFDias/SankhyaAPI)
![Nuget](https://img.shields.io/nuget/v/SankhyaAPI.Client)

Biblioteca .NET para integração com o ERP Sankhya: CRUD de entidades, consultas, SQL cru, relatórios, sessões e telemetria.

> **A 6.0 quebra a API da 5.x e não tem camada de compatibilidade.** Para migrar, siga o [guia de migração 5.x → 6.0](https://github.com/SamuelGFDias/SankhyaAPI/blob/main/docs/migracao-6.0.md). O histórico de mudanças está no [CHANGELOG](https://github.com/SamuelGFDias/SankhyaAPI/blob/main/CHANGELOG.md).

## Índice

- [Instalação](#instalação)
- [Configuração](#configuração)
- [Sessão](#sessão)
- [Mapeamento](#mapeamento)
- [CRUD](#crud)
- [Atualização parcial](#atualização-parcial)
- [Consultas](#consultas)
- [SQL cru](#sql-cru)
- [Relatórios](#relatórios)
- [Erros](#erros)
- [Observabilidade](#observabilidade)
- [Resiliência](#resiliência)
- [Source generator e AOT](#source-generator-e-aot)
- [Testes](#testes)
- [Skill para agentes de IA](#skill-para-agentes-de-ia)
- [Licença](#licença)

## Instalação

```bash
dotnet add package SankhyaAPI.Client --version 6.0.0
```

O pacote tem como alvo `net8.0` e `net10.0`.

## Configuração

O pacote foi feito para injeção de dependência, mas também funciona sem ela.

### Com injeção de dependência

Em `appsettings.json`, sem a senha:

```json
{
  "Sankhya": {
    "BaseUrl": "https://erp.empresa.com.br/mge",
    "Usuario": "integracao",
    "SessionMode": "Shared",
    "MaxSessions": 4
  }
}
```

A senha não fica no arquivo. Em desenvolvimento, use user-secrets; em produção, o cofre de segredos ou variável de ambiente:

```bash
dotnet user-secrets set "Sankhya:Senha" "<senha>"
```

No `Program.cs`:

```csharp
using SankhyaAPI.Client;

builder.Services
    .AddSankhya(o => builder.Configuration.GetSection("Sankhya").Bind(o))
    .AddEntity<Produto, ProdutoMap>()       // mapa fluente
    .AddRepository<Parceiro>("Parceiro");   // mapa por atributos
```

O `Bind` vem de `Microsoft.Extensions.Configuration.Binder`, que o host do ASP.NET Core e o Generic Host já trazem. Quem consome recebe o repositório pela injeção:

```csharp
public class ProdutoService(ISankhyaRepository<Produto> produtos)
{
    public Task<Produto?> ObterAsync(long codigo, CancellationToken ct = default) =>
        produtos.GetAsync([codigo], ct);
}
```

`AddSankhya` valida as opções na inicialização (`ValidateOnStart`): uma opção inválida impede o host de subir.

### Sem injeção de dependência

```csharp
using SankhyaAPI.Client;

await using SankhyaClient sankhya = SankhyaClient.Create(new SankhyaClientOptions
{
    BaseUrl = "https://erp.empresa.com.br/mge",
    Usuario = "integracao",
    Senha = senha,   // de cofre ou user-secrets, nunca fixa no código
});

SankhyaMaps.Register<ProdutoMap>();
ISankhyaRepository<Produto> produtos = sankhya.Repository<Produto>();
```

`SankhyaClient.Create` aceita um `ILoggerFactory` opcional. `SankhyaMaps.Register` registra o mapa no processo inteiro: vale para todos os clientes.

### Opções

| Opção | Padrão | Descrição |
|---|---|---|
| `BaseUrl` | obrigatória | URL absoluta do Sankhya, terminada em `/mge`. Exemplo: `https://erp.empresa.com.br/mge`. |
| `Usuario` | obrigatória | Usuário de integração. |
| `Senha` | obrigatória | Senha do usuário. Leia de cofre ou user-secrets. |
| `SessionMode` | `Shared` | `Shared` ou `PerOperation` (ver [Sessão](#sessão)). |
| `MaxSessions` | `4` | Máximo de sessões abertas no modo `Shared`. Também é o máximo de chamadas em paralelo. |
| `Timeout` | 100 s | Tempo máximo de cada tentativa de chamada a um serviço. |
| `MaxRetries` | `2` | Novas tentativas em falha transitória, só em operações idempotentes (ver [Resiliência](#resiliência)). |
| `Dialect` | `SqlServer` | Banco por trás do Sankhya (`SqlServer` ou `Oracle`), usado para formatar valores no SQL cru. |
| `VerifyExistenceOnUpdate` | `true` | Confere se as chaves existem antes de cada update. O saveRecord do Sankhya **cria** o registro quando a chave não existe; com a opção ligada, o update lança `SankhyaNotFoundException`. Custa uma consulta por lote. |
| `AllowHttp` | `false` | Libera `http://`. Só para desenvolvimento: a senha e a sessão trafegam em texto puro. |

Opções inválidas lançam `OptionsValidationException` com a lista de problemas: `BaseUrl` absoluta e terminada em `/mge`, `https` (salvo `AllowHttp`), `Usuario` e `Senha` preenchidos, `Timeout` positivo, `MaxRetries` não negativo e `MaxSessions` de pelo menos 1.

## Sessão

O Sankhya recusa duas requisições simultâneas na mesma sessão, com a mensagem "O serviço foi cancelado por situação de concorrência". Por isso o pacote dá a cada operação uma sessão com exclusividade.

| Modo | Como funciona | Quando usar |
|---|---|---|
| `Shared` (padrão) | Login sob demanda. Até `MaxSessions` sessões ficam abertas e são reaproveitadas, uma operação por vez em cada uma. Chamadas além desse limite esperam. | Uso normal, com várias chamadas. |
| `PerOperation` | Login e logout em cada operação, como na 5.x. | Isolamento total, ou jobs que rodam de tempos em tempos. |

No modo `Shared`, quando o Sankhya responde que a sessão expirou (`status="3"`), o pacote abre outra sessão e repete a operação uma vez. Repetir uma escrita é seguro nesse caso: com a sessão expirada, o Sankhya não grava nem apaga nada.

As sessões compartilhadas são encerradas no `DisposeAsync` do `SankhyaClient`. Com injeção de dependência, elas são encerradas quando o contêiner é descartado de forma assíncrona (`await using` no `ServiceProvider`, ou o host da aplicação), porque o tipo implementa só `IAsyncDisposable`.

### Sessão explícita

Quando um fluxo precisa de várias chamadas na mesma sessão, abra-a explicitamente. O login usa as credenciais da configuração:

```csharp
SankhyaSessionInfo sessao = await sankhya.Session.LoginAsync(ct);

// Enquanto a sessão estiver aberta, todas as operações a usam, uma por vez.
List<Produto> ativos = await produtos.FindAsync("this.ATIVO = 'S'", ct);

await sankhya.Session.LogoutAsync(ct);
```

Com injeção de dependência, `ISankhyaSession` é singleton. Enquanto uma sessão explícita estiver aberta, todas as operações da aplicação passam por ela, uma por vez. Use esse fluxo em jobs e rotinas curtas, não em requisições web concorrentes.

### Chamada a um serviço que o pacote não cobre

`sankhya.Transport` expõe a chamada crua ao `service.sbr`. Ela precisa da sessão, então roda dentro de `Session.ExecuteAsync`. O corpo (`requestBody`) é montado pelo chamador:

```csharp
using System.Xml.Linq;
using SankhyaAPI.Client.Transport;

List<XElement> corpo = [new XElement("dataSet", new XAttribute("rootEntity", "Produto"))];

SankhyaXmlResponse resposta = await sankhya.Session.ExecuteAsync(
    (id, token) => sankhya.Transport.SendXmlAsync(SankhyaServices.LoadRecords, corpo, id, idempotent: true, token),
    ct);
```

## Mapeamento

Há dois jeitos de dizer como um modelo vira uma entidade do Sankhya. O mapa fluente mantém o modelo limpo, sem atributos, e é o recomendado. Os atributos servem para casos simples.

### Mapa fluente

```csharp
using SankhyaAPI.Client.Mapping;

public class Produto
{
    public long CodProd { get; set; }
    public string? DescrProd { get; set; }
    public decimal? Preco { get; set; }
    public DateTime? DtAlter { get; set; }
    public bool? Ativo { get; set; }
    public string? NomeGrupo { get; set; }
}

public sealed class ProdutoMap : SankhyaEntityMap<Produto>
{
    public ProdutoMap()
    {
        Entity("Produto");
        Key(x => x.CodProd, "CODPROD").AutoNumbered();
        Property(x => x.DescrProd, "DESCRPROD");
        Property(x => x.Preco, "PRECO");
        Property(x => x.DtAlter, "DTALTER");
        Property(x => x.Ativo, "ATIVO");
        Presentation(x => x.NomeGrupo, "GrupoProduto_DESCRGRUPOPROD");   // campo de apresentação, só leitura
    }
}
```

Outros recursos do mapa, com um modelo de exemplo:

```csharp
public enum StatusOs { Aberta, Fechada }

public class OrdemServico
{
    public int Numero { get; set; }
    public string? Descricao { get; set; }
    public DateTime? DataExecucao { get; set; }
    public StatusOs Status { get; set; }
    public string? NomeParceiro { get; set; }
    public string? NomeUsuario { get; set; }
}

public sealed class OrdemServicoMap : SankhyaEntityMap<OrdemServico>
{
    public OrdemServicoMap()
    {
        Entity("OrdemServico");
        Key(x => x.Numero, "NUMOS").AutoNumbered();
        Property(x => x.Descricao, "DESCRICAO");
        Property(x => x.Status, "STATUS").Options(("A", StatusOs.Aberta), ("F", StatusOs.Fechada));
        Computed(x => x.DataExecucao)
            .Writes("DT_FINAL", SankhyaType.Date)
            .Writes("HR_FINAL", SankhyaType.Time)
            .Reads<DateOnly?, TimeOnly?>("DT_FINAL", "HR_FINAL", (d, h) => d?.ToDateTime(h ?? TimeOnly.MinValue));
        Joined(x => x.NomeParceiro, "Parceiro", "NOMEPARC");
        Presentation(x => x.NomeUsuario, "Usuario_NOMEUSU");
    }
}
```

| Método | O que faz |
|---|---|
| `Entity("Nome")` | Nome da entidade (instância) no Sankhya, por exemplo `Produto`. |
| `Key(x => x.Campo, "COLUNA")` | Campo da chave. Chave composta: chame uma vez por campo. `.AutoNumbered()` marca o campo que o servidor gera no insert. Só um campo por chave pode ser auto-numerado. |
| `Property(x => x.Campo, "COLUNA")` | Campo lido e escrito na coluna. Aceita um `SankhyaType` explícito, `.Options(("código", valor), ...)` para listas de opções, `.ReadOnly()` e um `ISankhyaConverter<T>` próprio. |
| `Computed(x => x.Campo)` | Campo do domínio que ocupa mais de uma coluna. `.Writes(coluna, tipo)` pode aparecer várias vezes. Para ler, `.Reads<T1>(coluna, compor)` ou `.Reads<T1, T2>(coluna1, coluna2, compor)`. Sem `Reads`, o campo é só de escrita. |
| `Joined(x => x.Campo, "Caminho", "CAMPO")` | Campo de entidade ligada, só leitura. Pede `<entity path="Caminho">` e lê a coluna `Caminho_CAMPO`. |
| `Presentation(x => x.Campo, "Coluna")` | Campo de apresentação que o Sankhya acrescenta, só leitura. Só os mapas com campo de apresentação pedem `includePresentationFields`. |

Coluna escrita por dois campos é erro, e uma chave precisa corresponder a exatamente uma coluna. O mapa é validado quando é usado pela primeira vez. Para validar antes, na inicialização, chame `SankhyaMaps.Validate<T>()`.

Quando o tipo não é dado com `SankhyaType`, ele é inferido pelo tipo da propriedade (também dentro de `Nullable<>` e `NullableState<>`):

| Tipo .NET | Sankhya | Formato no XML |
|---|---|---|
| `string`, `char`, `Guid` | Texto (`S`) | sem espaços nas pontas |
| `int`, `long`, `short`, `byte` e equivalentes sem sinal | Inteiro (`I`) | número |
| `decimal`, `double`, `float` | Decimal (`F`) | ponto decimal, cultura invariante |
| `DateOnly` | Data (`D`) | `dd/MM/yyyy` |
| `DateTime`, `DateTimeOffset` | Data e hora (`H`) | `dd/MM/yyyy HH:mm:ss` |
| `TimeOnly` | Hora (`T`) | HHMM, como número (`smallint`) |
| `bool` | Sim/não | `S` ou `N` |
| `enum` | Lista de opções | código do `[XmlEnum]`, ou do `.Options(...)` |

Texto vazio vira `null` na leitura (sim/não vira `false`). Erro de conversão lança `FormatException` sem o valor na mensagem (ver [Erros](#erros)).

Para um formato que a tabela não cobre, implemente `ISankhyaConverter<T>`: `Format(T)` devolve o texto a enviar (ou `null`), e `Parse(string)` devolve o valor a partir de um texto que nunca chega vazio.

### Mapa por atributos

```csharp
using System.Xml.Serialization;
using SankhyaAPI.Client.Mapping;

[SankhyaEntity("ItemNota")]
public class ItemNota
{
    [Key("NUNOTA")] public long NuNota { get; set; }
    [Key("SEQUENCIA", true)] public int Sequencia { get; set; }
    [XmlElement("QTDNEG")] public decimal Quantidade { get; set; }
    [SankhyaPresentation("Produto_DESCRPROD")] public string? DescrProduto { get; set; }
}
```

Propriedades sem `[XmlElement]`, `[Key]` ou `[SankhyaPresentation]` não entram no mapa. Campo calculado, entidade ligada e opções exigem o mapa fluente. Propriedades `NullableState<T>` também funcionam nos dois mapas (ver [Atualização parcial](#atualização-parcial)).

## CRUD

Os métodos ficam em `ISankhyaRepository<T>`, obtido com `sankhya.Repository<T>()` ou pela injeção:

```csharp
Produto novo = new() { DescrProd = "Parafuso", Preco = 1.5m, Ativo = true };
await produtos.CreateAsync(novo);   // novo.CodProd recebe a chave gerada pelo Sankhya

List<Produto> lote = [new() { DescrProd = "Porca" }, new() { DescrProd = "Arruela" }];
await produtos.CreateManyAsync(lote);

Produto? encontrado = await produtos.GetAsync([novo.CodProd]);

await produtos.DeleteAsync(novo);
await produtos.DeleteManyAsync(lote);
```

| Método | O que faz |
|---|---|
| `CreateAsync(entidade)`, `CreateManyAsync(entidades)` | Insere e devolve o que foi gravado. A chave gerada é preenchida no próprio objeto. |
| `GetAsync(object[] chave)` | Busca pela chave, com os valores na ordem dos campos da chave no mapa. Devolve `null` se não existir. |
| `FindAsync(expressao)` | Filtro sem parâmetros. Traz todos os registros que atendem, sem limite. Não concatene valores vindos do usuário. |
| `FindAsync(expressao, parametros, ordenacao)` | Filtro com `?` para cada valor, e ordenação opcional. |
| `FindPageAsync(expressao, parametros, pagina, ordenacao)` | Uma página de até 50 registros, começando em 0. |
| `UpdateAsync`, `UpdateManyAsync`, `UpdateAllAsync`, `Track` | Atualização (ver [Atualização parcial](#atualização-parcial)). |
| `DeleteAsync(entidade)`, `DeleteManyAsync(entidades)` | Exclui pela chave. |
| `Query()` | Consulta fluente (ver [Consultas](#consultas)). |

Filtro com parâmetros, em que os valores não viram SQL:

```csharp
using SankhyaAPI.Client.Repositories;

string padrao = busca + "%";
List<Produto> produtosDoGrupo = await produtos.FindAsync(
    "this.DESCRPROD LIKE ? AND this.ATIVO = ?",
    [Parameter.Texto(padrao), Parameter.Texto("S")],
    "this.CODPROD");
```

Regras de chave, verificadas antes de enviar: no insert, a chave sem auto-numeração precisa estar preenchida, e a auto-numerada precisa estar com o valor padrão (zero, ou `null` em tipo anulável), porque o servidor a gera. Update e delete exigem a chave.

Em modelo POCO, todo campo de tipo de valor não anulável (`int`, `bool`, `DateTime` sem `?`) vai no insert, mesmo com o valor padrão. Para não enviar um campo, use o tipo anulável ou `NullableState<T>`.

## Atualização parcial

Um update manda só o que foi informado: a chave e os campos escolhidos. Campo apagado vai vazio. Há três formas, e as três mandam o mesmo tipo de envelope.

**1. Campos explícitos (padrão).** Você diz quais campos mandar:

```csharp
await produtos.UpdateAsync(
    new Produto { CodProd = 10, DescrProd = "Parafuso M8" },
    u => u.Set(x => x.DescrProd).Clear(x => x.Preco));
```

`Set` manda o valor atual do campo. Se o valor for `null`, nada é enviado para o campo; para apagar, use `Clear`.

**2. Rastreamento (`Track`).** O pacote guarda uma cópia do estado lido, e o update manda só o que mudou. Um campo que virou `null` é apagado:

```csharp
Produto? atual = await produtos.GetAsync([10L]);
if (atual is null) return;

Produto produto = produtos.Track(atual);
produto.DescrProd = "Parafuso M8 zincado";
produto.Preco = null;                       // vira Clear: apaga no servidor
await produtos.UpdateAsync(produto);        // manda só DESCRPROD e PRECO
```

Depois de salvar, o objeto volta a ser rastreado a partir do novo estado.

**3. `NullableState<T>` no modelo.** Cada propriedade guarda se foi informada, apagada ou não. Ela é imutável: troque o valor por atribuição, ou use `NullableState<T>.Set(v)`, `NullableState<T>.Clear()` e `NullableState<T>.Unset` (não mexe no campo, e é o padrão):

```csharp
[SankhyaEntity("Produto")]
public class ProdutoAtualizavel
{
    [Key("CODPROD", true)] public NullableState<long> CodProd { get; set; }
    [XmlElement("DESCRPROD")] public NullableState<string> DescrProd { get; set; }
    [XmlElement("PRECO")] public NullableState<decimal> Preco { get; set; }
}

ISankhyaRepository<ProdutoAtualizavel> repo = sankhya.Repository<ProdutoAtualizavel>();

var produto = new ProdutoAtualizavel { CodProd = 10, DescrProd = "Parafuso M8" };
produto.Preco = NullableState<decimal>.Clear();
await repo.UpdateAsync(produto);
```

**Atualizar todos os campos.** `UpdateAllAsync(entidade)` manda todos os campos mapeados, e valor `null` é apagado. Isso sobrescreve alterações que outros fizeram no Sankhya, então use só quando for o que você quer.

`UpdateAsync(entidade)` sem `Track` e sem propriedades `NullableState<T>` lança `InvalidOperationException`: informe os campos, rastreie o objeto ou use `UpdateAllAsync`.

## Consultas

`Query()` devolve um `SankhyaQuery<T>`, que monta o loadRecords de forma fluente:

```csharp
using SankhyaAPI.Client.Repositories;

await foreach (Produto p in produtos.Query()
                   .Where("this.ATIVO = ? AND this.PRECO > ?", "S", 10m)
                   .OrderBy(x => x.CodProd)
                   .StreamAsync(ct))
{
    // um registro por vez; o pacote pede 50 por página
}

SankhyaPage<Produto> pagina = await produtos.Query()
    .Where("this.ATIVO = ?", "S")
    .OrderBy(x => x.CodProd)
    .ToPageAsync(0, ct);

List<Produto> todos = await produtos.Query().ToListAsync(ct);   // tudo em memória, sem limite
```

| Método | O que faz |
|---|---|
| `Where(expressao, valores...)` | Filtro com `?` para cada valor. Vários `Where` se combinam com `AND`. O tipo do valor é inferido: texto, inteiro, decimal, data, data e hora, sim/não (`S`/`N`) ou código de enum. Erro se a quantidade de `?` não bater com a de valores. |
| `OrderBy(x => x.Campo)`, `OrderByDescending(x => x.Campo)` | Ordenação pela coluna mapeada da propriedade. Não aceita texto livre. |
| `ModifiedSince(DateTime)` | Só os registros alterados desde a data. Exige o parâmetro `LOGTABOPER` habilitado no Sankhya; sem ele, o servidor responde `CORE_E07537`. |
| `IgnoreCalculatedFields()` | Pede `ignoreCalculatedFields`. O efeito sobre campos calculados não foi constatado na homologação. |
| `FileBasedPagination()` | Pede `useFileBasedPagination`. O servidor aceita, sem efeito observável na homologação. |
| `ToListAsync()` | Todos os registros em memória, sem limite. |
| `ToPageAsync(pagina)` | Uma página de até 50 registros, começando em 0. |
| `StreamAsync()` | `IAsyncEnumerable<T>` que pede as páginas em sequência até o Sankhya informar que não há mais. |
| `FirstOrDefaultAsync()` | Primeiro registro da primeira página, pela ordenação. `null` se não houver. |

O `StreamAsync` usa uma ordenação estável: informe um `OrderBy` pela chave. O resultado não é um retrato fixo; se os dados mudarem durante a leitura, a paginação pode repetir ou pular registros.

Os valores do `Where` vão como `<parameter>`, separados da expressão. O comportamento constatado do Sankhya, incluindo `LOGTABOPER` e as entidades ligadas, está em [docs/sankhya-protocolo.md](https://github.com/SamuelGFDias/SankhyaAPI/blob/main/docs/sankhya-protocolo.md).

## SQL cru

`sankhya.Sql` executa SQL pelo `executeQuery`. Escreva o SQL como string interpolada: cada `{valor}` é formatado pelo tipo e escapado.

```csharp
using SankhyaAPI.Client.Sql;
using System.Xml.Serialization;

public class ParceiroLinha
{
    [XmlElement("CODPARC")] public long? CodParc { get; set; }
    [XmlElement("NOMEPARC")] public string? NomeParc { get; set; }
}

string padrao = busca + "%";
int codCid = 10;

List<ParceiroLinha> parceiros = await sankhya.Sql.QueryRawAsync<ParceiroLinha>(
    $"SELECT CODPARC, NOMEPARC FROM TGFPAR WHERE NOMEPARC LIKE {padrao} AND CODCID = {codCid}");

List<Dictionary<string, object?>> linhas = await sankhya.Sql.QueryAsDictionaryAsync(
    SankhyaSql.Raw("SELECT CODPARC, NOMEPARC FROM TGFPAR WHERE ATIVO = 'S'"));
```

Cada valor é formatado assim:

- texto entre aspas simples, com `'` dobrado;
- número em formato invariante;
- `DateOnly` e `DateTime` como `CONVERT(...)` no SQL Server, ou `TO_DATE(...)` no Oracle, conforme `Dialect`;
- `bool` como `'S'` ou `'N'`; enum como o código entre aspas;
- `null` como `NULL`; lista como `(a, b, c)`.

Tipo sem formatação conhecida, ou texto com caractere nulo, lança `ArgumentException`.

**Atenção:** o escape não é o mesmo que parametrizar, porque o `executeQuery` não tem parâmetros. Para filtrar entidades com valores do usuário, use `Query().Where(...)`, que vai pelo loadRecords parametrizado. E não guarde a interpolação numa variável `string` antes de passá-la: a variável já é texto, e o valor sai sem escape. Passe a interpolação direto na chamada, ou declare `SankhyaSql sql = $"..."`. `SankhyaSql.Raw` serve para SQL fixo, escrito por você.

O SQL roda com os privilégios do usuário de integração.

### Limite de 5000 linhas

O `executeQuery` devolve no máximo 5000 linhas, e não há como mudar isso no servidor. Escolha a API conforme o caso:

- `QueryRawAsync` e `QueryAsDictionaryAsync` lançam `SankhyaTruncatedResultException` quando o servidor corta o resultado. Nunca devolvem um resultado incompleto.
- `QueryTopAsync<T>(sql, maxRows)` devolve até `maxRows` linhas (no máximo 4999), com `SankhyaRows<T>.HasMore` dizendo se ficaram linhas de fora. O pacote pede uma linha a mais para saber, porque com `maxRows` o servidor não avisa o corte.
- `QueryPagedAsync<T>(sql, orderBy, pageSize = 1000)` percorre um resultado de qualquer tamanho com `OFFSET ... FETCH NEXT`:

```csharp
await foreach (ParceiroLinha p in sankhya.Sql.QueryPagedAsync<ParceiroLinha>(
                   SankhyaSql.Raw("SELECT CODPARC, NOMEPARC FROM TGFPAR WHERE ATIVO = 'S'"),
                   "CODPARC",
                   pageSize: 1000))
{
    // ...
}
```

Regras da paginação: `orderBy` é obrigatória e fica fixa no código (nunca vinda do usuário), para a paginação não repetir nem pular linhas. O SQL não pode ter `ORDER BY` próprio, porque o SQL Server o recusa dentro da subconsulta: a `ArgumentException` é lançada ao começar a percorrer. `pageSize` vai de 1 a 5000.

Valores do dicionário: inteiros voltam como `long`, decimais como `decimal`, texto sem espaços nas pontas, data quando o texto está em `ddMMyyyy HH:mm:ss`, booleanos do JSON como `bool`, e `null` como `null`. Nomes de coluna repetidos no resultado lançam `InvalidOperationException`: dê alias (`AS`) a cada coluna.

## Relatórios

`sankhya.Reports.GenerateAsync` gera o relatório e baixa o arquivo na mesma sessão, numa chamada só:

```csharp
using SankhyaAPI.Client.Reports;

await using SankhyaReportFile arquivo = await sankhya.Reports.GenerateAsync(
    42,                                                    // código do relatório (NURFE)
    [new ReportParameter("CODEMP", "java.math.BigDecimal", "1")],
    ct);

await using FileStream destino = File.Create("relatorio.pdf");
await arquivo.Content.CopyToAsync(destino, ct);
```

`SankhyaReportFile` traz `Content` (um `Stream`), `ContentType`, `Format`, `CanExport` e `CanSendByEmail`. O conteúdo é copiado para a memória antes de ser devolvido, e o descarte do objeto libera essa memória.

## Erros

Toda falha que vem do Sankhya ou do transporte chega como `SankhyaException` (ou uma subclasse), com uma **categoria** em `Kind` e um **status HTTP sugerido** em `SuggestedHttpStatus`. Assim, quem usa o pacote decide o tratamento e a resposta da própria API sem comparar texto. A classificação foi montada sobre as respostas reais de um Sankhya de homologação (tabela completa em [`docs/sankhya-protocolo.md`](https://github.com/SamuelGFDias/SankhyaAPI/blob/main/docs/sankhya-protocolo.md), seção 6.1).

| `Kind` | Quando | HTTP sugerido |
|---|---|---|
| `AuthenticationFailed` | Usuário ou senha inválidos (`SankhyaAuthenticationException`) | 401 |
| `SessionExpired` | Sessão expirada e já renovada uma vez sem sucesso (`SankhyaSessionExpiredException`) | 401 |
| `Concurrency` | Requisições simultâneas na mesma sessão | 409 |
| `InvalidRequest` | Entidade, campo ou relacionamento inexistente; expressão ou SQL com erro de sintaxe; parâmetros faltando; DML no executeQuery. Erro de programação. | 400 |
| `QueryFailed` | A consulta SQL falhou ao executar (ex.: divisão por zero) | 400 |
| `InvalidValue` | Valor inválido para o campo: conversão, tamanho acima do limite (`Field` traz o campo) | 422 |
| `DuplicateKey` | Chave primária ou única duplicada | 409 |
| `ConstraintViolation` | Chave estrangeira, CHECK, campo obrigatório nulo | 409 |
| `BusinessRule` | Gravação recusada por **regra de negócio**: trigger (RAISERROR, RAISE_APPLICATION_ERROR), evento ou validação do Sankhya (`SankhyaBusinessRuleException`) | 422 |
| `NotFound` | Update de chave inexistente (`SankhyaNotFoundException`) | 404 |
| `ResultTruncated` | executeQuery acima de 5000 linhas (`SankhyaTruncatedResultException`) | 422 |
| `NotConfigured` | Recurso que depende de configuração do Sankhya (ex.: `ModifiedSince` sem LOGTABOPER) | 501 |
| `Timeout` / `Unavailable` | Tempo esgotado / Sankhya fora do ar, HTTP 5xx, resposta ilegível (`SankhyaTransportException`) | 504 / 503 |
| `Unknown` | Erro do Sankhya sem categoria reconhecida | 502 |

`Message` é a mensagem do servidor já decodificada e **limpa**: sem prefixo, sem a frase "A transação foi encerrada no gatilho" do SQL Server, sem o código `ORA-20xxx` e a pilha `ORA-06512` do Oracle. Em `BusinessRule`, ela é o texto que a regra escreveu, pronto para mostrar ao usuário final. Para quem guarda regra de negócio em trigger, toda recusa de gravação que não é um erro técnico reconhecido cai em `BusinessRule`.

`TsErrorCode`, `Status`, `ServiceName` e `Field` vêm junto, e `ToProblem()` devolve um `SankhyaProblem` no formato de `ProblemDetails` (RFC 9457):

```csharp
using SankhyaAPI.Client.Exceptions;

app.MapPut("/produtos/{id}", async (int id, ProdutoDto dto, ISankhyaRepository<Produto> produtos) =>
{
    try
    {
        await produtos.UpdateAsync(dto.ToProduto(id), u => u.Set(x => x.Descricao));
        return Results.NoContent();
    }
    catch (SankhyaException ex)
    {
        SankhyaProblem p = ex.ToProblem();
        return Results.Problem(detail: p.Detail, statusCode: p.Status, title: p.Title, type: p.Type);
    }
});

// ou por categoria
catch (SankhyaException ex) when (ex.Kind == SankhyaErrorKind.BusinessRule)
{
    return Results.UnprocessableEntity(new { mensagem = ex.Message });
}
```

Fora da `SankhyaException`, ficam só os erros de uso e de configuração:

| Exceção | Quando |
|---|---|
| `FormatException` | Um valor recebido do Sankhya não converte para o tipo do campo. A mensagem não traz o valor: ele fica em `ex.Data["Valor"]`, para não ir para o log sem querer. |
| `ArgumentException`, `ArgumentNullException`, `InvalidOperationException` | Validação antes de chamar o servidor: chave ausente ou indevida, update sem campos, mapa inválido, quantidade de `?` diferente da de valores. |
| `OperationCanceledException` | O `CancellationToken` foi cancelado. |
| `OptionsValidationException` | Opções de configuração inválidas. |

## Observabilidade

O pacote usa os mesmos nomes para o tracing e as métricas, e a constante `SankhyaTelemetry.Name` guarda esse nome: `"SankhyaAPI.Client"`.

- **Tracing.** Um `Activity` por tentativa de chamada de serviço (uma repetição gera outro span), nomeado pelo serviço (por exemplo `CRUDServiceProvider.loadRecords`), com a tag `sankhya.service`. Chamada que estoura o tempo marca o span com erro.
- **Métricas.** `sankhya.client.calls` (respostas HTTP de sucesso), `sankhya.client.errors` (com a tag `sankhya.status`), `sankhya.client.logins`, `sankhya.client.session_renewals` e `sankhya.client.duration` (histograma em milissegundos).
- **Logs.** Mensagens estruturadas com `ILogger`: serviço, status, duração, número da tentativa e renovação de sessão.

Nenhum span, métrica ou log carrega valor de campo, de parâmetro ou de SQL.

Para exportar com OpenTelemetry (pacote do consumidor), registre a fonte `"SankhyaAPI.Client"` no tracing e o medidor `"SankhyaAPI.Client"` nas métricas.

## Resiliência

- **Timeout.** Cada tentativa de chamada a um serviço, e o download do relatório, tem o `Timeout` da configuração (100 s por padrão).
- **Novas tentativas.** Falha transitória (timeout, HTTP 5xx, erro de conexão ou resposta que não é XML legível) é repetida até `MaxRetries` vezes, com espera de 200 ms, 400 ms e assim por diante. Vale só para operações idempotentes: login, loadRecords e executeQuery.
- **Escritas não se repetem por falha de transporte.** Insert, update e delete não são repetidos automaticamente, porque o servidor pode ter gravado antes de a resposta se perder. A única repetição de escrita é a da sessão expirada (`status="3"`), que não grava nada.
- **Logout.** Uma falha ao encerrar a sessão é registrada e ignorada. A sessão expira no servidor, e o resultado da operação não se perde.

## Source generator e AOT

O pacote traz um **source generator** e **analisadores**, dentro do próprio NuGet: quem instala ganha os dois sem passo extra.

- Para cada modelo usado num `SankhyaEntityMap<T>` ou marcado com `[SankhyaEntity]`, o generator gera acessores diretos (sem reflexão nem compilação de expressões) e os registra num `[ModuleInitializer]`. O mapa usa esses acessores automaticamente.
- O pacote é marcado `IsAotCompatible` e publica com NativeAOT sem avisos (`samples/AotSmoke`). Para AOT, prefira o mapa fluente; o mapa por atributos ainda descobre as propriedades por reflexão.

Diagnósticos de compilação (erros que, sem eles, só apareceriam em tempo de execução):

| Código | Severidade | Regra |
|---|---|---|
| SNK001 | erro | mais de um campo auto-numerado na chave |
| SNK002 | erro | coluna escrita mais de uma vez no mapa |
| SNK003 | aviso | propriedade de tipo sem conversão conhecida (informe um `SankhyaType` ou `ISankhyaConverter`) |
| SNK004 | aviso | campo calculado com `Writes` e sem `Reads` |
| SNK005 | aviso | SQL cru montado por concatenação (use string interpolada) |
| SNK006 | aviso | `null` atribuído a `NullableState<T>` (use `Clear()`) |
| SNK010 | info | modelo inacessível ao código gerado (privado, abstrato, genérico ou sem construtor público sem parâmetros): usa compilação em tempo de execução, sem AOT |

## Testes

### Testes do próprio pacote

- `scripts/test-fake.sh` faz o build e roda os testes contra um servidor fake, em `net8.0` e `net10.0`, dentro de containers Podman. Não precisa de Sankhya.
- `scripts/test-real.sh [filtro]` roda os testes contra um Sankhya de homologação. A URL, o usuário e a senha vêm de `~/.config/sankhyaapi/teste.env` (permissão 600), ou do arquivo indicado em `SANKHYA_CREDENTIALS_FILE`. As variáveis `SANKHYA_BASE_URL`, `SANKHYA_USER` e `SANKHYA_PASSWORD` têm precedência sobre o arquivo. Sem essas credenciais, os testes reais ficam como *skipped*.
- O filtro padrão é `Category=Real`, que só lê dados. `Category=RealEscrita` grava na tabela `AD_TESTE`, criada na homologação só para testes de escrita.

### Testando a sua integração

O pacote `SankhyaAPI.Client.Testing` reúne os auxiliares para testar código que usa o pacote, sem um Sankhya de verdade:

- `SankhyaFakeServer`: servidor HTTP local que imita o `service.sbr` do Sankhya (XML em ISO-8859-1, mensagens em base64, status 0/1/3, executeQuery em JSON). Registre respostas com `WithEntities`, `WithError`, `WithStatus`, `WithQuery`, `WithPage`, `WithSessionExpiredOnce`, `WithReport`, `WithDelay`, `WithHttpStatus`, e inspecione as chamadas com `Calls(servico)`.
- `CreateClient(mode, maxRetries, configure)`: cria um `SankhyaClient` apontado para o servidor fake.
- `SankhyaMapAssert.RoundTrip(exemplo)`: monta o insert de um mapa, devolve as colunas como resposta e lê de volta, sem servidor.

```csharp
using SankhyaAPI.Client.Testing;

using var fake = new SankhyaFakeServer();
fake.WithError("CRUDServiceProvider.saveRecord", "Pedido bloqueado: cliente com títulos vencidos.");
await using SankhyaClient cliente = fake.CreateClient();

var ex = await Assert.ThrowsAsync<SankhyaBusinessRuleException>(() => cliente.Repository<Pedido>().CreateAsync(pedido));
Assert.Equal("Pedido bloqueado: cliente com títulos vencidos.", ex.Message);
Assert.Single(fake.Calls("CRUDServiceProvider.saveRecord"));
```

## Skill para agentes de IA

O pacote traz uma **skill** que ensina um agente de IA (Claude Code, Codex, Cursor e outros compatíveis com o formato `SKILL.md`) a usar esta biblioteca:
- setup inicial, com e sem injeção de dependência;
- mapeamento de tabelas, CRUD, update parcial, consultas, SQL seguro e relatórios;
- tratamento de erros com status HTTP e testes com o servidor fake;
- **migração de uma versão anterior para a versão instalada**: a skill lê o `CHANGELOG.md` que vem dentro do pacote e monta a lista de quebras e de mudanças de comportamento entre as duas versões.

Ela vem em `skills/sankhyaapi-client/` dentro do pacote. Depois do `dotnet restore`, copie para a pasta de skills do seu projeto:

```bash
# Linux/macOS (troque 6.0.0 pela versão instalada)
mkdir -p .claude/skills
cp -r ~/.nuget/packages/sankhyaapi.client/6.0.0/skills/sankhyaapi-client .claude/skills/
```

```powershell
# Windows
Copy-Item -Recurse "$env:USERPROFILE\.nuget\packages\sankhyaapi.client\6.0.0\skills\sankhyaapi-client" ".claude\skills\"
```

Para usar em todos os projetos, copie para `~/.claude/skills/`. Peça ao agente coisas como "configura o SankhyaAPI.Client neste projeto", "mapeia a tabela TGFPAR", "migra do 5.2 para o 6.0" ou "trata os erros do Sankhya na API". Ao atualizar o pacote, copie a skill de novo: ela acompanha a versão.

A pasta da versão também traz o `CHANGELOG.md` e `docs/migracao-6.0.md`, que a skill consulta.

## Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](https://github.com/SamuelGFDias/SankhyaAPI/blob/main/LICENSE.txt) para mais detalhes.
