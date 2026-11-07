# Proposta 6.0 — arquitetura do SankhyaAPI.Client

Status: proposta para decisão (2026-11-07). Substitui as entregas B e C de `plano-correcao.md`, que viram fases desta proposta (seção 9).

## 1. Objetivo

Entregar **tudo o que a 5.2 faz hoje**, sem perder nenhuma funcionalidade, com os padrões atuais do .NET:

- o domínio fica livre de anotações e de tipos do pacote;
- o mapeamento fica numa classe de configuração por modelo, no estilo `IEntityTypeConfiguration<T>` do EF Core ou do `AbstractValidator<T>` do FluentValidation;
- injeção de dependência, `IHttpClientFactory`, options validadas, `CancellationToken` e `IAsyncEnumerable`;
- caminho para AOT por source generator.

A proposta resolve, por construção, os achados dos testes reais e da auditoria.

## 2. O que existe hoje e tem que continuar existindo

| # | Funcionalidade da 5.2 | Onde está hoje | Na 6.0 |
|---|---|---|---|
| F1 | Login e logout explícitos | `SessionService.LoginSankhya/LogoutSankhya` | `ISankhyaSession.LoginAsync/LogoutAsync` (e automático) |
| F2 | Sessão por operação (login → operação → logout) | `SessionService.Execute` | `ModoSessao.PorOperacao` |
| F3 | Busca por expressão crua | `FindAsync(string)` | `Consultar().Onde(string)`; a sobrecarga sem parâmetros fica com aviso de analisador |
| F4 | Busca parametrizada com ordenação | `FindAsync(expr, params, orderBy)` | `Consultar().Onde(expr, params).OrdenarPor(...)` |
| F5 | Paginação (50 por página, `hasMoreResult`) | `FindPageAsync` | `PaginaAsync(n)` e, novo, `StreamAsync()` |
| F6 | Insert de um ou vários registros, com o registro gravado de volta | `CreateAsync/CreateManyAsync` | `InserirAsync` (preenche a chave gerada no próprio objeto) |
| F7 | Update parcial: só campos `Set`, `Clear` apaga, `UnSet` preserva | `UpdateAsync/UpdateManyAsync` + `NullableState<T>` | três modos (seção 5.4), incluindo o próprio `NullableState<T>` |
| F8 | Delete de um ou vários | `DeleteAsync` | `ExcluirAsync` |
| F9 | Chave simples ou composta, com no máximo um campo auto-numerado | `[Key(nome, autoEnumerable)]` | `Chave(x => new { x.A, x.B }).AutoNumerada(x => x.B)` |
| F10 | Validações antes de chamar o servidor (chave obrigatória, auto-numerada vazia no insert, chave não `Clear`) | `ValidarCamposChave`, `ValidateNullableStateProperties` | as mesmas regras, verificadas no mapa e por diagnóstico de compilação |
| F11 | Conversão de tipos: texto, inteiro, decimal, data, data e hora, hora HHMM, S/N, enum por código | `ConvertForPropertyType`, `GetFormattedString` | conversores nomeados e extensíveis (seção 5.3) |
| F12 | Consulta SQL tipada e em dicionário | `QueryRawAsync<T>`, `QueryAsDictionaryAsync` | `ConsultarSqlAsync<T>`, `ConsultarSqlAsync` (dicionário) |
| F13 | Corte de 5000 linhas do executeQuery detectado | 5.2 lança `SankhyaException` | `SankhyaResultadoCortadoException` + leitura paginada (seção 5.6) |
| F14 | Relatório: gerar e baixar o arquivo | `RelatorioService.GerarRelatorio/ObterRelatorio` | `GerarRelatorioAsync` (uma chamada, sessão fechada no fim) |
| F15 | Erros tipados: `SankhyaException`, `SankhyaSessionExpiredException`, `FormatException` sem o valor | `Exceptions/` | mantidos, mais `SankhyaResultadoCortadoException` |
| F16 | `NullableState<T>` serializável em JSON (para DTOs de API) | `NullableStateConverterFactory` | mantido |
| F17 | Campos de apresentação (por exemplo `GrupoUsuario_NOMEGRUPO`) | `includePresentationFields="S"` + mapeamento por nome | campo só de leitura no mapa: `Apresentacao(x => x.NomeGrupo, "GrupoUsuario_NOMEGRUPO")` |
| F18 | Modelos 5.x com atributos | `[Key]`, `[XmlElement]`, `SankhyaModelBase` | continuam funcionando: o adaptador de atributos gera o mesmo mapa (seção 8) |

**Superfície de baixo nível pública hoje.** Cada grupo tem um destino:

| Grupo | Hoje | Na 6.0 | Por quê |
|---|---|---|---|
| Membros `protected` de `SessionService` (`LoadRequest`, `UpdateRequest`, `CreateRequest`, `DeleteRequest`, `Query`, `JSessionId`, `SessionIdAtual`) | base para serviços próprios que herdam de `SessionService` | mantidos na fachada `[Obsolete]`. O substituto é injetar `ISankhyaRepositorio<T>`, `ISankhyaSql` ou `ISankhyaSession` em vez de herdar | herança como extensão amarra o consumidor aos detalhes internos |
| `ISankhyaServiceClient`, `SankhyaApiClientFactory`, `DefaultClientService` | cliente Refit exposto | internos. Quem precisa de chamada livre ganha `ISankhyaTransport.EnviarAsync(servico, corpoXml)` | sai o Refit (5.1); a chamada livre continua possível sem expor o Refit |
| `LoadRecordsGeneric`, `SaveRecordsGeneric`, `RemoveRecordsGeneric`, `ExecuteQueryGeneric`, `EServiceNames`, classes de envelope (`ServiceRequest`, `ServiceResponse`, `DataSet`, `DataRow`, `Criteria`, `ResponseBody`, `Entities`...) | montagem de envelope à mão | internas. A montagem de envelope fica coberta pelo transporte | são detalhe de protocolo; mantê-las públicas congelaria o formato do XML |
| `Parameter` | parâmetro de criteria | mantido (com as fábricas da 5.2) | já é API de uso |
| Helpers públicos de `ObjectUtilsMethods` (`GetFormattedString`, `ConvertForPropertyType`, `GetXmlEnumValue`, `GetEnumValueFromXml`...) | conversão por reflexão | substituídos pelos conversores `SankhyaTipo` (públicos e extensíveis); os antigos ficam `[Obsolete]` | mesma função, sem reflexão |
| `LoginEntity`, `ResponseEntity` | retorno do login | `SessaoSankhya` (id da sessão, usuário, horário) | o `LoginEntity` guarda o código do usuário num membro privado inacessível |

Também saem da 5.2 sem substituto direto, porque estão quebrados ou sem uso (#7, #19): `AddSankhyaClient` atual, `XmlSerialable`, `IProxysable`, `SankhyaObjectKey`, `IModelBase` e as extensões de serialização genéricas (`SerializerServiceExtension`). O guia de migração explica o que usar no lugar.

## 3. Achados que a arquitetura precisa resolver

Constatados na homologação ou nos testes:

| Achado | Risco hoje | Como a 6.0 resolve |
|---|---|---|
| O loadRecords sem paginação traz **tudo** (47.269 linhas no dicionário, 10.077 cidades) | estouro de memória e timeout numa entidade grande | `StreamAsync()` pagina por baixo dos panos com `IAsyncEnumerable<T>`. A lista inteira só sai quando pedida explicitamente (`ListarAsync`) |
| O executeQuery corta em 5000 linhas (`burstLimit`) | dado incompleto | exceção dedicada e leitura paginada com `OFFSET/FETCH` gerado pelo pacote (5.6) |
| O executeQuery não aceita parâmetros | injeção de SQL no SQL cru | `SankhyaSql`: string interpolada que formata e escapa cada valor (5.6). O SQL concatenado à mão passa a gerar aviso |
| Sessão expirada = `status="3"` | operação falha sem nova tentativa | sessão compartilhada com renovação e uma única repetição (5.1) |
| Logout errado deixava a sessão aberta (#1) | sessões acumuladas no servidor | sessão com dono único (`IAsyncDisposable`) e logout testado |
| `CookieContainer` do `HttpClient` guardava o `JSESSIONID` (#8) | cookie de uma sessão vazando para outra | `UseCookies = false`: a sessão só vai no header que o pacote monta |
| O servidor lê o corpo como UTF-8, qualquer que seja o charset declarado | nenhum hoje (o 5.2 alinhou o header) | envelope escrito com `XmlWriter` em UTF-8 explícito, coberto pelo teste real com acento |
| A resposta traz colunas extras e a ordem pode variar | desalinhamento (#6) | leitura pela `<metadata>`, já na 5.2, mantida |
| Update parcial depende de estado no modelo | perder `NullableState` sobrescreveria campos | modos explícitos de alteração (5.4) |
| A hora vai como `smallint` HHMM | conversão manual espalhada | conversor `SankhyaTipo.Hora` |

## 4. Visão geral

```
            Aplicação (domínio sem anotações)
                 │
   ISankhyaRepositorio<T>  ←  SankhyaEntityMap<T> (mapa fluente) ─┐
   ISankhyaSql                                                    │ compila na inicialização
   ISankhyaRelatorios                                             ▼
                 │                                         IEntityMap<T>
                 ▼                                         (gerado por generator
            ISankhyaSession  (PorOperacao | Compartilhada)  ou compilado em runtime)
                 │
            ISankhyaTransport  (HttpClient via IHttpClientFactory,
                                envelopes XmlWriter/XmlReader, JSON com STJ source-gen)
```

São quatro camadas, cada uma testável sozinha. O `SankhyaFakeServer` dos testes vira um pacote à parte (`SankhyaAPI.Client.Testing`) para os consumidores testarem as próprias integrações.

## 5. Desenho

### 5.1 Transporte e sessão

- **Transporte.** Um `HttpClient` tipado por `AddHttpClient` (`IHttpClientFactory`), com `UseCookies = false`, timeout configurável (padrão de 100 s) e https obrigatório, salvo `PermitirHttp = true` explícito para desenvolvimento.
- **Sai o Refit.** São sete endpoints fixos. O Refit.Xml está desalinhado (#18), usa `XmlSerializer` por reflexão (o que impede AOT) e já causou o problema de charset. Os envelopes passam a ser escritos e lidos à mão com `XmlWriter`/`XmlReader`, e o JSON do executeQuery usa `JsonSerializerContext` (source generation do System.Text.Json).
- **Resiliência.** `Microsoft.Extensions.Http.Resilience` com nova tentativa só para falha transitória de transporte (5xx, timeout) e só em operações idempotentes (loadRecords, executeQuery, login). Insert e update não são repetidos automaticamente por falha de transporte, porque o servidor pode ter gravado.
- **Sessão.** `ISankhyaSession` tem dois modos:
  - `Compartilhada` (**padrão**, decisão de 2026-11-07): login sob demanda, protegido por `SemaphoreSlim` para que chamadas concorrentes façam um login só. Ao receber `SankhyaSessionExpiredException` (`status="3"`), renova a sessão e repete a operação **uma vez**. Repetir também a escrita é seguro nesse caso, e isso foi verificado na homologação: saveRecord e removeRecord com sessão expirada voltam `status="3"` sem gravar nem apagar nada. Logout no `DisposeAsync`. O registro no DI é singleton.
  - `PorOperacao`: o comportamento da 5.x (login e logout a cada operação), para quem precisa de isolamento total ou roda em job esporádico.
- **Login explícito (F1).** `await using var sessao = await sankhya.AbrirSessaoAsync();` serve para fluxos que precisam da mesma sessão em várias chamadas, como gerar relatório e baixar.

### 5.2 Mapeamento fluente

```csharp
// Domínio: POCO puro
public class ItemNota
{
    public long NumeroNota { get; set; }
    public int Sequencia { get; set; }
    public decimal Quantidade { get; set; }
    public DateTime? Entrega { get; set; }
    public StatusItem Status { get; set; }
    public string? NomeProduto { get; set; }   // vem de um campo de apresentação
}

public sealed class ItemNotaMap : SankhyaEntityMap<ItemNota>
{
    public ItemNotaMap()
    {
        Entidade("ItemNota");

        Chave(x => new { x.NumeroNota, x.Sequencia }, "NUNOTA", "SEQUENCIA")
            .AutoNumerada(x => x.Sequencia);          // no máximo um campo auto-numerado

        Campo(x => x.Quantidade, "QTDNEG");
        Campo(x => x.Status, "STATUS").Opcoes(("A", StatusItem.Aberto), ("F", StatusItem.Faturado));

        // Um campo do domínio em duas colunas (data + hora HHMM), com a volta explícita
        Campo(x => x.Entrega)
            .Grava("DTENTREGA", SankhyaTipo.Data)
            .Grava("HRENTREGA", SankhyaTipo.Hora)
            .Le("DTENTREGA", "HRENTREGA", (DateOnly? d, TimeOnly? h) => d?.ToDateTime(h ?? TimeOnly.MinValue));

        Apresentacao(x => x.NomeProduto, "Produto_DESCRPROD");   // só leitura
    }
}
```

Regras do mapa, validadas quando ele é montado (falha na inicialização, não na primeira chamada):

- **Chave** simples ou composta, com **no máximo um** campo auto-numerado. Se a chave for composta, o campo auto-numerado é preenchido pelo servidor e os outros são obrigatórios no insert. Isso preserva a F9 e a F10, já cobertas pelos testes `ChaveCompostaFakeTests`.
- **Campo calculado.**
  - `Grava` pode aparecer várias vezes: um campo do domínio pode gerar várias colunas.
  - `Le` recompõe o campo a partir de uma ou mais colunas.
  - Um campo só com `Grava` é só de escrita, e um só com `Le` é só de leitura. Isso fica explícito no mapa, nunca é deduzido.
  - Escrever e ler com conversões diferentes é o principal risco do campo calculado. Por isso cada mapa ganha um teste de ida e volta automático na suíte fake (`SankhyaMapTests.IdaEVolta<TMap>()`).
- **Campo de apresentação** só de leitura, pedido com `includePresentationFields`.
- **Coluna duplicada** no mapa é erro.

Os atributos da 5.x continuam aceitos. Um `AttributeEntityMap<T>` lê `[Key]`/`[XmlElement]` e produz o mesmo `IEntityMap<T>` (F18). O `ModelMap` interno da 5.2 é o embrião dele.

### 5.3 Conversores

`SankhyaTipo.Texto`, `.Inteiro`, `.Decimal`, `.Data`, `.DataHora`, `.Hora` (HHMM), `.SimNao` e `.Opcoes(...)`, todos verificados na homologação com a AD_TESTE. Eles formatam em cultura invariante, e o tipo é inferido pela propriedade quando não for informado. Conversores próprios entram por `IConversorSankhya<T>`. Erro de conversão continua sem o valor na mensagem (#12).

### 5.4 Update parcial sem perder o comportamento do `NullableState`

O domínio limpo não carrega o estado Set/UnSet/Clear. A proposta oferece três modos, todos produzindo o mesmo envelope:

1. **Campos explícitos (padrão):**
   ```csharp
   await itens.AtualizarAsync(item, c => c.Campo(x => x.Quantidade).Limpar(x => x.Entrega));
   ```
   Manda a chave, a quantidade e a entrega vazia, e mais nada. É o mais previsível e não depende de estado guardado.
2. **Rastreamento por cópia:** `var item = await itens.ObterParaAlterarAsync(chave);` guarda uma cópia do que foi lido, e `AtualizarAsync(item)` manda só o que mudou. Um campo que passou de valor a `null` vira `Clear`. É o mesmo princípio do EF Core.
3. **`NullableState<T>` no modelo**, como na 5.x, para quem preferir: o mapa reconhece propriedades `NullableState<T>` e usa o estado delas.

Atualizar **todos** os campos mapeados exige pedir explicitamente (`AtualizarTudoAsync`), porque sobrescreve alterações feitas por outros no servidor.

### 5.5 Consultas de entidade

```csharp
await foreach (ItemNota item in itens.Consultar()
                   .Onde("this.NUNOTA = ? AND this.QTDNEG > ?", nota, 0m)
                   .OrdenarPor(x => x.Sequencia)
                   .StreamAsync(ct))
{ ... }

SankhyaPage<ItemNota> pagina = await itens.Consultar().Onde(...).PaginaAsync(0, ct);
List<ItemNota> todos = await itens.Consultar().Onde(...).ListarAsync(ct);   // tudo em memória, explícito
ItemNota? um = await itens.ObterAsync(new { NumeroNota = 500L, Sequencia = 3 }, ct);
```

- Os valores de `Onde(expr, params object[])` viram `<parameter>`, com o tipo inferido (S/I/F/D/H). A injeção pelo criteria já foi testada: valores com SQL e com XML viram texto e nada é apagado.
- `OrdenarPor(x => x.Campo)` usa o nome da coluna do mapa, então a ordenação deixa de ser string livre e não aceita entrada do usuário.
- **Fase posterior, opcional:** um tradutor de expressões só para comparações simples (`Onde(x => x.Quantidade > q && x.Status == StatusItem.Aberto)`). Tradutor de LINQ completo fica fora: o custo é alto e o ganho é pequeno sobre a forma parametrizada.

### 5.6 SQL cru (executeQuery)

O executeQuery não tem parâmetros. A proposta é um handler de string interpolada:

```csharp
List<Linha> linhas = await sql.ConsultarAsync<Linha>($"SELECT CODPARC, NOMEPARC FROM TGFPAR WHERE NOMEPARC LIKE {busca + "%"} AND CODCID = {codCid}");
```

- Cada `{valor}` é formatado pelo tipo:
  - texto com aspas e `'` duplicado;
  - número e data em formato invariante;
  - `null` vira `NULL`.
- Tipo desconhecido é erro.
- `string` concatenada à mão no lugar do `SankhyaSql` gera aviso de analisador.
- **Risco:** escapar não é a mesma coisa que parametrizar. O escape cobre o SQL Server e o Oracle no que é padrão (`'` dobrado), mas o pacote passa a precisar saber o dialeto (`SankhyaClientOptions.Dialeto`, detectável no login). Por isso o caminho recomendado para filtrar por entrada do usuário continua sendo o loadRecords parametrizado.
- **Corte de 5000:** `SankhyaResultadoCortadoException` (subtipo de `SankhyaException`). `ConsultarPaginadoAsync(sql, ordenarPor, tamanho)` embrulha a consulta em `SELECT * FROM (...) q ORDER BY ... OFFSET n ROWS FETCH NEXT m ROWS ONLY` (no Oracle, `OFFSET ... FETCH` desde a 12c) e devolve `IAsyncEnumerable`. A ordenação é obrigatória, para a paginação não repetir nem pular linhas. Verificado na homologação: a forma embrulhada funciona, mas o SQL Server **recusa `ORDER BY` no SQL interno**. A regra fica assim: o SQL passado ao `ConsultarPaginadoAsync` não pode ter `ORDER BY`, a ordenação vai em `ordenarPor`, e o pacote rejeita antes de enviar quando encontrar `ORDER BY` fora de parênteses.

### 5.7 Relatórios

`GerarRelatorioAsync(int codigo, IEnumerable<ParametroRelatorio> parametros, CancellationToken)` abre a sessão, gera, baixa o arquivo para um `Stream` próprio e fecha a sessão no `finally` (#11). Devolve um `SankhyaArquivoRelatorio` com o `Stream` e os metadados que o Sankhya manda na `<chave>`: `ContentType`, `Formato`, `PermiteExportar`, `PodeEnviarPorEmail`. Os dois métodos atuais ficam como `[Obsolete]` na 6.0.

### 5.8 Configuração e DI

```csharp
builder.Services.AddSankhya(o =>
{
    builder.Configuration.GetSection("Sankhya").Bind(o);  // BaseUrl, Usuario, Senha (de user-secrets/vault), Timeout, ModoSessao
})
.AddMapas<ItemNotaMap>()                 // ou .AddMapasDoAssembly(typeof(ItemNotaMap).Assembly) (o generator gera a lista para AOT)
.AddRelatorios();

public class FaturamentoService(ISankhyaRepositorio<ItemNota> itens) { ... }
```

As options são validadas na inicialização (`ValidateOnStart`): URL absoluta terminada em `/mge`, https, usuário e senha presentes.

### 5.9 Observabilidade

- `ILogger` com eventos estruturados (`LoggerMessage` gerado): serviço, entidade, duração e status. Nunca valores de campo nem SQL com valores.
- `ActivitySource("SankhyaAPI.Client")` para OpenTelemetry, com um span por chamada.
- `Meter` para métricas: chamadas, erros por status, logins e renovações de sessão.

### 5.10 AOT e source generator

- O mapa fluente em runtime já tira a reflexão do caminho quente: é compilado uma vez em delegates.
- O generator (`SankhyaAPI.Client.Generators`, empacotado em `analyzers/`) lê as classes `SankhyaEntityMap<T>` e gera o `IEntityMap<T>` sem reflexão nem `Expression.Compile`, o que deixa o pacote marcado como `IsAotCompatible`.
- Ele entende lambdas de acesso a membro e os conversores do pacote. Uma lambda arbitrária num `Le(...)` continua funcionando, mas emite o diagnóstico SNK010 ("não compatível com AOT").

Diagnósticos:

| Código | Regra |
|---|---|
| SNK001 | mais de um campo auto-numerado na chave |
| SNK002 | coluna repetida no mapa |
| SNK003 | campo do domínio sem conversor conhecido |
| SNK004 | campo com `Grava` e sem `Le` (ou o contrário) sem `.SoEscrita()`/`.SoLeitura()` explícito |
| SNK005 | SQL cru montado por concatenação (use `SankhyaSql`) |
| SNK006 | atribuição de `null` em `NullableState<T>` (use `Clear()`) |
| SNK010 | mapeamento não compatível com AOT |

## 6. Testes

- **Fake (CI):**
  - os 47 testes atuais, portados;
  - ida e volta de cada mapa;
  - paridade entre o mapa fluente, o de atributos e o gerado, com o envelope XML idêntico byte a byte;
  - snapshot do código gerado;
  - um caso que dispara e outro que não dispara para cada SNK.
- **Reais (homologação, opt-in):** os 39 atuais, mais:
  - `StreamAsync` sobre TSICID inteira, conferindo a contagem;
  - `ConsultarPaginadoAsync` passando de 5000;
  - sessão compartilhada com 50 chamadas concorrentes e um único login;
  - renovação após logout forçado;
  - relatório, se houver um relatório de teste.
- **`SankhyaAPI.Client.Testing`:** o servidor fake publicado como pacote.

## 7. Riscos

| Risco | Prob. | Impacto | Mitigação |
|---|---|---|---|
| Reescrita do transporte (sem Refit) quebra algo que a 5.x fazia | média | alto | paridade de envelope byte a byte contra a 5.2 na suíte fake, mais os testes reais |
| Campo calculado com ida e volta inconsistente | média | médio | teste automático de ida e volta por mapa e SNK004 |
| SQL do usuário com `ORDER BY` em `ConsultarPaginadoAsync` (recusado pelo SQL Server dentro da subconsulta) | alta | baixo | validação antes de enviar, mensagem dizendo para usar `ordenarPor` |
| Escape de SQL não cobre um caso do dialeto | baixa | alto | lista branca de tipos, testes de injeção reais nos dois dialetos e documentação apontando o loadRecords parametrizado como caminho seguro |
| Sessão compartilhada: um logout ou expiração no meio de várias chamadas | média | médio | renovação sob semáforo e uma única repetição; teste concorrente real |
| Streaming de muitas páginas com dado mudando no meio | média | baixo | ordenação estável obrigatória; documentar que não é snapshot |
| Escopo grande atrasa a 6.0 | alta | médio | fases com previews publicáveis (seção 9); a 5.x recebe correções enquanto isso |
| Consumidores com modelos 5.x | certa | médio | adaptador de atributos e `BaseService<T>` como fachada `[Obsolete]` sobre o núcleo novo até a 7.0 |

## 8. Compatibilidade e migração

- **6.0:** `BaseService<T>`, os atributos e o `NullableState<T>` continuam, implementados sobre o núcleo novo e marcados `[Obsolete]` com a mensagem de migração. O código 5.x compila com avisos. Só quebra o que hoje já está quebrado ou é interno: os construtores `(baseUrl, user, password)` passam a ter sobrecarga por DI, e o `AddSankhyaClient` antigo sai.
- **7.0:** remove a fachada.
- O guia de migração mostra cada F1–F18, lado a lado: 5.x → 6.0.

## 9. Fases (substituem B e C)

| Fase | Versão | Conteúdo | Pronto quando |
|---|---|---|---|
| P1 Núcleo | 6.0.0-preview.1 | transporte sem Refit, sessão (dois modos), options e DI, `CancellationToken`, logs/trace, relatório atômico. `BaseService` como fachada | todos os testes atuais passam pela fachada; paridade de envelope |
| P2 Mapa fluente | preview.2 | `SankhyaEntityMap<T>`, conversores, chave composta, campos calculados e de apresentação, adaptador de atributos, update parcial (3 modos) | ida e volta por mapa; paridade fluente × atributos |
| P3 Consultas | preview.3 | `Consultar()` com `StreamAsync`/`PaginaAsync`/`ListarAsync`, `SankhyaSql`, `ConsultarPaginadoAsync`, exceção de corte | testes reais de volume e de injeção |
| P4 Generator e AOT | preview.4 | generator, diagnósticos SNK, STJ source-gen, `IsAotCompatible` | zero avisos de trimming; app de exemplo publicado com AOT |
| P5 Limpeza | 6.0.0 | dependências (#18), código morto (#19), pacote Testing, README e guia de migração | checklist F1–F18 conferido |

Issues: P1 fecha #7, #8, #9, #10 e #11. P2 fecha #16 (com o `NullableState` imutável como modo 3) e parte de #17. P4 fecha #17. P5 fecha #18 e #19.

## 10. Decisões para você

1. **Tirar o Refit** (recomendado: sim, pelo AOT, pelo #18 e porque são só sete endpoints) ou mantê-lo no transporte.
2. **Modo padrão de update parcial:** campos explícitos (recomendado) ou rastreamento por cópia.
3. **`SankhyaSql` com escape** (recomendado: sim, com aviso de que o loadRecords parametrizado é o caminho seguro) ou proibir valores no SQL cru.
4. **Target frameworks:** `net8.0` só, ou `net8.0` + `net10.0` (LTS).
5. **Fachada 5.x até a 7.0** (recomendado) ou quebra direta na 6.0.
6. **Nomes da API nova:** este documento usa nomes em português (`InserirAsync`, `Consultar`, `AtualizarAsync`) por coerência com o domínio Sankhya, mas a 5.x usa inglês (`FindAsync`, `CreateAsync`). As alternativas são português, inglês (`InsertAsync`, `Query`, `UpdateAsync`) ou manter os nomes da 5.x onde a operação é a mesma. Os exemplos acima são só ilustrativos até essa decisão.

Verificado só no fake, a confirmar na P2: chave composta com um campo auto-numerado. Os testes `ChaveCompostaFakeTests` cobrem insert, update e delete, mas falta uma tabela real com chave composta para o teste de homologação.
