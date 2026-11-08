# Protocolo do Sankhya: referência constatada

O que o servidor do Sankhya (`service.sbr`) faz de fato, constatado contra a homologação entre 2026-11-07 e 2026-11-08 e conferido com a documentação oficial onde ela existe. **Consulte este arquivo antes de pesquisar na web.** Quando algo novo for descoberto, acrescente aqui com a data e como foi verificado.

Legenda da coluna "Fonte":
- **H**: sondado na homologação com `scripts/sondar-sankhya.sh` ou por teste real;
- **T**: coberto por teste real em `tests/SankhyaAPI.Client.Tests/Real/`;
- **D**: documentação oficial, não sondado.

Ambiente sondado: Sankhya com SQL Server 2022. A URL e as credenciais ficam em `~/.config/sankhyaapi/teste.env` (permissão 600) e nunca entram no repositório.

## 1. Documentação oficial

- Índice da documentação (bom ponto de partida): https://developer.sankhya.com.br/llms.txt
- loadRecords: https://developer.sankhya.com.br/reference/get_loadrecords
- Critérios do loadRecords: https://developer.sankhya.com.br/reference/get_criteriosloadrecords
- loadView (sem paginação): https://developer.sankhya.com.br/reference/get_loadview
- **Não existe página do `DbExplorerSP.executeQuery`.** Tudo o que está na seção 5 foi sondado.
- Páginas que dão 404 (não insistir): `/reference/como-fazer-consulta-de-dados` e `/reference/executequery`.

## 2. Chamada, sessão e status

| Fato | Fonte |
|---|---|
| A URL base termina em `/mge`. Os serviços ficam em `{base}/service.sbr?serviceName=<Servico>` | H/T |
| Para receber JSON, acrescente `&outputType=json` na URL. **Sem isso, o executeQuery com erro responde em XML**, mesmo com o pedido em JSON | H/T |
| **Login:** `MobileLoginSP.login` com XML `<serviceRequest serviceName="MobileLoginSP.login"><requestBody><NOMUSU>u</NOMUSU><INTERNO>senha</INTERNO></requestBody></serviceRequest>`. A resposta traz `<jsessionid>` e `<idusu>` (este em base64 sobre ISO-8859-1) | H/T |
| A sessão vai no header `Cookie: JSESSIONID=<jsessionid>`, sem sufixo `.master` | H/T |
| **Logout:** `MobileLoginSP.logout` na mesma rota (`{base}/service.sbr`), com o mesmo cookie. Responde `status="1"` e encerra a sessão de fato | H/T |
| Um logout em rota errada (`/mge/mge/...`) ou com cookie sem `JSESSIONID=` responde HTTP 200 e **não** encerra a sessão: falha silenciosa | H |
| Atributo `status`: `1` = sucesso, `0` = erro, `3` = não autorizado (sessão expirada, encerrada ou inventada). Vale para XML e JSON | H/T |
| Com `status="3"`, a mensagem é "Não autorizado." | H |
| **Uma escrita com sessão morta não executa:** saveRecord e removeRecord voltam `status="3"` sem gravar nem apagar. Por isso, repetir depois de renovar a sessão é seguro | H |
| `status="1"` pode vir acompanhado de `statusMessage` (aviso), e isso não é erro | H |
| No XML, `statusMessage` vem em base64 sobre ISO-8859-1, às vezes dentro de `<![CDATA[...]]>` e com quebra de linha no fim. No JSON, vem em texto puro | H/T |
| `<tsError tsErrorCode="..." tsErrorLevel="...">` traz um `<statusMessage>` também em base64 | T (fake) |
| Uma requisição concorrente com sessões diferentes na mesma instância não se mistura (12 chamadas simultâneas) | T |
| **Duas requisições simultâneas na mesma sessão são recusadas**: "O serviço foi cancelado por situação de concorrência. Essa mesma sessão HTTP fez a requisição duas vezes simultaneamente." Uma sessão só atende uma requisição por vez; para paralelismo, use várias sessões (o pacote mantém um pool, `MaxSessions`) | H/T |

## 3. Encoding

| Fato | Fonte |
|---|---|
| O servidor lê o corpo XML **como UTF-8, qualquer que seja o charset declarado** (ISO-8859-1, UTF-8 ou nenhum, no header ou na declaração XML). Teste usado: `LEN(?) = 1` com `'Ã'` em UTF-8 | H |
| As respostas variam no charset declarado: o loadRecords declara UTF-8 e o logout e os erros, ISO-8859-1. Leia o charset de cada resposta, sem supor um único | H |
| Texto acentuado gravado pelo saveRecord chega intacto no banco (conferido por `UNICODE(SUBSTRING(...))` via executeQuery) | T |

## 4. loadRecords (`CRUDServiceProvider.loadRecords`)

Envelope XML:

```xml
<serviceRequest serviceName="CRUDServiceProvider.loadRecords"><requestBody>
  <dataSet rootEntity="Usuario" includePresentationFields="S" parallelLoader="false"
           disableRowsLimit="false" offsetPage="0" orderByExpression="this.CODUSU DESC">
    <entity path=""><field name="CODUSU"/><field name="NOMEUSU"/></entity>
    <criteria><expression>this.NOMEUSU LIKE ? AND this.CODUSU &gt; ?</expression>
      <parameter type="S">JO%</parameter><parameter type="I">0</parameter></criteria>
  </dataSet></requestBody></serviceRequest>
```

| Fato | Fonte |
|---|---|
| A resposta traz `<entities total="N">`, com `<metadata><fields><field name="..."/>` e `<entity><f0>...</f0><f1>...</f1></entity>`. `fN` é o índice na `<metadata>` | H |
| Com `includePresentationFields="S"`, campos de apresentação são **acrescentados no fim** (por exemplo `GrupoUsuario_NOMEGRUPO`). Mapeie as colunas pelo nome da `<metadata>`, nunca pela posição dos campos pedidos | H |
| `disableRowsLimit="true"` traz **tudo, sem limite**: 47.269 linhas em `Campo` (TDDCAM) e 10.077 em `Cidade` (TSICID). Cuidado com memória | H/T |
| Paginação: `disableRowsLimit="false"` + `offsetPage="N"` (começa em 0) traz **50 por página**. A resposta traz `hasMoreResult="true"`, `offsetPage` e `offset` (= N×50). Não há atributo de tamanho de página | H/T/D |
| A paginação passa do registro 5000 (página 100 ou mais) sem problema | H/T |
| Uma página depois do fim vem vazia, com `hasMoreResult` falso | T |
| `orderByExpression` ordena, separado do `<criteria>`. Aceita `DESC`. Não está documentado oficialmente | T |
| Parâmetros: um `?` na expressão para cada `<parameter type="X">`, em ordem. Tipos: `S` texto, `I` inteiro, `F` decimal com ponto, `D` data `dd/MM/yyyy`, `H` data e hora `dd/MM/yyyy HH:mm:ss` | T/D |
| **O parâmetro é valor, não SQL.** `' OR '1'='1`, `'; DELETE ...; --` e marcação XML viram texto e não afetam a consulta | T |
| Um parâmetro `I` com texto não numérico (`1 OR 1=1`) é recusado pelo servidor | T |
| Os valores das datas voltam como `dd/MM/yyyy HH:mm:ss`, os decimais com ponto, a hora (`T`) como número HHMM e a opção como o código | T |
| Sem registros: `<entities total="0">`, só com `<metadata>` | H |
| `useFileBasedPagination="true"`: **aceito e ignorado**. A resposta é byte a byte idêntica (Cidade, páginas 0, 1 e 150) e a página continua de 50. A página depois do fim (215) volta `total="0" hasMoreResult="false" offset="10750"`. `total` é o tamanho da página, não da tabela | H/T |
| `modifiedSince="2026-01-01T00:00:00"`: **erro** `status="0"`, `tsErrorCode="CORE_E07537"`, "Necessario habilitar o parametro 'LOGTABOPER'.", em qualquer data e entidade. Exige o parâmetro `LOGTABOPER` habilitado no Sankhya (não habilitado na homologação) | H/T |
| `ignoreCalculatedFields="true"`: sem mudança pedindo campos normais. O efeito sobre campos calculados não foi constatado | H/T |
| **Entidades ligadas:** `<entity path="UnidadeFederativa"><field name="UF"/></entity>` junto com `<entity path="">`. A coluna vem na `<metadata>` como `UnidadeFederativa_UF`, depois dos campos de `path=""`. O caminho é o nome da instância ligada (`TDDLIG`, `TIPLIGACAO='I'`; as de Cidade são Parceiro, UnidadeFederativa, Regiao, AliquotaISS...). `tryJoinedFields="true"` não muda nada. Caminho inexistente dá `CORE_E03405` ("Relacionamento: 'Foo' não encontrado em: 'Cidade'"); campo inexistente ou com ponto (`UnidadeFederativa.UF` em `path=""`) dá `CORE_E04064` ("Descritor do campo ... inválido"). Valores podem vir com espaços à direita | H |

## 5. executeQuery (`DbExplorerSP.executeQuery`)

Envelope JSON: `{"serviceName":"DbExplorerSP.executeQuery","requestBody":{"sql":"SELECT ..."}}`, sempre com `&outputType=json` na URL.

| Fato | Fonte |
|---|---|
| Resposta: `responseBody.fieldsMetadata` (`name`, `description`, `order` a partir de 1, `userType` I/S/...), `rows` (lista de listas), `burstLimit`, `timeQuery` e `timeResultSet` | H |
| **A posição da coluna é `order - 1`**, não a ordem em que o `fieldsMetadata` chega | T (fake) |
| **Teto fixo de 5000 linhas.** Acima disso o resultado é cortado em 5000 e vem `burstLimit: true`. Não há como aumentar | H/T |
| `maxRows` no `requestBody` **só reduz** o limite (`"maxRows":"10"` traz 10 linhas). Acima de 5000, continua cortando em 5000. **Com `maxRows`, o `burstLimit` vem falso mesmo havendo mais linhas**: para detectar, peça `n + 1` | H |
| Não há campo de ordenação, paginação ou parâmetros. `orderBy`, `orderByExpression`, `offset`, `offsetPage`, `page`, `pageSize`, `limit`, `rowsLimit` e `disableRowsLimit` são ignorados | H |
| Paginar só dentro do SQL: `... ORDER BY X OFFSET n ROWS FETCH NEXT m ROWS ONLY` funciona (SQL Server) | H/T |
| O SQL Server **recusa `ORDER BY` dentro de subconsulta** (tabela derivada) sem `TOP`/`OFFSET`. Embrulhar o SQL do usuário em `SELECT * FROM (...) q ORDER BY ... OFFSET` só funciona se o interno não tiver `ORDER BY` | H |
| SQL inválido: `status="0"` e `statusMessage` em texto puro, com a mensagem do banco | T |
| Os valores de `rows` vêm como tipos JSON. Com o `ObjectToInferredTypesConverter` do Refit, viram `long`, `string` e assim por diante, não `JsonElement` | T (fake) |
| O SQL roda com o poder do usuário de integração: concatenar entrada do usuário é injeção | — |
| O `requestBody` JSON aceita escape Unicode (`\u0027` para `'`): o `Utf8JsonWriter` padrão escapa aspas e o servidor lê corretamente | T |
| Data no SQL Server: `CONVERT(DATETIME, '2026-11-07T14:30:00', 126)` e `CONVERT(DATE, '2026-11-07', 23)` funcionam no executeQuery | T |

## 6. saveRecord e removeRecord

| Fato | Fonte |
|---|---|
| **Insert** (`CRUDServiceProvider.saveRecord`): `<dataSet rootEntity="X"><entity path=""><field name=.../></entity><dataRow><localFields><CAMPO>v</CAMPO></localFields></dataRow></dataSet>`. Os campos de chave auto-numerada são omitidos | H/T |
| **Update:** o mesmo envelope, com `<dataRow><localFields>...</localFields><key><CHAVE>v</CHAVE></key></dataRow>`. Campos ausentes ficam como estão. Um elemento vazio (`<CAMPO />`) apaga o valor | T |
| A resposta do saveRecord devolve as entidades gravadas (`<entities>`), inclusive a chave gerada | T |
| **Vários `<dataRow>` numa chamada só funciona:** 120 registros num único saveRecord | T |
| **Delete** (`CRUDServiceProvider.removeRecord`): `<requestBody><entity rootEntity="X"><id><CHAVE>v</CHAVE></id>...</entity></requestBody>`, com um `<id>` por registro e as várias partes da chave composta dentro dele | H/T |
| Chave composta com um campo auto-numerado (por exemplo NUNOTA + SEQUENCIA): no insert vai só a parte não numerada; no update e no delete, as duas | T (fake); falta real |

## 6.1 Erros (sondados em 2026-11-08)

Regra geral: **o Sankhya responde HTTP 200 em quase tudo**, inclusive login inválido e rota inexistente. O tipo do erro só aparece no `status`, no `tsErrorCode` (quando há) e no texto. Os erros do banco chegam com a mensagem crua do SQL Server, sem código. O pacote classifica cada erro em `SankhyaErrorKind`, com um status HTTP sugerido (`SankhyaErrorClassifier`).

| Cenário | Serviço | status | tsErrorCode | Mensagem (decodificada) | Categoria / HTTP |
|---|---|---|---|---|---|
| Senha errada | login | 0 | CORE_E01434 | Usuário/Senha inválido. | AuthenticationFailed / 401 |
| Usuário inexistente | login | 0 | CORE_E01428 | Usuário/Senha inválido. | AuthenticationFailed / 401 |
| Sessão expirada | qualquer | 3 | — | Não autorizado. | SessionExpired / 401 (renovada e repetida uma vez) |
| Requisições simultâneas na mesma sessão | qualquer | 0 | — | O serviço foi cancelado por situação de concorrência... | Concurrency / 409 |
| Entidade inexistente | loadRecords | 0 | — | Não foi encontrado objeto de acesso a dados para este BMP: mge-dwf:X | InvalidRequest / 400 |
| Campo inexistente | loadRecords | 0 | CORE_E04064 | Descritor do campo 'X' inválido. | InvalidRequest / 400 (Field) |
| Expressão inválida | loadRecords | 0 | — | Sintaxe incorreta próxima a '='. | InvalidRequest / 400 |
| Coluna inexistente na expressão | loadRecords | 0 | — | O identificador de várias partes "Usuario.X" não pôde ser associado. | InvalidRequest / 400 |
| Parâmetro `I` com texto | loadRecords | 0 | CORE_E02271 | Erro de conversão para número: abc | InvalidValue / 422 |
| Faltando parâmetro | loadRecords | 0 | — | Parametros informados insuficientes para o critério de consulta. | InvalidRequest / 400 |
| modifiedSince sem LOGTABOPER | loadRecords | 0 | CORE_E07537 | Necessario habilitar o parametro 'LOGTABOPER'. | NotConfigured / 501 |
| Chave duplicada | saveRecord | 0 | — | Violação da restrição PRIMARY KEY 'PK_AD_TESTE'. Não é possível inserir a chave duplicada... | DuplicateKey / 409 |
| Texto maior que a coluna | saveRecord | 0 | — | Propriedade 'AD_TESTE.TEXTO' com largura acima do limite: (150 > 100) | InvalidValue / 422 (Field) |
| Inteiro com texto | saveRecord | 0 | — | Erro de conversão de valor para o campo 'INTEIRO': abc | InvalidValue / 422 (Field) |
| Campo inexistente | saveRecord | 0 | — | Campo não existe: AD_TESTE->X | InvalidRequest / 400 (Field) |
| SQL com erro de sintaxe | executeQuery | 0 | — | O comando SELECT está em formato inválido. | InvalidRequest / 400 |
| Tabela ou coluna inexistente | executeQuery | 0 | — | Nome de objeto 'X' inválido. / Nome de coluna 'X' inválido. | InvalidRequest / 400 |
| UPDATE/DELETE/INSERT/DDL | executeQuery | 0 | — | Permitido apenas a execução de consultas, a instrução possui 'UPDATE', 'DELETE' ou 'INSERT'... | InvalidRequest / 400 |
| Divisão por zero | executeQuery | 0 | — | Erro de divisão por zero. | QueryFailed / 400 |
| RAISERROR na consulta | executeQuery | 0 | — | o texto do RAISERROR, cru | QueryFailed / 400 |

**Aceito sem erro, mas atenção:**

| Cenário | Comportamento constatado | Como o pacote trata |
|---|---|---|
| **Update de chave inexistente** | **cria o registro** (upsert), `status="1"` | `VerifyExistenceOnUpdate` (padrão ligado) consulta as chaves antes e lança `SankhyaNotFoundException` (404) |
| Delete de chave inexistente | `status="1"`, nada acontece | não detectável sem consulta extra |
| Insert sem a chave numa tabela de chave simples | o servidor **numera sozinho** (gerou ID 1, 2), mesmo sem a coluna marcada auto-numerada | o pacote exige a chave não auto-numerada; marque `AutoNumbered` quando o Sankhya numera |
| Data inválida (31/02/2026) e opção fora da lista (`Z`) | `status="1"`, gravado | sem validação no servidor: valide no domínio |
| Rota inexistente (`/mge/naoexiste.sbr`) | HTTP 200 | — |

**Notas e contatos (TGFCAB, TGFITE, TGFCTT), sondados em 2026-11-08:**

| Cenário | Resposta | Categoria |
|---|---|---|
| Trigger com RAISERROR (TGFCAB, data de negociação de 1900) | `status="0"`, "SQL-50001 Ano inferior ao limite permitido, veja o parâmetro de Limite Inferior para Ano. Nota de Nro Único: N." | BusinessRule (o pacote tira o prefixo `SQL-5xxxx`) |
| Validação do Sankhya (contato sem endereço) | "O preenchimento do endereço do contato é obrigatório." | BusinessRule |
| Empresa inexistente | `CORE_E00912` "Empresa não encontrada" | InvalidValue |
| TOP inexistente | `CORE_E00916` "TOP não encontrada." | InvalidValue |
| Tipo de venda inexistente (CODTIPVENDA 99999) | "Data truncation" | InvalidValue |
| Parceiro inexistente | "A instrução INSERT conflitou com a restrição do FOREIGN KEY "FK_TGFCAB_TGFPAR2"... column 'CODPARC'." | ConstraintViolation (Field = CODPARC) |
| `NUMNOTA` ausente no cabeçalho | "Não é possível inserir o valor NULL na coluna 'NUMNOTA'..." (o servidor não preenche; mande 0 em pedido) | ConstraintViolation |
| TIPMOV inválido (`Z`) | aceito, `status="1"` | sem validação no servidor |
| **Incluir item (ItemNota) pelo CRUD genérico** | `status="0"` com `<statusMessage></statusMessage>` **vazio** | Unknown, com a explicação. Item de nota exige o serviço de nota (`CACSP.incluirAlterarItemNota`), que **não existe no `/mge`** ("Nenhum provedor foi encontrado para o serviço") e cujo módulo `/mgecom` responde 302 nesta homologação |
| Excluir contato inexistente | `status="0"` "Contato não existe: PK[4,210]" (a AD_TESTE devolve `status="1"`; depende da entidade) | NotFound |
| Ler item de nota por chave composta (`NUNOTA`, `SEQUENCIA`) | funciona pelo loadRecords | — |
| Contato (`CODPARC` + `CODCONTATO`): insert sem `CODCONTATO` | o servidor numera e devolve a chave completa | chave composta com a parte auto-numerada verificada |

Uma exclusão chegou a levar mais de 100 s uma vez (e concluiu no servidor depois do timeout do cliente). Isso não se repetiu em outras execuções nem com duas sessões: foi lentidão intermitente da homologação. O timeout padrão de 100 s ficou.

**Trigger com regra de negócio:** não foi possível criar uma trigger na homologação (o executeQuery recusa DDL), então o caminho do saveRecord com RAISERROR não foi sondado. O que se viu: o banco devolve a mensagem crua (chave duplicada, RAISERROR na consulta), sem código. O pacote trata assim:
- toda recusa de saveRecord ou removeRecord que não bate com um erro técnico conhecido vira `BusinessRule` (422), com a mensagem da regra limpa;
- no SQL Server, sai a frase "A transação foi encerrada no gatilho...";
- no Oracle, `ORA-20xxx` (RAISE_APPLICATION_ERROR) também vira `BusinessRule`, e saem o código e a pilha `ORA-06512`.

Para confirmar, crie numa base de teste uma trigger em AD_TESTE com RAISERROR e rode `ErrosRealTests`.

## 7. Dicionário de dados e tipos

| Fato | Fonte |
|---|---|
| `TDDINS`: `NOMETAB` → `NOMEINSTANCIA` (nome da entidade). Uma tabela tem várias instâncias (`TSICID` → `Cidade`, `CidadeDestino`, ...). Tabela `AD_` criada pela tela fica com instância de mesmo nome | H |
| `TDDCAM`: `NOMETAB`, `NOMECAMPO`, `TIPCAMPO`, `TIPOAPRESENTACAO` (`P` padrão, `O` lista de opções), `DESCRCAMPO`, `NUCAMPO`. `TAMANHO` vem nulo, então o tamanho real só aparece em `INFORMATION_SCHEMA.COLUMNS` | H |
| `TDDOPC`: opções das listas (`NUCAMPO`, `VALOR`, `OPCAO`) | H |
| Tipos (`TIPCAMPO` → SQL Server → .NET): `S` → `varchar(n)` → `string`; `I` → `int` → `int`/`long`; `F` → **`float`** → `decimal`/`double`; `D` → `datetime` → `DateOnly`; `H` → `datetime` → `DateTime`; `T` → **`smallint` (HHMM)** → `TimeOnly`; lista de opções → `varchar(10)` + `TDDOPC` → `enum` com `[XmlEnum]` | H/T |
| Checkbox criado pela tela vira **lista de opções com S/N** (`TIPOAPRESENTACAO="O"`) | H |
| Os nomes de campo `DECIMAL` e `DATA` são recusados na criação (erro `CORE_E01912`, provavelmente palavra reservada); `DATAHORA`, `HORA`, `TEXTO` são aceitos | H |
| O `F` gravado como `float` faz `1234.56` voltar exato nos testes, mas é ponto flutuante: não use para valor monetário que exija precisão | T |

## 8. Dados da homologação usados nos testes

| Dado | Uso |
|---|---|
| `TSIUSU` / `Usuario`, `CODUSU = 0` (SUP) existe em toda base, com cerca de 719 usuários | testes de leitura |
| `TSICID` / `Cidade`: `COUNT(*)` = 10.078; o loadRecords traz 10.077 | testes de volume (só leitura) |
| `TDDCAM` / `Campo`: 47.269 linhas | sondagem de volume |
| `AD_TESTE` / `AD_TESTE`: `ID` (int, chave sem auto numeração), `TEXTO` varchar(100), `INTEIRO` int, `AD_DECIMAL` float, `AD_DATA` datetime, `DATAHORA` datetime, `HORA` smallint, `ATIVO` S/N, `SITUACAO` A/I/P. Criada só para testes de escrita, e os testes a deixam vazia | testes de escrita (`Category=RealEscrita`) |

## 9. Como sondar algo novo

```bash
./scripts/sondar-sankhya.sh sql "SELECT TOP 5 NOMECAMPO FROM TDDCAM"
./scripts/sondar-sankhya.sh xml CRUDServiceProvider.loadRecords < envelope.xml
./scripts/sondar-sankhya.sh morta-sql "SELECT 1"    # mesma chamada com uma sessão já encerrada
```

O script lê a URL e as credenciais do arquivo de credenciais, nunca as imprime e faz logout no fim. Escrita só na `AD_TESTE`.
