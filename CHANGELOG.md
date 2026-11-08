# Changelog

## 6.0.0 (em desenvolvimento)

Versão com quebra de API, sem camada de compatibilidade com a 5.x. O guia de migração fica em `docs/migracao-6.0.md`.

### Núcleo (P1)

- **Transporte próprio, sem Refit** (#30, #18). `HttpClient` pelo `IHttpClientFactory`, sem `CookieContainer`: a sessão só vai no header que o pacote monta (#8). Timeout por chamada (padrão de 100 s) e https obrigatório, salvo `AllowHttp` para desenvolvimento. Envelopes XML com LINQ to XML/`XmlWriter` em UTF-8 e JSON com `Utf8JsonWriter`/`JsonDocument`, sem `XmlSerializer` nem serialização por reflexão.
- **`SankhyaClient`** (`SankhyaClient.Create(options)` ou `services.AddSankhya(...)`) com `Session`, `Repository<T>(entidade)`, `Sql`, `Reports` e `Transport` (#7).
- **Opções validadas na inicialização** (`SankhyaClientOptions`, `ValidateOnStart`): URL absoluta terminada em `/mge`, https, usuário e senha.
- **Sessão** (#9). O padrão é `Shared`, um pool de até `MaxSessions` sessões reaproveitadas, uma operação por vez em cada, porque o Sankhya recusa requisições simultâneas na mesma sessão. Sessão expirada (`status="3"`) é renovada e a operação repetida uma vez. Logout no `DisposeAsync`. `PerOperation` mantém o comportamento da 5.x. `Session.LoginAsync/LogoutAsync` servem para sessão explícita.
- **`CancellationToken`** em todos os métodos públicos (#10).
- **Relatórios** (#11). `Reports.GenerateAsync(codigo, parametros)` gera e baixa o arquivo na mesma sessão e devolve `SankhyaReportFile` com conteúdo, `ContentType`, formato e permissões.
- **Resiliência** (#32): nova tentativa só em falha transitória de transporte e só em operações idempotentes (login, loadRecords, executeQuery). Escritas nunca se repetem por falha de transporte.
- **Observabilidade** (#31): logs estruturados (`LoggerMessage`), `ActivitySource` e `Meter` `SankhyaAPI.Client`, sem valores de campo, parâmetro ou SQL.
- **Target frameworks** `net8.0` e `net10.0`. Saem as dependências Refit, Refit.Xml, Refit.HttpClientFactory, Azure.Core e Swashbuckle.
- `SankhyaTruncatedResultException` para o corte de 5000 linhas do executeQuery.

### Mapa e modelo (P2)

- **Mapa fluente por modelo** (#24): `SankhyaEntityMap<T>` com `Entity`, `Key(...).AutoNumbered()`, `Property(...)` (com `.Options(...)` e `.ReadOnly()`), `Computed(...)` (um campo do domínio em várias colunas, com `.Writes(coluna, SankhyaType)` e leitura composta explícita por `.Reads(...)`), `Joined(...)` (campo de entidade ligada, `<entity path>`) e `Presentation(...)` (campos de apresentação). O domínio fica sem atributos nem tipos do pacote. Registre com `SankhyaMaps.Register<TMap>()` ou `AddEntity<T, TMap>()`.
- **Conversores nomeados** `SankhyaType` (Text, Integer, Decimal, Date, DateTime, Time em HHMM, YesNo, Options) e `ISankhyaConverter<T>` para tipos próprios. Nenhuma conversão depende da cultura da máquina.
- **Atributos como alternativa**: `[SankhyaEntity]`, `[Key]`, `[XmlElement]` e `[SankhyaPresentation]` produzem o mesmo mapa (paridade de envelope testada byte a byte).
- **Chave composta** com no máximo um campo auto-numerado, validada na montagem do mapa (#26). No insert, a chave gerada é preenchida no próprio objeto.
- **Update parcial em três modos** (#25): campos explícitos (`UpdateAsync(e, u => u.Set(x => x.A).Clear(x => x.B))`), rastreamento por cópia (`Track(e)` + `UpdateAsync(e)`) e `NullableState<T>` no modelo. `UpdateAllAsync` manda tudo, só quando pedido.
- **`NullableState<T>` imutável** (#16): `readonly struct` com `Set(v)`, `Clear()`, `Unset`; JSON pelo valor puro.
- `Repository<T>()` resolve o nome da entidade pelo mapa ou por `[SankhyaEntity]`. `SankhyaModelBase` deixa de existir.

### Consultas (P3)

- **Consulta fluente** `repo.Query()`: `Where(expressão, valores...)` com tipo inferido e contagem de `?` conferida, `OrderBy`/`OrderByDescending` pela coluna mapeada, `ToListAsync`, `ToPageAsync`, `FirstOrDefaultAsync` e **`StreamAsync`** (`IAsyncEnumerable` paginado, sem carregar tudo em memória) (#27).
- **`GetAsync([chave...])`** pela chave simples ou composta.
- **Atributos do loadRecords** (#28): `ModifiedSince` (exige `LOGTABOPER` no Sankhya; sem ele, `CORE_E07537`), `IgnoreCalculatedFields`, `FileBasedPagination`. As entidades ligadas já estão no mapa (`Joined`). Comportamento sondado na homologação e registrado em `docs/sankhya-protocolo.md`.
- **SQL cru seguro** (#29): `ISankhyaSql` recebe `SankhyaSql`, uma string interpolada que escapa e formata cada valor pelo tipo (dialeto SQL Server ou Oracle em `SankhyaClientOptions.Dialect`). `SankhyaSql.Raw` serve para trechos fixos.
- **`QueryPagedAsync`** percorre resultados de qualquer tamanho com `OFFSET ... FETCH` e recusa SQL com `ORDER BY` próprio. **`QueryTopAsync`** limita com `maxRows` e avisa se há mais linhas (pede uma a mais, porque o servidor não marca `burstLimit` com `maxRows`).
- Números inteiros do executeQuery voltam como `long` no dicionário (antes, `decimal`).

## 5.2.0

Correções da auditoria (entrega A de `docs/plano-correcao.md`). Nenhuma assinatura pública mudou, mas há mudanças de comportamento, marcadas com ⚠.

### Novo

- `FindAsync(expression, parameters, orderBy)`: filtro com `?` e valores enviados como `<parameter>`, fora da expressão. Fábricas `Parameter.Texto/Inteiro/Decimal/Data/DataHora` (#2).
- `FindPageAsync(expression, parameters, page, orderBy)`, que devolve `SankhyaPage<T>` (`Items`, `Page`, `HasMoreResult`), com 50 registros por página (#2).
- `IBaseService<T>` expõe os dois métodos acima, com corpo padrão para não quebrar implementações próprias da interface.
- `SankhyaException` (`Status`, `ServiceName`, `TsErrorCode`) e `SankhyaSessionExpiredException` (`status="3"`), no namespace `SankhyaAPI.Client.Exceptions` (#5).

### Corrigido

- O logout de fato encerra a sessão. Antes ele chamava `/mge/mge/service.sbr` com um cookie sem `JSESSIONID=` e a sessão continuava aberta no servidor (#1).
- `QueryRawAsync` e `QueryAsDictionaryAsync` usam o `order` de cada coluna e não trocam mais colunas quando o `fieldsMetadata` vem fora de ordem. Coluna repetida lança `InvalidOperationException` com o nome dela (#3).
- `TsError.StatusMessage` traz a mensagem decodificada em vez de `System.Byte[]` (#4).
- A leitura de `<entities>` mapeia cada coluna pelo nome (pela `<metadata>` da resposta ou pelos campos pedidos). Um campo omitido ou fora de ordem não desalinha mais os seguintes, e a leitura para em `</entities>` (#6).
- Um `idusu` vazio ou inválido no login não lança mais `FormatException` (#14).
- O XML enviado declara `charset=UTF-8`, que é a codificação real do corpo. Antes, loadRecords e removeRecord declaravam ISO-8859-1. A homologação lê o corpo como UTF-8 nos dois casos, então não havia corrupção ali (#15).
- ⚠ `QueryRawAsync` e `QueryAsDictionaryAsync` lançam `SankhyaException` quando o servidor corta o resultado no limite de 5000 linhas do executeQuery (`burstLimit`). Antes, as 5000 linhas eram devolvidas como se fossem o resultado inteiro. Para mais linhas, pagine com `ORDER BY ... OFFSET ... FETCH NEXT ...` ou use `FindPageAsync`. O loadRecords (`FindAsync`) não tem esse limite.
- O executeQuery pede `outputType=json`. Antes, com SQL inválido, o servidor respondia em XML e a mensagem do erro se perdia num `HttpRequestException`.
- Reflexão em cache por tipo (`ModelMap`), sem `dynamic` e sem busca linear por campo a cada linha (primeira parte do #17).

### ⚠ Mudanças de comportamento

- **O erro é decidido pelo `status` da resposta** (#5). Uma resposta com `status` diferente de `1` lança exceção mesmo sem mensagem. Uma resposta com `status="1"` e mensagem não lança mais. As exceções passaram de `Exception` para `SankhyaException`: `catch (Exception)` continua funcionando, mas um teste com `Assert.ThrowsAsync<Exception>` (tipo exato) precisa ser trocado.
- **Insert exige as chaves sem auto numeração** (#13). Uma chave `[Key("X")]` sem `autoEnumerable` precisa estar preenchida (`Set`) no `CreateAsync`/`CreateManyAsync`, como o README já dizia. Antes o insert ia para o servidor sem ela.
- **Mensagens de erro de conversão sem o valor do campo** (#12). A conversão lança `FormatException` com o nome e o tipo da propriedade. O valor fica só em `exception.Data["Valor"]` (pode ser dado pessoal e mensagens vão para log).
- **O envelope só pede campos mapeados.** Propriedades sem `[XmlElement]`/`[Key]` não geram mais `<field />` sem nome no loadRecords/saveRecord.
