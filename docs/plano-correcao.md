# Plano de correção — rodada 1 de planejamento

Escopo: as issues #1 a #19 abertas pela auditoria, mais as melhorias estruturais que elas pedem (troca de reflexão por source generator, AOT/trimming, DI). Base: `main` depois do merge do PR #20, que já traz a infra de testes (servidor fake + testes reais opt-in).

## Estratégia

Três entregas, para quebrar a API uma vez só:

| Entrega | Versão | Quebra API? | Conteúdo |
|---|---|---|---|
| **A — correções** | 5.2.0 | não | bugs de comportamento e segurança que cabem sem mudar assinatura |
| **B — infraestrutura** | 6.0.0-preview.1 | sim | HTTP, DI, sessão, cancelamento, relatório |
| **C — modelo e geração** | 6.0.0-preview.2 → 6.0.0 | sim | source generator, `NullableState` imutável, serialização sem reflexão, limpeza |

B e C saem juntas como 6.0.0: quem migra paga a quebra uma vez. As previews (`v6.0.0-preview.N`) já passam pelo workflow de publicação, que aceita sufixo na tag.

Regra para todo item: **teste primeiro**. Correção começa por um teste fake que falha (ou pelo teste skipped que já existe); comportamento que depende do servidor ganha também um teste real.

---

## Entrega A — 5.2.0 (sem quebra)

Ordem pensada por dependência: o modelo de erro (#5) vem antes, porque várias correções passam a lançar a exceção nova.

| # | Item | Como | Teste | Tamanho |
|---|---|---|---|---|
| A1 | **#5** status e `SankhyaException` | `VerificarErros` decide por `status != "1"`; nova `SankhyaException : Exception` com `Status`, `TsErrorCode`, `ServiceName`. Herdar de `Exception` mantém quem faz `catch (Exception)` funcionando. Status de sessão expirada vira `SankhyaSessionExpiredException` (usada no B4). | fake: `status="0"` sem mensagem lança; `status="1"` com mensagem não lança | P |
| A2 | **#4** `TsError.StatusMessage` | Reaproveitar o decodificador de `ServiceEnvelope.StatusMessage` (base64 → ISO-8859-1, fallback para texto). | fake com `<tsError>` | P |
| A3 | **#1** logout | Cookie `JSESSIONID=...`, rota `/service.sbr`. Reativar os 2 testes skipped. | fake (2 testes) + real `Logout_encerra_a_sessao_no_servidor` | P |
| A4 | **#3** ordem do `fieldsMetadata` | Chave = `FieldsMetadata[i].Name`, valor = `row[Order - 1]`; coluna repetida ganha sufixo ou erro claro. Extrair a montagem para um método único usado por `Query<T>` e `Query`. | reativar o teste skipped + coluna repetida | P |
| A5 | **#2** parâmetros no `FindAsync` | Sobrecarga `FindAsync(string expression, IEnumerable<Parameter> parameters, string? orderBy = null)`, usando o `Criteria.Parameter` que já existe. Sobrecarga com limite/offset no loadRecords. Aditivo: não quebra. README com aviso sobre SQL cru em `QueryRawAsync`. | fake: valor vai em `<parameter type="S">`, não na `<expression>`; real: filtro parametrizado no SUP | M |
| A6 | **#6** `Entities.ReadXml` | Ler cada `<entity>` como subárvore (`ReadSubtree`) e mapear por nome (`fN` → índice do campo pedido, ou nome da coluna), sem depender da ordem e sem passar de `</entities>`. Ainda por reflexão, mas usando o cache do A9. | fake: colunas fora de ordem, campo omitido, elemento depois de `</entities>` | M |
| A7 | **#13** chave no insert | Exigir `Set` em chave não auto-enumerada no insert. Muda comportamento (antes passava), então entra no changelog como correção. | fake | P |
| A8 | **#14** `LoginEntity` | `int.TryParse`. | fake com `idusu` vazio | P |
| A9 | **#12** dado em exceção + cache de reflexão (primeira parte de **#17**) | Mensagens sem o valor (valor só em `Exception.Data`). Cache de `PropertyInfo`/atributos por tipo (`ConcurrentDictionary<Type, ModelMap>`), sem `dynamic`. Esse `ModelMap` vira o contrato que o generator implementa na entrega C. | fake: mensagem não contém o valor; benchmark simples antes/depois | M |
| A10 | **#15** encoding | Primeiro verificar: teste real de escrita gravando e relendo um texto com acento. Corrigir só se o teste mostrar corrupção (provável: `XmlContentSerializer` com encoding ISO-8859-1 explícito). | real (escrita) + fake conferindo bytes | P–M |

Pendência de A10 e de todo teste real de escrita: falta escolher uma entidade segura na homologação (ver "Decisões").

---

## Entrega B — 6.0.0-preview.1 (infraestrutura)

| # | Item | Como | Teste | Tamanho |
|---|---|---|---|---|
| B1 | **#8** HTTP | `IHttpClientFactory` + `AddRefitClient` (o `Refit.HttpClientFactory` já está referenciado). Um client por formato (XML/JSON), não três por instância. `UseCookies = false`: sessão só no header, acabando com o vazamento de cookie entre chamadas. Timeout configurável (padrão 100 s). URL validada, https obrigatório salvo opção explícita para dev. | fake: nenhuma chamada leva cookie de outra sessão; URL `http://` rejeitada | M |
| B2 | **#7** opções e DI | `SankhyaClientOptions` (BaseUrl, Usuario, Senha, Timeout, AllowHttp) com validação na inicialização (`ValidateOnStart`). `AddSankhyaClient(Action<SankhyaClientOptions>)` + `AddSankhyaService<TService>()`. Os serviços passam a receber `ISankhyaSession` por injeção em vez de `(baseUrl, user, password)`: **quebra** os construtores. Senha lida da configuração (user-secrets/vault), nunca fixa. README reescrito. | teste de DI: `ServiceProvider` resolve os serviços e chama o fake | M |
| B3 | **#10** `CancellationToken` | Parâmetro opcional em todos os métodos públicos, repassado ao Refit. | fake com atraso: cancelar interrompe | P |
| B4 | **#9** sessão reaproveitada | `ISankhyaSession` com dois modos: `PorOperacao` (atual) e `Compartilhada` (login sob demanda com `SemaphoreSlim`, renovação ao receber `SankhyaSessionExpiredException`, logout no `DisposeAsync`). Depende de A1 e B1. | fake: 3 operações = 1 login; sessão expirada → relogin e repetição única | M–G |
| B5 | **#11** relatório | `GerarRelatorioAsync(...)` que abre a sessão, gera, baixa o arquivo e encerra no `finally`, devolvendo o `Stream` já copiado. Os dois métodos atuais ficam `[Obsolete]`. | fake (2 serviços em sequência) + real se houver relatório de teste | P |

---

## Entrega C — 6.0.0 (modelo, geração de código e limpeza)

### C1 — Source generator para os modelos (**#17**)

**Onde a reflexão está hoje** (todos os pontos levantados no código):

| Ponto | Arquivo | Uso | Substituição |
|---|---|---|---|
| Campos pedidos no envelope | `ObjectUtilsMethods.GetFieldsFromObject` | `GetProperties()` + atributos | lista estática gerada |
| Campos do insert/update | `AddEntityForFields` | `GetProperties()`, `GetValue`, `State` por reflexão | método gerado que escreve no `XmlWriter` |
| Chaves | `AddKeysAsXml`, `ValidarCamposChave`, `GetKeysPropertiesFromObject` | atributos `[Key]`, `new T()` | código gerado por modelo |
| Validação `NullableState` | `SankhyaModelBase.ValidateNullableStateProperties` | checagem em runtime | **diagnóstico de compilação** |
| Leitura do loadRecords/saveRecord | `Entities.ReadXml` | `Activator.CreateInstance`, `SetValue` por índice | `ReadEntity(XmlReader)` gerado, com parse tipado por propriedade |
| Linhas do executeQuery | `GetListOfObjectsFromDictionary` | `dynamic`, `FirstOrDefault` por campo | `FromRow(JsonElement[] row, int[] colunaPorCampo)` gerado |
| Conversão de valor | `ConvertForPropertyType` | switch em `Type` + `Activator.CreateInstance(NullableState<>)` | parse/format específico por tipo, sem boxing |
| Enums com `[XmlEnum]` | `GetXmlEnumValue`, `GetEnumValueFromXml` | `GetField`, `Enum.Parse` | `switch` gerado por enum |
| Registro no DI | `SankhyaApiExtensions.AddSankhyaClient` | `Assembly.GetTypes()` | substituído por `AddSankhyaService<T>()` (B2); opcionalmente gerado |

**Desenho.** Projeto novo `SankhyaAPI.Client.Generators` (netstandard2.0, `IIncrementalGenerator`), empacotado dentro do mesmo NuGet em `analyzers/dotnet/cs`: quem instala o pacote ganha o generator sem passo extra.

- O consumidor marca o modelo como `partial`:
  ```csharp
  [SankhyaEntity("Produto")]
  public partial class ProdutoEntity : SankhyaModelBase
  {
      [Key("CODPROD", autoEnumerable: true)] public NullableState<long> CodProd { get; set; }
      [XmlElement("DESCRPROD")] public NullableState<string> DescrProd { get; set; }
  }
  ```
- O generator emite um `ProdutoEntityMap : IModelMap<ProdutoEntity>` (o mesmo contrato do cache de reflexão do A9) e o registra via `[ModuleInitializer]` num registro estático (`ModelMaps.Register(...)`).
- O pacote resolve o mapa por `ModelMaps.Get<T>()`: **gerado se existir, reflexão com cache se não existir**. Modelos antigos, não `partial`, continuam funcionando, e a migração pode ser feita modelo a modelo.
- `[SankhyaEntity("Produto")]` também leva o nome da entidade, e o `BaseService<T>` deixa de precisar receber a string.
- `QueryRawAsync<T>` usa o mesmo mecanismo com `[SankhyaQueryRow]` para tipos de linha que não são entidades.

**Diagnósticos (erros que hoje só aparecem em runtime):**

| Código | Severidade | Regra |
|---|---|---|
| SNK001 | erro | propriedade mapeada que não é `NullableState<T>` em modelo de entidade |
| SNK002 | erro | `[Key]` com nome vazio, ou nome XML duplicado no modelo |
| SNK003 | erro | enum usado em modelo com membro sem `[XmlEnum]` |
| SNK004 | aviso | tipo de propriedade sem conversão conhecida |
| SNK005 | info | modelo não é `partial`: usa reflexão (sugere tornar `partial`) |

**Testes.**
- Snapshot do código gerado (Verify.SourceGenerators) para os modelos de teste.
- **Paridade**: para cada modelo de teste, o envelope XML gerado pelo mapa gerado é idêntico ao do mapa por reflexão, e a leitura das respostas fake produz objetos iguais. É isso que permite trocar o motor sem mudar o comportamento.
- Testes de diagnóstico: cada SNK com um caso que dispara e um que não dispara.
- Os testes fake e reais existentes rodam com os modelos `partial` (gerado) e não `partial` (reflexão).

Tamanho: G. É o maior item do plano, e por isso o A9 já define o contrato `IModelMap<T>` na 5.2.0: o generator só implementa algo que já foi testado.

### C2 — Serialização sem reflexão (AOT e trimming)

- **JSON (executeQuery):** `JsonSerializerContext` com source generation do System.Text.Json para `ServiceEnvelope`/`ResponseBody` da query.
- **XML (demais serviços):** os envelopes são poucos (login, loadRecords, saveRecord, removeRecord, relatório). Escrever com `XmlWriter`/`XmlReader` à mão (ou gerado pelo C1) elimina o `XmlSerializer` e o `Refit.Xml`. Isso resolve também a versão desalinhada do **#18** e o encoding do **#15** num lugar só.
- Ao fim, `<IsAotCompatible>true</IsAotCompatible>` no csproj, com os avisos de trimming zerados como critério de pronto.

Tamanho: M–G. Depende de C1 (a escrita/leitura do corpo da entidade vem do mapa gerado).

### C3 — `NullableState` imutável (**#16**)

`readonly struct` com `init`, fábricas `NullableState.Set(v)`, `NullableState<T>.Clear()`, `NullableState<T>.Unset`. Atribuição implícita de valor continua (`DescrProd = "X"` vira `Set`). Atribuição de `null` em tipo de referência passa a gerar aviso do analisador (SNK006: "use Clear() para apagar ou não atribua para manter"). Quebra quem escreve em `.Value`/`.State`, por isso fica na 6.0. Depende de C1 (analisador).

### C4 — Dependências e limpeza (**#18**, **#19**)

- Remover Swashbuckle (3 pacotes) e Azure.Core; referenciar explicitamente `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Http` e `Microsoft.Extensions.Options`.
- Apagar `XmlSerialable`, `IProxysable`, `SankhyaObjectKey`, `IModelBase`, o `NullableState` estático duplicado e as `GetFormattedString` repetidas.
- csproj: `GenerateDocumentationFile`, SourceLink, `snupkg`, build determinístico, `PackageRequireLicenseAcceptance=false`.
- README único (raiz) com URL base `/mge`, guia de migração 5.x → 6.0, generator e testes.

---

## Dependências entre itens

```
A1 (#5 exceção) ──> A3, A8, B4
A9 (cache / IModelMap) ──> A6, C1
B1 (HTTP) ──> B2 (DI) ──> B4 (sessão)
C1 (generator) ──> C2 (serialização), C3 (analisador do NullableState)
```

Paralelizável dentro de cada entrega: em A, {A2, A4, A5, A7, A8} não se tocam; em C, C4 pode andar junto com C1.

## Critério de pronto por entrega

- Todos os testes fake verdes, nenhum `Skip` de bug conhecido sobrando da entrega.
- Testes reais verdes na homologação (`./scripts/test-real.sh`), incluindo os de escrita quando houver entidade definida.
- Issues da entrega fechadas pelo PR (`Closes #N`).
- Changelog com o que mudou de comportamento (A7, A1) e, na 6.0, guia de migração.

## Decisões abertas (para você)

1. **Entidade para teste real de escrita** na homologação (A10, e testes de create/update/delete). Sugestão: uma tabela `AD_` criada só para teste, ou um parceiro/produto marcado e apagado no fim do teste.
2. **Target frameworks na 6.0:** só `net8.0`, ou `net8.0` + `net9.0`/`net10.0`. `netstandard2.0` não é recomendado, porque perde `DateOnly`/`TimeOnly`, `static abstract` e `ModuleInitializer`.
3. **Generator obrigatório ou opcional:** o plano mantém a reflexão como fallback (migração gradual). A alternativa é exigir `partial` na 6.0 e apagar o caminho por reflexão, que fica mais simples de manter mas obriga todo consumidor a mudar os modelos.
4. **Modo de sessão padrão na 6.0** (B4): manter `PorOperacao` (comportamento atual) ou passar a `Compartilhada`.
