# Changelog

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
