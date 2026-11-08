# Setup inicial

## 1. Instalar

```bash
dotnet add package SankhyaAPI.Client
dotnet add <ProjetoDeTestes> package SankhyaAPI.Client.Testing   # opcional, para testes
```

O source generator e os analisadores (SNK001–SNK010) vêm dentro do pacote. Não é preciso instalar nada a mais.

## 2. Credenciais fora do código

```bash
dotnet user-secrets init
dotnet user-secrets set "Sankhya:Usuario" "integracao"
dotnet user-secrets set "Sankhya:Senha" "<senha>"
```

`appsettings.json` (sem senha):

```json
{
  "Sankhya": {
    "BaseUrl": "https://erp.empresa.com.br/mge",
    "SessionMode": "Shared",
    "MaxSessions": 4,
    "Timeout": "00:01:40"
  }
}
```

Em produção: variáveis de ambiente (`Sankhya__Senha`) ou cofre (Key Vault, 1Password, Vault).

## 3a. Com injeção de dependência (ASP.NET Core, Worker)

```csharp
using SankhyaAPI.Client;

builder.Services.AddSankhya(o => builder.Configuration.GetSection("Sankhya").Bind(o))
                .AddEntity<Produto, ProdutoMap>()        // modelo + mapa fluente
                .AddRepository<Usuario>();               // modelo mapeado por atributos ([SankhyaEntity])
```

Injete onde precisar:

```csharp
public class ProdutoService(ISankhyaRepository<Produto> produtos, ISankhyaSql sql, ISankhyaReports relatorios) { ... }
```

Também ficam registrados `SankhyaClient`, `ISankhyaSession` e `ISankhyaTransport`. As opções são validadas na inicialização (`ValidateOnStart`): uma URL sem `/mge`, um `http` sem `AllowHttp` ou uma senha vazia derrubam a aplicação na subida, com a lista de problemas.

## 3b. Sem injeção de dependência (console, job, script)

```csharp
using SankhyaAPI.Client;
using SankhyaAPI.Client.Configuration;
using SankhyaAPI.Client.Mapping;

SankhyaMaps.Register<ProdutoMap>();   // uma vez por processo

await using SankhyaClient sankhya = SankhyaClient.Create(new SankhyaClientOptions
{
    BaseUrl = "https://erp.empresa.com.br/mge",
    Usuario = Environment.GetEnvironmentVariable("SANKHYA_USER")!,
    Senha = Environment.GetEnvironmentVariable("SANKHYA_PASSWORD")!
});

var produtos = sankhya.Repository<Produto>();
```

## 4. Opções

| Opção | Padrão | Quando mudar |
|---|---|---|
| `BaseUrl` | obrigatória | URL do Sankhya terminada em `/mge` |
| `Usuario`, `Senha` | obrigatórias | usuário de integração |
| `SessionMode` | `Shared` | `PerOperation` para login e logout a cada operação (jobs raros, isolamento total) |
| `MaxSessions` | 4 | máximo de chamadas paralelas (uma sessão por chamada) |
| `Timeout` | 100 s | por tentativa de chamada |
| `MaxRetries` | 2 | novas tentativas em falha transitória, só em leitura e login |
| `VerifyExistenceOnUpdate` | `true` | desligue só se o upsert do saveRecord for desejado |
| `Dialect` | `SqlServer` | `Oracle` quando o Sankhya roda em Oracle (afeta datas no SQL cru) |
| `AllowHttp` | `false` | só em desenvolvimento |

## 5. Próximo passo

Mapeie cada tabela usada ([`mapeamento.md`](mapeamento.md)). Para descobrir o **nome da entidade** (instância) e os campos de uma tabela, consulte o dicionário do próprio Sankhya:

```csharp
var instancias = await sql.QueryAsDictionaryAsync($"SELECT NOMEINSTANCIA FROM TDDINS WHERE NOMETAB = {"TGFPRO"}");
var campos = await sql.QueryAsDictionaryAsync($"SELECT NOMECAMPO, TIPCAMPO, DESCRCAMPO FROM TDDCAM WHERE NOMETAB = {"TGFPRO"}");
```

`TIPCAMPO`: `S` texto, `I` inteiro, `F` decimal, `D` data, `H` data e hora, `T` hora (HHMM). Campo com lista de opções: veja `TDDOPC`.

**Campos obrigatórios** ficam no dicionário, não só no banco. Antes de montar um insert, liste os dois e mapeie todos:

```csharp
var obrigatorios = await sql.QueryAsDictionaryAsync(
    $"SELECT C.NOMECAMPO FROM TDDCAM C JOIN TDDPCO P ON P.NUCAMPO = C.NUCAMPO WHERE C.NOMETAB = {"TGFPRO"} AND P.NOME IN ('requerido', 'required') AND P.VALOR IN ('S', 'true')");
var notNull = await sql.QueryAsDictionaryAsync(
    $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = {"TGFPRO"} AND IS_NULLABLE = 'NO' AND COLUMN_DEFAULT IS NULL");
```

Um campo obrigatório só no dicionário não dá erro de banco: o Sankhya recusa, às vezes **sem mensagem** (o pacote lança `Unknown` explicado).

## 6. Observabilidade (opcional)

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("SankhyaAPI.Client"))
    .WithMetrics(m => m.AddMeter("SankhyaAPI.Client"));
```

Logs, spans e métricas nunca levam valor de campo, parâmetro ou SQL.
