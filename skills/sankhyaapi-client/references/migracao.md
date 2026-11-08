# Migração entre versões

## Roteiro (vale para qualquer par de versões)

1. **Versão instalada (V1)**: o `Version` do `PackageReference Include="SankhyaAPI.Client"` no `.csproj`.
2. **Versão alvo (V2)**: a pedida, ou a mais recente.
3. **Ler o CHANGELOG da V2.** Restaure a V2 (`dotnet add package SankhyaAPI.Client --version V2`, ou edite o csproj e rode `dotnet restore`) e abra `~/.nuget/packages/sankhyaapi.client/V2/CHANGELOG.md`. Sem a pasta, use https://github.com/SamuelGFDias/SankhyaAPI/blob/main/CHANGELOG.md.
4. Leia **todas as seções entre V1 (exclusive) e V2 (inclusive)**. Monte uma lista com:
   - quebras de API: o que não compila mais;
   - mudanças de comportamento, marcadas com ⚠ ou nas seções "Mudanças de comportamento": compilam, mas fazem outra coisa;
   - novidades úteis para o código do usuário.
5. Mostre a lista ao usuário antes de mexer no código, com o impacto em cada arquivo do projeto dele (procure os usos com `grep`).
6. Migre, compile e rode os testes. Os analisadores SNK do pacote acusam boa parte dos problemas de mapa.
7. Revise as mudanças de comportamento uma a uma: são as que o compilador não pega.

## 5.x → 6.0 (quebra de API, sem camada de compatibilidade)

O guia completo está em `~/.nuget/packages/sankhyaapi.client/6.0.0/docs/migracao-6.0.md`, funcionalidade por funcionalidade. Resumo do que muda no código do usuário:

| 5.x | 6.0 |
|---|---|
| `class XService : BaseService<T>` com construtor `(baseUrl, user, password, entityName)` | `ISankhyaRepository<T>` (injetado ou `sankhya.Repository<T>()`). O serviço do usuário recebe o repositório em vez de herdar. |
| `AddSankhyaClient()` + `SankhyaClientSettings` | `services.AddSankhya(o => ...).AddEntity<T, TMap>()` / `.AddRepository<T>()` |
| `new XService(url, u, p)` fora de DI | `SankhyaClient.Create(new SankhyaClientOptions { ... })` + `await using` |
| `SessionService.LoginSankhya/LogoutSankhya` | `sankhya.Session.LoginAsync()/LogoutAsync()` |
| `QueryRawAsync<T>(string)` / `QueryAsDictionaryAsync(string)` no serviço | `ISankhyaSql.QueryRawAsync<T>($"...")` / `QueryAsDictionaryAsync($"...")` (interpolação com escape) |
| `RelatorioService.GerarRelatorio` + `ObterRelatorio` | `ISankhyaReports.GenerateAsync(codigo, parametros)` |
| `DeleteAsync(List<T>)` | `DeleteManyAsync(IEnumerable<T>)` |
| `Envelopes.Parameter` | `Repositories.Parameter` (mesmas fábricas `Texto/Inteiro/Decimal/Data/DataHora`) |
| `MetaData.NullableState<T>` mutável | `Mapping.NullableState<T>` imutável (`Set`, `Clear()`, `Unset`) |
| `Extensions.KeyAttribute`, modelos herdando `SankhyaModelBase` | `Mapping.KeyAttribute`; `SankhyaModelBase` saiu; nome da entidade em `[SankhyaEntity("...")]` |
| `catch (HttpRequestException)` | `catch (SankhyaTransportException)` |
| comparar `ex.Message` | `ex.Kind` / `ex.SuggestedHttpStatus` / `SankhyaBusinessRuleException` |

**Mudanças de comportamento que não dão erro de compilação** (revise):
- A sessão é compartilhada por padrão (um pool). Antes era login e logout por operação; para o comportamento antigo, use `SessionMode.PerOperation`.
- Update de chave inexistente lança `SankhyaNotFoundException`. Antes, o Sankhya criava o registro; para manter isso, use `VerifyExistenceOnUpdate = false`.
- `UpdateAsync(entidade)` sem `Track` e sem `NullableState` no modelo lança `InvalidOperationException`.
- O timeout é de 100 s por tentativa (antes, 1 dia). Leituras são repetidas em falha transitória.
- A mensagem de erro não tem mais o prefixo `SNK Error:`.
- No dicionário do SQL, inteiros voltam como `long`.

Caminho sugerido para migrar um serviço 5.x:
1. Troque a herança de `BaseService<T>` por injeção de `ISankhyaRepository<T>`.
2. Mantenha os modelos com atributos, acrescentando `[SankhyaEntity("Nome")]`.
3. Registre com `AddRepository<T>()`.
4. Ajuste os `catch`.
5. Depois, se quiser, migre os modelos para o mapa fluente, um por vez: os dois caminhos convivem.
