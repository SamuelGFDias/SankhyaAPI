---
name: sankhyaapi-desenvolvimento
description: Como manter e evoluir o repositório SankhyaAPI (pacote SankhyaAPI.Client 6.x) — arquitetura, gates de teste (fake em podman com SDK 8 e 10, reais na homologação), release no NuGet por Trusted Publishing, onde registrar achados, e os impedimentos já resolvidos (podman/SELinux, SDK 8 x net10, generator netstandard2.0, AOT, empacotamento, armadilhas de C#) para não repetir erro. Use ao mexer no código, nos testes, no CI, no empacotamento ou ao investigar comportamento do Sankhya neste repositório.
---

# SankhyaAPI: manutenção

Comportamento do servidor Sankhya: [`docs/sankhya-protocolo.md`](../../../docs/sankhya-protocolo.md) (skill `sankhya-protocolo`). Guia para quem **usa** o pacote: [`skills/sankhyaapi-client/`](../../../skills/sankhyaapi-client/SKILL.md), que vai dentro do NuGet. Desenho da 6.0: [`docs/proposta-6.0.md`](../../../docs/proposta-6.0.md) (seção 11, "Como ficou").

## Arquitetura (SankhyaAPI.Client)

| Pasta | O que tem |
|---|---|
| `Transport/` | `SankhyaTransport`: HttpClient sem cookies, envelopes XML (LINQ to XML) e JSON (`Utf8JsonWriter`/`JsonDocument`), retry só em operação idempotente, `SankhyaResponses.EnsureSuccess` (decide pelo `status`) |
| `Session/` | `SankhyaSession`: pool `Shared` (sessões exclusivas, `MaxSessions`), `PerOperation`, sessão explícita, renovação em status 3 |
| `Mapping/` | `SankhyaEntityMap<T>` (fluente), `AttributeMaps` (atributos), `EntityMap`/`MemberMap` (mapa compilado), `SankhyaFormat` (conversões), `NullableState<T>` imutável, `SankhyaAccessorRegistry` (acessores gerados) |
| `Repositories/` | `SankhyaRepository<T>` (CRUD, update parcial, `VerifyExistenceOnUpdate`), `SankhyaQuery<T>`, `EntityXml` (envelopes e leitura de `<entities>`) |
| `Sql/` | `SankhyaSqlClient` (executeQuery, top, paginado), `SankhyaSql` (interpolação com escape) |
| `Exceptions/` | `SankhyaException` + `SankhyaErrorKind` + `SuggestedHttpStatus`; `SankhyaErrorClassifier` (regras tiradas de respostas reais) |
| `SankhyaAPI.Client.Generators/` | generator de acessores + analisadores SNK001–SNK010 (netstandard2.0, empacotado em `analyzers/dotnet/cs`) |
| `SankhyaAPI.Client.Testing/` | `SankhyaFakeServer` (WireMock.Net), `CreateClient`, `SankhyaMapAssert` |

## Gates (obrigatórios antes de PR)

```bash
./scripts/test-fake.sh                                                      # SDK 10 (net10.0) e SDK 8 (net8.0), sem avisos
./scripts/test-real.sh "Category=Real|Category=RealEscrita|Category=RealNota"   # homologação
```

- Credenciais em `~/.config/sankhyaapi/teste.env` (600). Nunca imprimir nem versionar.
- `Real` só lê. `RealEscrita` grava na `AD_TESTE` (ID, TEXTO, INTEIRO, AD_DECIMAL, AD_DATA, DATAHORA, HORA, ATIVO, SITUACAO). `RealNota` cria pedido (TGFCAB, `OBSERVACAO` começando com `TESTE-SANKHYAAPI-`) e contato do parceiro 4 (TGFCTT), e apaga tudo no fim.
- Confira sobras depois: `./scripts/sondar-sankhya.sh sql "SELECT (SELECT COUNT(*) FROM AD_TESTE) A, (SELECT COUNT(*) FROM TGFCAB WHERE OBSERVACAO LIKE 'TESTE-SANKHYAAPI-%') P, (SELECT COUNT(*) FROM TGFCTT WHERE NOMECONTATO LIKE 'TESTE SANKHYAAPI%') C"`.
- **Nunca** apague ou altere nota, parceiro ou registro que o teste não criou.
- AOT: `dotnet publish samples/AotSmoke -r linux-x64` num container com `clang zlib1g-dev` instalados, com avisos como erro.
- Pacote: `dotnet pack SankhyaAPI.Client -c Release` e confira no `.nupkg`: `analyzers/dotnet/cs/SankhyaAPI.Client.Generators.dll`, `skills/sankhyaapi-client/**`, `CHANGELOG.md`, `docs/migracao-6.0.md`, `lib/net8.0` e `lib/net10.0`.

## Fluxo de mudança

1. Branch por assunto, commit sem rodapé de atribuição de IA.
2. Achado novo do servidor: sonde com `scripts/sondar-sankhya.sh` e registre em `docs/sankhya-protocolo.md` (fonte H, T ou D). Se mudar o comportamento, acrescente teste real.
3. Erro novo do servidor: inclua a mensagem real no `[Theory]` de `ClassificacaoDeErrosTests` e ajuste `SankhyaErrorClassifier`.
4. Mudança de API ou de comportamento: CHANGELOG (com ⚠ quando muda comportamento), README raiz **e** `SankhyaAPI.Client/README.md` (o que vai no NuGet; os dois são iguais) e a skill do usuário em `skills/sankhyaapi-client/`.
5. Gates, PR com `Closes #N`, merge (`gh pr merge --admin` está autorizado neste repositório, porque o ruleset exige aprovação e o check `test`).

## Release

- Tag `vX.Y.Z` na `main` dispara `publish-nuget.yml`: testa, empacota o cliente e o Testing e publica por **Trusted Publishing** (`NuGet/login@v1`, usuário `samueldias21`, `id-token: write`). Não usa secret de API.
- O CI só roda com a cobrança da conta GitHub em dia. Enquanto não, a verificação é local.
- Nunca crie tag nem publique sem pedido explícito do usuário.

## Impedimentos já resolvidos (não repita)

**Ambiente**
- Não há `dotnet` local: tudo roda em `podman` com `mcr.microsoft.com/dotnet/sdk:10.0` ou `:8.0`.
- Use volume com **`:z`** (minúsculo). Com `:Z`, cada contêiner reetiqueta a árvore no SELinux, e contêineres paralelos (de outras worktrees) dão "Access denied".
- O SDK 8 não conhece `net10.0`. O gate net8 usa `-p:SankhyaSoNet8=true` (condição nos csproj multi-target). **Não** use `-p:TargetFrameworks=net8.0` global: ele vaza para o generator (netstandard2.0) e quebra o restore.
- O BenchmarkDotNet falha escrevendo no bind mount: rode numa cópia dentro do contêiner e traga os artefatos.
- `git add -A` chegou a incluir `.claude/worktrees/` (worktrees de subagentes) como repositórios embutidos. A pasta está no `.gitignore`; confira `git show --stat` antes do push.

**C#, generator e AOT**
- Ternário une tipos sem avisar: `cond ? long : decimal` vira `decimal`, e `cond ? DateTimeOffset : DateTime` vira `DateTimeOffset`. Faça cast para `object` ou use `if` (dois bugs reais vieram disso).
- O generator em netstandard2.0 precisa do polyfill `IsExternalInit` para records. Com Roslyn 4.8, não use collection expression em `ImmutableArray` (use `ImmutableArray.Create`). A mensagem do diagnóstico segue a RS1032: uma frase, sem ponto final, sem ". " no meio.
- AOT: nada de `MakeGenericType` no caminho quente. `NullableState<T>` implementa `INullableStateValue` para leitura e criação sem reflexão. O JSON usa `options.GetTypeInfo(typeof(T))`. `Expression.Compile` só como reserva quando não há acessor gerado (SNK010 avisa).
- Com `GenerateDocumentationFile` e `TreatWarningsAsErrors`, CS1591 e CS1573 viram erro: estão em `NoWarn`.

**Empacotamento**
- `<None Update="LICENSE.txt">` não empacotou (NU5030): use `Include` com `Pack="true"`.
- `PackagePath` de pasta já acrescenta o `RecursiveDir`. Somar `%(RecursiveDir)` duplica o caminho (`references/references`).

**Testes**
- WireMock.Net 1.6.7 trazia dependências vulneráveis (NU1903): use a 2.x, que tem `RequestMessage` anulável (`!`).
- O fake responde JSON em Latin1: o transporte lê o JSON com `ReadAsStringAsync`, que respeita o charset, e não com `JsonDocument.ParseAsync` sobre o stream.
- O `Utf8JsonWriter` escapa `'` como `'`: no teste, compare o SQL fazendo parse do corpo JSON, não por `Contains`.
- `VerifyExistenceOnUpdate` vem ligado. Teste fake de update precisa de `WithEntities(loadRecords, [chave])` ou de `CreateClient(configure: o => o.VerifyExistenceOnUpdate = false)`.
- Para provar que a repetição usa a sessão nova, use `WithLoginSequence("S1","S2")`: o login padrão do fake devolve sempre o mesmo id.
- Teste real que encerra sessão "por fora" precisa pegar o id **do pool** (`SankhyaSession.PooledSessionIds`), não o da sessão explícita.

**Sankhya (detalhe em `docs/sankhya-protocolo.md`)**
- Sessão não aceita requisições simultâneas, por isso o pool.
- saveRecord de update é upsert, por isso `VerifyExistenceOnUpdate`.
- executeQuery: teto de 5000 linhas, sem parâmetros, só SELECT. DDL é recusado, então não dá para criar trigger de teste.
- Item de nota (TGFITE) não grava pelo CRUD genérico (status 0 sem mensagem). **Limite conhecido**, decidido com o usuário: não investigar mais sem pedido. Já descartado: falta de coluna do banco (falha igual com as 47 colunas de um item real). Pista não testada: `CODTPA` tem `requerido = S` no dicionário (TDDPCO) e é anulável no banco. Detalhes em `docs/sankhya-protocolo.md`, "Limite conhecido".
- Campos obrigatórios: compare `TDDPCO` (`requerido`/`required`) com o `NOT NULL` do banco. Obrigatório no dicionário e anulável no banco não dá erro de banco e pode falhar sem mensagem.
- Consulta na TGFITE com `GROUP BY` ou `WHERE ... IS NOT NULL` passa de minutos: amostre pela chave (`WHERE NUNOTA = ? AND SEQUENCIA = ?`). Para provocar as triggers do item, use **update inválido num item existente, restaurando o original** (`Triggers_do_item_de_nota_...`). `CACSP` não existe no `/mge`, e o `/mgecom` responde 302 nesta homologação.
- `NUMNOTA` é obrigatório no cabeçalho de nota (mande 0 em pedido).
- Erro sem mensagem não é regra de negócio: o classificador devolve `Unknown` explicado.
- A homologação às vezes fica lenta (uma exclusão passou de 100 s e concluiu depois). Antes de culpar o código, repita a operação isolada.
