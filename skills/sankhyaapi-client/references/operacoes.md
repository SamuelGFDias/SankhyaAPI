# Operações

Todos os métodos aceitam `CancellationToken` (último parâmetro, opcional).

## CRUD (`ISankhyaRepository<T>`)

```csharp
var produtos = sankhya.Repository<Produto>();          // ou injete ISankhyaRepository<Produto>

Produto? criado = await produtos.CreateAsync(produto);  // a chave auto-numerada é preenchida em `produto`
List<Produto> lote = await produtos.CreateManyAsync(lista);   // uma chamada só
await produtos.DeleteAsync(produto);                    // pela chave (simples ou composta)
await produtos.DeleteManyAsync(lista);
Produto? um = await produtos.GetAsync([10L]);           // chave composta: GetAsync([nunota, sequencia])
```

Validações antes de chamar o servidor: chave auto-numerada preenchida no insert, chave não numerada vazia no insert e chave vazia em update ou delete lançam `ArgumentException`/`ArgumentNullException`.

## Update parcial

O update manda só o que for pedido. Há três modos:

```csharp
// 1. Campos explícitos (padrão recomendado)
await produtos.UpdateAsync(produto, u => u.Set(x => x.Descricao).Clear(x => x.Observacao));

// 2. Rastreamento: guarda uma cópia e manda só o que mudou (null vira apagar)
var p = produtos.Track((await produtos.GetAsync([10L]))!);
p.Descricao = "Novo";
await produtos.UpdateAsync(p);

// 3. NullableState<T> no modelo: Set vai, Clear apaga, UnSet não vai
await produtos.UpdateAsync(new Produto { CodProd = 10, DescrProd = "Novo", Obs = NullableState<string>.Clear() });

// Tudo (sobrescreve o que outros mudaram): só quando for isso mesmo
await produtos.UpdateAllAsync(produto);
```

`UpdateAsync(entidade)` sem `Track` e sem `NullableState` no modelo lança `InvalidOperationException`, de propósito. Antes de gravar, o pacote confere se a chave existe (`VerifyExistenceOnUpdate`), porque o Sankhya criaria o registro.

## Consultas

```csharp
// Filtro parametrizado: valores viram <parameter>, nunca SQL
List<Produto> ativos = await produtos.Query()
    .Where("this.ATIVO = ? AND this.CODGRUPOPROD = ?", true, codGrupo)
    .Where("this.DESCRPROD LIKE ?", busca + "%")          // várias chamadas = AND
    .OrderBy(x => x.CodProd)                              // coluna do mapa (sem texto livre)
    .ToListAsync();                                       // tudo em memória: cuidado com volume

await foreach (Produto p in produtos.Query().OrderBy(x => x.CodProd).StreamAsync())   // paginado, 50 por chamada
{ ... }

SankhyaPage<Produto> pagina = await produtos.Query().ToPageAsync(0);   // Items, Page, HasMoreResult
Produto? primeiro = await produtos.Query().OrderByDescending(x => x.CodProd).FirstOrDefaultAsync();
```

Outras opções: `ModifiedSince(data)` (exige LOGTABOPER no Sankhya), `IgnoreCalculatedFields()`, `FileBasedPagination()`.

Também há `FindAsync(expr)`, `FindAsync(expr, [Parameter.Texto(..), Parameter.Inteiro(..), Parameter.Decimal(..), Parameter.Data(..), Parameter.DataHora(..)], orderBy)` e `FindPageAsync`.

**O loadRecords não tem limite de linhas**: `FindAsync("1 = 1")` numa tabela grande traz tudo. Para volume, use `StreamAsync`.

## SQL cru (`ISankhyaSql`, executeQuery)

```csharp
// Interpolação direto na chamada: cada valor é escapado pelo tipo
var linhas = await sql.QueryRawAsync<ParceiroLinha>($"SELECT CODPARC, NOMEPARC FROM TGFPAR WHERE NOMEPARC LIKE {busca + "%"}");
var dicionario = await sql.QueryAsDictionaryAsync($"SELECT COUNT(*) AS N FROM TGFPRO WHERE CODGRUPOPROD = {codGrupo}");

SankhyaRows<ParceiroLinha> top = await sql.QueryTopAsync<ParceiroLinha>("SELECT CODPARC FROM TGFPAR", 100);  // até 4999, HasMore

await foreach (var l in sql.QueryPagedAsync<ParceiroLinha>("SELECT CODPARC FROM TGFPAR", "CODPARC", pageSize: 1000)) { ... }
```

Regras:
- Teto fixo de **5000 linhas**: acima disso, `QueryRawAsync`/`QueryAsDictionaryAsync` lançam `SankhyaTruncatedResultException`.
- `QueryPagedAsync` pede a ordenação à parte, e o SQL não pode ter `ORDER BY`.
- Só `SELECT`: o servidor recusa UPDATE, DELETE, INSERT e DDL.
- Não guarde a interpolação numa `string` antes de passar: ela vira texto sem escape (SNK005). `SankhyaSql.Raw("...")` serve para SQL fixo.
- O tipo da linha mapeia por `[XmlElement]`, pelo mapa registrado ou pelo nome da propriedade. No dicionário, inteiros voltam como `long`.

## Relatórios (`ISankhyaReports`)

```csharp
await using SankhyaReportFile arquivo = await relatorios.GenerateAsync(42, [new ReportParameter("CODEMP", "java.math.BigDecimal", "1")]);
await arquivo.Content.CopyToAsync(destino);   // ContentType, Format, CanExport, CanSendByEmail
```

## Sessão

- `Shared` (padrão): pool de até `MaxSessions` sessões reaproveitadas, uma operação por vez em cada. Sessão expirada é renovada e a operação repetida uma vez, o que é seguro: com status 3 o servidor não executou nada. O logout acontece no `DisposeAsync`.
- `PerOperation`: login e logout em cada operação.
- `await sankhya.Session.LoginAsync()` abre uma sessão explícita, usada por todas as operações até `LogoutAsync()`.

## Serviço que o pacote não cobre

```csharp
SankhyaXmlResponse r = await sankhya.Session.ExecuteAsync((id, ct) =>
    sankhya.Transport.SendXmlAsync("Servico.metodo", [new XElement("campo", "valor")], id, idempotent: false, ct));
```
