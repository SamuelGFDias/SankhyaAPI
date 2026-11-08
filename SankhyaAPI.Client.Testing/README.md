# SankhyaAPI.Client.Testing

Servidor fake do Sankhya para testar integrações que usam o `SankhyaAPI.Client`, sem precisar de um Sankhya real.
O servidor é um WireMock local que responde no formato do `service.sbr` (XML em ISO-8859-1 e JSON no `executeQuery`).

## Instalação

```bash
dotnet add package SankhyaAPI.Client.Testing --version x.y.z
```

Instale no projeto de testes. O pacote traz o `SankhyaAPI.Client` e o WireMock como dependências.

## Uso

```csharp
using SankhyaAPI.Client.Testing;
using Xunit;

// Usuario é um modelo da sua integração, mapeado como no SankhyaAPI.Client (ver [SankhyaEntity] e [Key]).
public class UsuariosTests
{
    [Fact]
    public async Task Lista_os_usuarios_devolvidos_pelo_sankhya()
    {
        using var fake = new SankhyaFakeServer()
            .WithEntities("CRUDServiceProvider.loadRecords", ["0", "SUP"]);
        await using var cliente = fake.CreateClient();

        var usuarios = await cliente.Repository<Usuario>("Usuario").FindAsync("1 = 1");

        Assert.Single(usuarios);
        Assert.Single(fake.Calls("CRUDServiceProvider.loadRecords"));
    }
}
```

## Principais métodos

- `WithEntities(servico, linhas)`: resposta de `loadRecords` e `saveRecord`, com os valores na ordem dos campos pedidos.
- `WithError(servico, mensagem)` e `WithStatus(servico, status)`: erros do serviço.
- `WithQuery(...)` e `WithQueryError(mensagem)`: respostas do `executeQuery`.
- `WithSessionExpiredOnce(servico, linhas)`: sessão expirada na primeira chamada e sucesso depois.
- `WithDelay(servico, atraso)`: resposta atrasada, para testar cancelamento e timeout.
- `Calls(servico)` e `AllCalls`: requisições recebidas, para conferir o que o cliente enviou.
- `CreateClient(modo, maxRetries)`: cliente apontando para o servidor fake.

## Mapeamento

`SankhyaMapAssert.RoundTrip(exemplo)` faz a ida e volta de um modelo pelo mapa, sem servidor, e devolve a instância
lida de volta. Use para confirmar que o mapeamento preserva os valores.

## Licença

MIT. Veja o arquivo `LICENSE.txt`.
