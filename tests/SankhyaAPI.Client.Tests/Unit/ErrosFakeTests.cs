using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Tests.Fake;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Unit;

/// <summary>
/// Como as respostas de erro do Sankhya viram exceção (#4, #5, #12, #14).
/// </summary>
public class ErrosFakeTests : IDisposable
{
    private const string Load = "CRUDServiceProvider.loadRecords";

    private readonly SankhyaFakeServer _fake = new();

    public void Dispose() => _fake.Dispose();

    private ISankhyaRepository<Usuario> Usuarios() => _fake.Cliente().Repository<Usuario>("Usuario");
    private ISankhyaRepository<Produto> Produtos() => _fake.Cliente().Repository<Produto>("Produto");

    [Fact]
    public async Task Status_0_sem_mensagem_e_erro()
    {
        _fake.ComStatus(Load, "0");

        var ex = await Assert.ThrowsAsync<SankhyaException>(() => Usuarios().FindAsync("1 = 1"));

        Assert.Equal("0", ex.Status);
        Assert.Contains("status 0", ex.Message);
    }

    [Fact]
    public async Task Status_1_com_mensagem_nao_e_erro()
    {
        _fake.ComStatus(Load, "1",
            $"<statusMessage><![CDATA[{SankhyaFakeServer.Base64("Aviso qualquer")}]]></statusMessage>" +
            "<responseBody><entities total=\"1\"><entity><f0>0</f0><f1>SUP</f1></entity></entities></responseBody>");

        Assert.Single(await Usuarios().FindAsync("1 = 1"));
    }

    [Fact]
    public async Task Status_3_vira_sessao_expirada()
    {
        _fake.ComStatus(Load, "3", $"<statusMessage><![CDATA[{SankhyaFakeServer.Base64("Não autorizado.")}]]></statusMessage>");

        var ex = await Assert.ThrowsAsync<SankhyaSessionExpiredException>(() => Usuarios().FindAsync("1 = 1"));

        Assert.Equal("3", ex.Status);
        Assert.Contains("Não autorizado.", ex.Message);
    }

    [Fact]
    public async Task TsError_traz_a_mensagem_decodificada_e_o_codigo()
    {
        _fake.ComStatus(Load, "0",
            $"<tsError tsErrorCode=\"CORE_E00001\" tsErrorLevel=\"ERROR\"><statusMessage>{SankhyaFakeServer.Base64("Campo obrigatório: DESCRPROD")}</statusMessage></tsError>");

        var ex = await Assert.ThrowsAsync<SankhyaException>(() => Usuarios().FindAsync("1 = 1"));

        Assert.Contains("Campo obrigatório: DESCRPROD", ex.Message);
        Assert.DoesNotContain("System.Byte[]", ex.Message);
        Assert.Equal("CORE_E00001", ex.TsErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("não é base64!")]
    public async Task Login_com_idusu_vazio_ou_invalido_nao_quebra(string idusu)
    {
        _fake.ComLoginIdUsuBruto(idusu);
        _fake.ComEntidades(Load, ["0", "SUP"]);

        Assert.Single(await Usuarios().FindAsync("1 = 1"));
    }

    [Fact]
    public async Task Erro_de_conversao_nao_expoe_o_valor_do_campo()
    {
        const string valor = "CPF 123.456.789-00";
        _fake.ComEntidades(Load, ["1", "X", valor]);

        var ex = await Assert.ThrowsAsync<FormatException>(() => Produtos().FindAsync("1 = 1"));

        Assert.DoesNotContain(valor, ex.ToString());
        Assert.Contains("Preco", ex.Message);
        Assert.Equal(valor, ex.Data["Valor"]);
    }
}
