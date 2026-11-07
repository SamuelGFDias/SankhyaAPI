# 👜 SankhyaAPI.Client

![GitHub issues](https://img.shields.io/github/issues/SamuelGFDias/SankhyaAPI)
![GitHub forks](https://img.shields.io/github/forks/SamuelGFDias/SankhyaAPI)
![GitHub stars](https://img.shields.io/github/stars/SamuelGFDias/SankhyaAPI)
![GitHub license](https://img.shields.io/github/license/SamuelGFDias/SankhyaAPI)
![GitHub Languages](https://img.shields.io/github/languages/top/SamuelGFDias/SankhyaAPI)
![Nuget](https://img.shields.io/nuget/v/SankhyaAPI.Client)

Projeto de integração com o ERP Sankhya para operação de CRUD!

## 📑 Índice
- [Sobre](#sobre)
- [Instalação](#instalação)
- [Configuração](#configuração)
- [Funcionalidades](#funcionalidades)
- [Uso](#uso)
- [Licença](#licença)

## Sobre

> Este projeto visa disponibilizar, de maneira mais sucinta, métodos mais utilizados para manipulações e retornos de dados. Utilizando a própria API do Sankhya disponível [aqui](https://developer.sankhya.com.br/reference/api-de-integrações-sankhya), o projeto abstrai os métodos para o usuário.


## Instalação
1. Na raiz do seu projeto, use o seguinte comando:
   ```bash
   dotnet add package SankhyaAPI.Client --version x.y.z

## Configuração

Inicialmente, é importante destacar que esse pacote foi pensado para um contexto de injeção de dependência. Então, as configurações abaixo mostram como configurá-la na mesma.

1. Para configurar o pacote para ser usado no seu projeto, é necessário carregar a classe `SankhyaClientSettings` no namespace `SankhyaAPI.Client.Providers`. Essa classe deve ser injetada dentro das configurações do seu `IServiceCollection` como no exemplo abaixo.

    | Campo     | Descrição                                 |
    |-----------|-------------------------------------------|
    | `BaseUrl` | URL de conexão com aplicação SANKHYA      |
    | `Usuario` | Usuário que será utilizado para operações |
    | `Senha`   | Senha do Usuário                          |
    ```c#
    SankhyaClientSettings sankhyaSettings = new SankhyaClientSettings();
    sankhyaSettings.BaseUrl = "https://seusistema.sankhya.com";
    sankhyaSettings.Usuario = "usuario";
    sankhyaSettings.Senha = "senha";
    builder.Services.Configure<SankhyaClientSettings>(sankhyaSettings);
   
2.  É importante também injetar o serviço `builder.Services.AddSankhyaClient();` para que as classes de serviços estejam configuradas na injeção de dependência da sua solução.


## Funcionalidades

1. Para utilização nas três operações principais disponíveis `Update`, `Insert` e `Select`, é necessário mapear a entidade desejada para um objeto. Um exemplo é a entidade Produto, abaixo estão algumas propriedades da mesma. Campos pertencentes à chave primária devem ter o atributo `PrimaryKeyElement`. `XmlSerializable` é uma classe abstrata necessária para deserialização dos elementos do XML para o objeto, todas as classes de mapeamento devem implementá-la.

    | Campo            | Descrição                      | Tipo     | Obrigatório | Padrão |
    |------------------|--------------------------------|----------|-------------|--------|
    | `ElementName`    | Nome do campo na Entidade      | `string` | sim         | -      |
    | `AutoEnumerable` | Chave com numeração automática | `bool`   | não         | -      |
    ```c#
    public class ProdutoEntity : XmlSerialable
    {
        [PrimaryKeyElement("CODPROD", true)] public long? CodProd { get; set; }
        [XmlElement("CODVOL")] public string? CodVol { get; set; }
        [XmlElement("DESCRPROD")] public string? DescrProd { get; set; }
        [XmlElement("REFFORN")] public string? RefForn { get; set; }
    }   
    // obs: É indicado mapear todas as propriedades como nullable

2. Para criar a classe de serviço que vai operar nessa entidade é necessário passar como parâmetro um `IOptions<SankhyaClientSettings>`, a classe que representa a entidade e um `Enum` representando a instância da entidade (o mesmo deve utilizar o atributo `XmlEnum` com o nome da instância):
    ```c#
    public enum EEntityNames
    {
        [XmlEnum("Produto")] Produto,
    }

    public class ProdutoClientService(IOptions<SankhyaClientSettings> sankhyaApiConfig)
    :
        BaseService<ProdutoEntity>(
            sankhyaApiConfig,
            EEntityNames.Produto)

## Uso

#### Operações CRUD
Esta biblioteca permite realizar operações de CRUD (Criar, Ler, Atualizar e Excluir) usando classes de serviço que herdam de uma classe base genérica (BaseService<T>). A seguir estão exemplos de como utilizar esses métodos.

1. **Inserir**

   - O método Inserir permite adicionar uma ou mais instâncias de uma entidade no banco de dados.
       ```c#
       // Para inserir uma lista de objetos
       var produtos = new List<ProdutoEntity>
       {
           new ProdutoEntity { CodProd = 1, CodVol = "UN", DescrProd = "Produto A", RefForn = "F123" },
           new ProdutoEntity { CodProd = 2, CodVol = "UN", DescrProd = "Produto B", RefForn = "F456" }
       };
       var response = await produtoClientService.Inserir(produtos);

       // Para inserir um único objeto
       var produto = new ProdutoEntity { CodProd = 3, CodVol = "UN", DescrProd = "Produto C", RefForn = "F789" };
       var response = await produtoClientService.Inserir(produto);

2. **Atualizar**
   - O método Atualizar permite modificar uma ou mais instâncias de uma entidade já existente no banco de dados.
       ```c#
       // Para atualizar uma lista de objetos
       produtos[0].DescrProd = "Produto A Atualizado";
       var response = await produtoClientService.Atualizar(produtos);

       // Para atualizar um único objeto
       produto.DescrProd = "Produto C Atualizado";
       var response = await produtoClientService.Atualizar(produto);
     
3. **Recuperar**
   - O método Recuperar permite buscar dados de uma entidade com base em uma query SQL.
       ```c#

       // O query espera só a parte do Where de uma consulta SQL
       string query = "this.CODPROD = 1";
       var produtos = await produtoClientService.Recuperar(query);
     
4. **Query**
   - O método Query permite executar uma consulta SQL nativa e retornar os dados em uma lista de objetos mapeados ou em um dicionário de valores.
       ```c#
       public class ProdutoEntity
       {
           [XmlElement("CODPROD")] public long CodProd { get; set; }
           [XmlElement("CODVOL")] public string? CodVol { get; set; }
           [XmlElement("DESCRPROD")] public string? DescrProd { get; set; }
           [XmlElement("REFFORN")] public string? RefForn { get; set; }
           [XmlElement("DESCRGRUPOPROD")] public string? Grupo { get; set; }
       }

       // Program.cs

       // Exemplo de uso do método Query para retornar uma lista de objetos
       string script = "SELECT * FROM Produto WHERE CodProd > 1";
       List<ProdutoEntity> resultado = await produtoClientService.Query<ProdutoEntity>(script);

       // Exemplo de uso do método Query para retornar uma lista de dicionários
       List<Dictionary<string, dynamic?> resultadoDicionario = await produtoClientService.Query(script);

#### Descrição dos Métodos na BaseService&lt;T&gt;
    
- **Inserir(List&lt;T&gt; requests)**: Recebe uma lista de objetos do tipo `T` para inserir no banco. Retorna uma lista dos objetos inseridos.
- **Inserir(T request)**: Recebe uma instância de `T` e insere uma linha no banco. Retorna o objeto inserido.
- **Atualizar(List&lt;T&gt; requests)**: Recebe uma lista de objetos do tipo `T` para atualizar. Retorna uma lista dos objetos atualizados.
- **Atualizar(T request)**: Recebe uma instância de `T` e atualiza a linha correspondente no banco. Retorna o objeto atualizado.
- **Recuperar(string query)**: Executa uma consulta com base na `query` passada e retorna uma lista de objetos do tipo `T`.



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

## Licença
Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE.txt) para mais detalhes.
