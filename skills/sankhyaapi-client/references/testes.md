# Testes da integração

Use o pacote `SankhyaAPI.Client.Testing` (WireMock.Net). Ele traz um Sankhya fake local que fala o mesmo protocolo: XML em ISO-8859-1, mensagens em base64, status 0, 1 e 3, e executeQuery em JSON.

```bash
dotnet add <ProjetoDeTestes> package SankhyaAPI.Client.Testing
```

```csharp
using SankhyaAPI.Client.Testing;

public class PedidoServiceTests : IDisposable
{
    private readonly SankhyaFakeServer _fake = new();      // login e logout já respondem
    public void Dispose() => _fake.Dispose();

    [Fact]
    public async Task Regra_de_negocio_vira_422()
    {
        _fake.WithError("CRUDServiceProvider.saveRecord", "Pedido bloqueado: cliente com títulos vencidos.");
        await using SankhyaClient cliente = _fake.CreateClient();

        var ex = await Assert.ThrowsAsync<SankhyaBusinessRuleException>(() => cliente.Repository<Pedido>().CreateAsync(new Pedido()));

        Assert.Equal("Pedido bloqueado: cliente com títulos vencidos.", ex.Message);
        string corpo = _fake.Calls("CRUDServiceProvider.saveRecord").Single().RequestMessage!.Body!;
        Assert.Contains("<OBSERVACAO>", corpo);
    }
}
```

| Método | Para quê |
|---|---|
| `WithEntities(servico, ["c0","c1"], ...)` | resposta de loadRecords/saveRecord (colunas na ordem dos campos do mapa) |
| `WithRawXml(servico, "<responseBody>...</responseBody>")` | resposta livre (ex.: com `<metadata>`) |
| `WithError(servico, mensagem)` / `WithStatus(servico, status, conteudo)` | erro do Sankhya (status 0, 3...) |
| `WithQuery(metadataJson, rowsJson, burstLimit)` / `WithQueryError` / `WithQueryWhen(trecho, ...)` | executeQuery |
| `WithPage(pagina, temMais, linhas...)` | paginação do loadRecords |
| `WithSessionExpiredOnce(servico, linhas...)` / `WithLoginSequence("S1", "S2")` | renovação de sessão |
| `WithReport(chave, bytes)` | relatório |
| `WithDelay(servico, tempo)` / `WithHttpStatus(servico, 503)` | timeout, cancelamento e falha de transporte |
| `Calls(servico)` | inspecionar as requisições recebidas (corpo, cookie) |
| `CreateClient(mode, maxRetries, configure)` | cliente apontado para o fake. Ex.: `configure: o => o.VerifyExistenceOnUpdate = false` quando o teste não registra a consulta de existência do update |

`SankhyaMapAssert.RoundTrip(exemplo)` confere a ida e volta de um mapa sem servidor.

Lembre-se: com `VerifyExistenceOnUpdate` ligado (o padrão), todo update faz antes um loadRecords das chaves. No teste, registre `WithEntities("CRUDServiceProvider.loadRecords", ["<chave>"])` ou desligue a opção no `CreateClient`.

## Contra um Sankhya de homologação

Teste real é opt-in: leia as credenciais de variável de ambiente ou de um arquivo fora do repositório (permissão 600). Sem elas, o teste fica como *skipped*. Escreva só em tabela de teste (por exemplo, uma `AD_` criada para isso) e apague o que criar num `finally`.
