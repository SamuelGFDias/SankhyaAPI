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
| Atributos documentados e **ainda não sondados**: `useFileBasedPagination="true"` (paginação em disco, indicada para mais de 1000 páginas), `modifiedSince="2024-04-16T12:59:59"` (só registros alterados; depende do log de alterações de tabelas), `ignoreCalculatedFields="true"`, `tryJoinedFields="true"` e `entity` com `path` de entidade ligada (`"Cidade"`, `"GrupoProduto"`) | D |

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

## 6. saveRecord e removeRecord

| Fato | Fonte |
|---|---|
| **Insert** (`CRUDServiceProvider.saveRecord`): `<dataSet rootEntity="X"><entity path=""><field name=.../></entity><dataRow><localFields><CAMPO>v</CAMPO></localFields></dataRow></dataSet>`. Os campos de chave auto-numerada são omitidos | H/T |
| **Update:** o mesmo envelope, com `<dataRow><localFields>...</localFields><key><CHAVE>v</CHAVE></key></dataRow>`. Campos ausentes ficam como estão. Um elemento vazio (`<CAMPO />`) apaga o valor | T |
| A resposta do saveRecord devolve as entidades gravadas (`<entities>`), inclusive a chave gerada | T |
| **Vários `<dataRow>` numa chamada só funciona:** 120 registros num único saveRecord | T |
| **Delete** (`CRUDServiceProvider.removeRecord`): `<requestBody><entity rootEntity="X"><id><CHAVE>v</CHAVE></id>...</entity></requestBody>`, com um `<id>` por registro e as várias partes da chave composta dentro dele | H/T |
| Chave composta com um campo auto-numerado (por exemplo NUNOTA + SEQUENCIA): no insert vai só a parte não numerada; no update e no delete, as duas | T (fake); falta real |

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
