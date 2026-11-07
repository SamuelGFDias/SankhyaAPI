
# 👜 SankhyaAPI.Client

![GitHub issues](https://img.shields.io/github/issues/SamuelGFDias/SankhyaAPI)
![GitHub forks](https://img.shields.io/github/forks/SamuelGFDias/SankhyaAPI)
![GitHub stars](https://img.shields.io/github/stars/SamuelGFDias/SankhyaAPI)
![GitHub license](https://img.shields.io/github/license/SamuelGFDias/SankhyaAPI)
![GitHub Languages](https://img.shields.io/github/languages/top/SamuelGFDias/SankhyaAPI)
![Nuget](https://img.shields.io/nuget/v/SankhyaAPI.Client)

Projeto de integração com ERP Sankhya para operação de CRUD!

## 📑 Índice
- [Sobre](#sobre)
- [Instalação](#instalacao)
- [Configuração](#configuracao)
- [Funcionalidades](#funcionalidades)
- [Uso](#uso)
- [Licença](#licenca)

## 🚀 Sobre

Este projeto tem como objetivo disponibilizar, de maneira mais sucinta, métodos mais utilizados para manipulações e retornos de dados. Utilizando a própria API do Sankhya disponível [aqui](https://developer.sankhya.com.br/reference/api-de-integra%C3%A7%C3%B5es-sankhya), o projeto abstrai os métodos para o usuário.

## 📦 Instalação

1. Na raiz do seu projeto, use o seguinte comando:
   ```bash
   dotnet add package SankhyaAPI.Client --version x.y.z
   ```

## ⚙ Configuração

Este pacote foi pensado para uso com injeção de dependência.

1. Configure o `SankhyaClientSettings`:
   ```csharp
   SankhyaClientSettings sankhyaSettings = new SankhyaClientSettings();
   sankhyaSettings.BaseUrl = "https://seusistema.sankhya.com";
   sankhyaSettings.Usuario = "usuario";
   sankhyaSettings.Senha = "senha";
   builder.Services.Configure<SankhyaClientSettings>(sankhyaSettings);
   ```

2. Injete o serviço:
   ```csharp
   builder.Services.AddSankhyaClient();
   ```

## 🧠 Validação de modelos

Todos os modelos devem herdar de `SankhyaModelBase`. Todas as propriedades devem ser `NullableState<T>`. As chaves primárias (com atributo `[Key]`) devem estar com:

- `AutoEnumerable == true`: obrigatoriamente `UnSet`
- `AutoEnumerable == false`: obrigatoriamente `Set`
- Nunca podem estar com `Clear`

## ⚙️ Funcionalidades

1. Exemplo de entidade:
   ```csharp
   public class ProdutoEntity : SankhyaModelBase
   {
       [Key("CODPROD", true)] public NullableState<long> CodProd { get; set; }
       [XmlElement("CODVOL")] public NullableState<string> CodVol { get; set; }
       [XmlElement("DESCRPROD")] public NullableState<string> DescrProd { get; set; }
       [XmlElement("REFFORN")] public NullableState<string> RefForn { get; set; }
   }
   ```

2. Serviço da entidade usando nome da entidade como string:
   ```csharp
   public class ProdutoClientService : BaseService<ProdutoEntity>
   {
       public ProdutoClientService(IOptions<SankhyaClientSettings> sankhyaApiConfig)
           : base(sankhyaApiConfig, "Produto") { }
   }
   ```

## 🧪 Uso

### Inserir

```csharp
var produtos = new List<ProdutoEntity>
{
    new() { CodProd = 1, CodVol = "UN", DescrProd = "Produto A", RefForn = "F123" },
    new() { CodProd = 2, CodVol = "UN", DescrProd = "Produto B", RefForn = "F456" }
};
var response = await produtoClientService.CreateManyAsync(produtos);
```

### Atualizar

```csharp
produtos[0].DescrProd = "Produto A Atualizado";
var response = await produtoClientService.UpdateManyAsync(produtos);
```

### Buscar registros com FindAsync

```csharp
string query = "this.CODPROD = 1 AND this.AD_ATIVO = 'S'";
var produtos = await produtoClientService.FindAsync(query);
```

### Query tipada com QueryRawAsync

```csharp
string script = "SELECT CODPROD, DESCRPROD FROM TGFPRO WHERE ATIVO = 'S'";
List<ProdutoEntity> resultado = await produtoClientService.QueryRawAsync<ProdutoEntity>(script);
```

### Query como dicionário com QueryAsDictionaryAsync

```csharp
string script = "SELECT CODPROD, DESCRPROD FROM TGFPRO WHERE ATIVO = 'S'";
List<Dictionary<string, object?>> resultadoDict = await produtoClientService.QueryAsDictionaryAsync(script);
```

## 🔎 Filtros com parâmetros, paginação e erros

**Nunca concatene valores vindos do usuário na expressão ou no SQL.** Use `?` no lugar de cada valor e passe os valores em ordem. Eles vão separados da expressão (`<parameter>`), então não viram SQL:

```c#
using SankhyaAPI.Client.Envelopes;

List<ProdutoEntity> produtos = await produtoService.FindAsync(
    "this.DESCRPROD LIKE ? AND this.CODGRUPOPROD = ?",
    [Parameter.Texto(busca + "%"), Parameter.Inteiro(codGrupo)],
    orderBy: "this.CODPROD");
```

Fábricas disponíveis: `Parameter.Texto`, `Parameter.Inteiro`, `Parameter.Decimal`, `Parameter.Data` (`DateOnly`) e `Parameter.DataHora` (`DateTime`).

Para paginar (o Sankhya devolve até 50 registros por página), use `FindPageAsync` com uma ordenação estável:

```c#
int pagina = 0;
SankhyaPage<ProdutoEntity> resultado;
do
{
    resultado = await produtoService.FindPageAsync("this.ATIVO = ?", [Parameter.Texto("S")], pagina++, "this.CODPROD");
    Processar(resultado.Items);
} while (resultado.HasMoreResult);
```

`QueryRawAsync` e `QueryAsDictionaryAsync` executam o SQL como está, com o poder do usuário de integração. Use só com SQL fixo, escrito por você.

**Erros.** Quando o Sankhya responde com erro, o pacote lança `SankhyaException` (namespace `SankhyaAPI.Client.Exceptions`), com `Status`, `ServiceName`, `TsErrorCode` e a mensagem do servidor já decodificada. Sessão inválida ou expirada (`status="3"`) lança `SankhyaSessionExpiredException`. As duas herdam de `Exception`. Falhas de transporte (HTTP 4xx/5xx, corpo vazio) lançam `HttpRequestException`. Erros de conversão de campo lançam `FormatException` sem o valor na mensagem: o valor fica só em `exception.Data["Valor"]`, para não ir para o log sem querer.

## 📄 Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE.txt) para mais detalhes.
