using System.Net;
using SankhyaAPI.Client.Exceptions;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Real;

/// <summary>
/// Cada tipo de erro provocado na homologação vira a categoria e o status HTTP certos, sem tratar texto.
/// Escrita só na AD_TESTE, com IDs aleatórios apagados no fim.
/// </summary>
[Trait("Category", "RealEscrita")]
public class ErrosRealTests
{
    private static ISankhyaRepository<AdTeste> Testes() => SankhyaReal.Cliente().Repository<AdTeste>();

    private static async Task<SankhyaException> Erro(Func<Task> acao, SankhyaErrorKind tipo, HttpStatusCode http)
    {
        var ex = await Assert.ThrowsAnyAsync<SankhyaException>(acao);
        Assert.Equal(tipo, ex.Kind);
        Assert.Equal(http, ex.SuggestedHttpStatus);
        Assert.DoesNotContain("SNK Error", ex.Message);
        return ex;
    }

    [RealFact]
    public async Task Senha_errada_e_usuario_inexistente_sao_falha_de_autenticacao()
    {
        var senha = await Erro(() => SankhyaReal.Cliente(senha: "errada-" + Guid.NewGuid()).Repository<Usuario>().FindAsync("1 = 1"),
            SankhyaErrorKind.AuthenticationFailed, HttpStatusCode.Unauthorized);
        Assert.IsType<SankhyaAuthenticationException>(senha);
        Assert.Equal("CORE_E01434", senha.TsErrorCode);
    }

    [RealFact]
    public async Task Campo_entidade_e_expressao_invalidos_sao_pedido_invalido()
    {
        var campo = await Erro(() => SankhyaReal.Cliente().Repository<CampoInexistente>().FindAsync("1 = 1"), SankhyaErrorKind.InvalidRequest, HttpStatusCode.BadRequest);
        Assert.Equal("CAMPOXYZ", campo.Field);

        await Erro(() => SankhyaReal.Cliente().Repository<Usuario>("EntidadeXyz").FindAsync("1 = 1"), SankhyaErrorKind.InvalidRequest, HttpStatusCode.BadRequest);
        await Erro(() => SankhyaReal.Cliente().Repository<Usuario>().FindAsync("this.CODUSU === 1"), SankhyaErrorKind.InvalidRequest, HttpStatusCode.BadRequest);
        await Erro(() => SankhyaReal.Cliente().Sql.QueryAsDictionaryAsync("SELEC 1"), SankhyaErrorKind.InvalidRequest, HttpStatusCode.BadRequest);
        await Erro(() => SankhyaReal.Cliente().Sql.QueryAsDictionaryAsync("UPDATE AD_TESTE SET TEXTO = 'x' WHERE ID = -1"), SankhyaErrorKind.InvalidRequest, HttpStatusCode.BadRequest);
    }

    [RealFact]
    public async Task Erro_de_execucao_da_consulta_e_QueryFailed()
    {
        await Erro(() => SankhyaReal.Cliente().Sql.QueryAsDictionaryAsync("SELECT 1/0 AS X"), SankhyaErrorKind.QueryFailed, HttpStatusCode.BadRequest);
    }

    [RealFact]
    public async Task Valor_invalido_traz_o_campo()
    {
        await Erro(() => SankhyaReal.Cliente().Repository<Usuario>().FindAsync("this.CODUSU = ?", [new Parameter("I", "abc")]),
            SankhyaErrorKind.InvalidValue, HttpStatusCode.UnprocessableEntity);

        int id = Random.Shared.Next(900_000, 1_000_000);
        try
        {
            var ex = await Erro(() => Testes().CreateAsync(new AdTeste { Id = id, Texto = new string('A', 150) }),
                SankhyaErrorKind.InvalidValue, HttpStatusCode.UnprocessableEntity);
            Assert.Equal("TEXTO", ex.Field);
        } finally
        {
            await Apagar(id);
        }
    }

    [RealFact]
    public async Task Chave_duplicada_e_conflito()
    {
        int id = Random.Shared.Next(900_000, 1_000_000);
        try
        {
            await Testes().CreateAsync(new AdTeste { Id = id });
            await Erro(() => Testes().CreateAsync(new AdTeste { Id = id }), SankhyaErrorKind.DuplicateKey, HttpStatusCode.Conflict);
        } finally
        {
            await Apagar(id);
        }
    }

    [RealFact]
    public async Task Update_de_chave_inexistente_nao_cria_registro()
    {
        int id = -Random.Shared.Next(100_000, 999_999);

        var ex = await Erro(() => Testes().UpdateAsync(new AdTeste { Id = id, Texto = "não deveria existir" }),
            SankhyaErrorKind.NotFound, HttpStatusCode.NotFound);

        Assert.IsType<SankhyaNotFoundException>(ex);
        Assert.Empty(await Testes().FindAsync("this.ID = ?", [Parameter.Inteiro(id)]));
    }

    [RealFact]
    public async Task ModifiedSince_sem_LOGTABOPER_e_recurso_nao_configurado()
    {
        await Erro(() => SankhyaReal.Cliente().Repository<Usuario>().Query().ModifiedSince(new DateTime(2026, 1, 1)).ToListAsync(),
            SankhyaErrorKind.NotConfigured, HttpStatusCode.NotImplemented);
    }

    private static async Task Apagar(int id)
    {
        var repo = Testes();
        if ((await repo.FindAsync("this.ID = ?", [Parameter.Inteiro(id)])).Count > 0)
            await repo.DeleteAsync(new AdTeste { Id = id });
    }

    [SankhyaEntity("Usuario")]
    public class CampoInexistente
    {
        [Key("CODUSU")] public NullableState<int> CodUsu { get; set; }
        [System.Xml.Serialization.XmlElement("CAMPOXYZ")] public NullableState<string> Xyz { get; set; }
    }
}
