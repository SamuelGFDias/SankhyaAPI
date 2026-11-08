using System.Globalization;

using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Real;

/// <summary>
/// Testes de escrita contra o Sankhya real, só na tabela AD_TESTE (criada para isso na homologação).
/// Cada teste usa um ID aleatório alto e apaga o que criou. Rodar só eles:
/// ./scripts/test-real.sh Category=RealEscrita
/// </summary>
[Trait("Category", "RealEscrita")]
public class EscritaRealTests
{
    private static ISankhyaRepository<AdTeste> Service() => SankhyaReal.Cliente().Repository<AdTeste>("AD_TESTE");

    [RealFact]
    public async Task Cria_le_e_apaga_um_registro()
    {
        ISankhyaRepository<AdTeste> service = Service();
        int id = Random.Shared.Next(900_000, 1_000_000);

        try
        {
            AdTeste? criado = await service.CreateAsync(new AdTeste { Id = id });
            Assert.Equal(id, criado!.Id.Value);

            AdTeste lido = (await service.FindAsync("this.ID = ?", [Parameter.Inteiro(id)])).Single();
            Assert.Equal(id, lido.Id.Value);
        } finally
        {
            await ApagarSeExistir(service, id);
        }

        Assert.Empty(await service.FindAsync("this.ID = ?", [Parameter.Inteiro(id)]));
    }

    /// <summary>
    /// Grava uma coluna de cada tipo e relê: o valor tem que voltar igual. Roda em pt-BR para provar que
    /// decimal e datas não dependem da cultura da máquina, e o texto com acento cobre o encoding (#15).
    /// </summary>
    [RealFact]
    public async Task Todos_os_tipos_voltam_iguais_depois_de_gravados()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        ISankhyaRepository<AdTeste> service = Service();
        int id = Random.Shared.Next(900_000, 1_000_000);

        try
        {
            var gravado = new AdTeste
            {
                Id = id,
                Texto = "Ação São João: çãõéü ÀÉÎÕÜ",
                Inteiro = -42,
                Decimal = 1234.56m,
                Data = new DateOnly(2026, 2, 28),
                DataHora = new DateTime(2026, 11, 7, 10, 30, 45),
                Hora = new TimeOnly(9, 5),
                Ativo = true,
                Situacao = SituacaoTeste.Pendente
            };

            await service.CreateAsync(gravado);
            AdTeste lido = await Ler(service, id);

            Assert.Equal(gravado.Texto.Value, lido.Texto.Value);
            Assert.Equal(-42, lido.Inteiro.Value);
            Assert.Equal(1234.56m, lido.Decimal.Value);
            Assert.Equal(new DateOnly(2026, 2, 28), lido.Data.Value);
            Assert.Equal(new DateTime(2026, 11, 7, 10, 30, 45), lido.DataHora.Value);
            Assert.Equal(new TimeOnly(9, 5), lido.Hora.Value);
            Assert.True(lido.Ativo.Value);
            Assert.Equal(SituacaoTeste.Pendente, lido.Situacao.Value);

            // Conferência por outro caminho (executeQuery em JSON): se o XML tivesse corrompido o acento na
            // gravação e de novo na leitura, a ida e volta acima passaria; o banco não.
            Dictionary<string, object?> banco = (await SankhyaReal.Cliente().Sql.QueryAsDictionaryAsync(
                $"SELECT LEN(TEXTO) AS TAMANHO, UNICODE(SUBSTRING(TEXTO, 2, 1)) AS SEGUNDA FROM AD_TESTE WHERE ID = {id}")).Single();
            Assert.Equal(gravado.Texto.Value!.Length, Convert.ToInt32(banco["TAMANHO"]));
            Assert.Equal('ç', (char)Convert.ToInt32(banco["SEGUNDA"]));
        } finally
        {
            CultureInfo.CurrentCulture = original;
            await ApagarSeExistir(service, id);
        }
    }

    /// <summary>
    /// O update só manda o que mudou: Clear apaga o campo, UnSet não mexe, Set troca o valor.
    /// </summary>
    [RealFact]
    public async Task Update_com_Clear_apaga_e_sem_valor_preserva()
    {
        ISankhyaRepository<AdTeste> service = Service();
        int id = Random.Shared.Next(900_000, 1_000_000);

        try
        {
            await service.CreateAsync(new AdTeste
            {
                Id = id, Texto = "ORIGINAL", Inteiro = 1, Ativo = true, Situacao = SituacaoTeste.Ativo
            });

            await service.UpdateAsync(new AdTeste
            {
                Id = id,
                Texto = NullableState<string>.Clear(),
                Ativo = false,
                Situacao = SituacaoTeste.Inativo
            });

            AdTeste lido = await Ler(service, id);
            Assert.True(string.IsNullOrEmpty(lido.Texto.Value), "TEXTO deveria ter sido apagado pelo Clear");
            Assert.Equal(1, lido.Inteiro.Value);
            Assert.False(lido.Ativo.Value);
            Assert.Equal(SituacaoTeste.Inativo, lido.Situacao.Value);
        } finally
        {
            await ApagarSeExistir(service, id);
        }
    }

    /// <summary>
    /// Filtro parametrizado com texto acentuado na gravação e na busca: só acha se o encoding estiver certo
    /// nos dois sentidos (#15).
    /// </summary>
    [RealFact]
    public async Task Busca_por_texto_com_acento_encontra_o_que_foi_gravado()
    {
        ISankhyaRepository<AdTeste> service = Service();
        int id = Random.Shared.Next(900_000, 1_000_000);
        string texto = $"Coração {id}";

        try
        {
            await service.CreateAsync(new AdTeste { Id = id, Texto = texto });

            AdTeste lido = (await service.FindAsync("this.TEXTO = ?", [Parameter.Texto(texto)])).Single();
            Assert.Equal(id, lido.Id.Value);
        } finally
        {
            await ApagarSeExistir(service, id);
        }
    }

    [RealFact]
    public async Task Mapa_fluente_grava_e_le_campo_calculado_e_atualiza_por_campos_e_por_rastreio()
    {
        SankhyaMaps.Register<TesteDominioMap>();
        var repo = SankhyaReal.Cliente().Repository<TesteDominio>();
        int id = Random.Shared.Next(900_000, 1_000_000);

        try
        {
            await repo.CreateAsync(new TesteDominio
            {
                Id = id, Texto = "Domínio", Quando = new DateTime(2026, 11, 7, 14, 35, 0), Situacao = Situacao.Pendente, Ativo = true
            });

            TesteDominio lido = (await repo.FindAsync("this.ID = ?", [Parameter.Inteiro(id)])).Single();
            Assert.Equal(new DateTime(2026, 11, 7, 14, 35, 0), lido.Quando);
            Assert.Equal(Situacao.Pendente, lido.Situacao);
            Assert.Equal("Domínio", lido.Texto);

            await repo.UpdateAsync(new TesteDominio { Id = id }, u => u.Clear(x => x.Texto).Clear(x => x.Quando));
            TesteDominio limpo = (await repo.FindAsync("this.ID = ?", [Parameter.Inteiro(id)])).Single();
            Assert.True(string.IsNullOrEmpty(limpo.Texto));
            Assert.Null(limpo.Quando);
            Assert.Equal(Situacao.Pendente, limpo.Situacao);

            repo.Track(limpo);
            limpo.Situacao = Situacao.Inativa;
            await repo.UpdateAsync(limpo);
            TesteDominio rastreado = (await repo.FindAsync("this.ID = ?", [Parameter.Inteiro(id)])).Single();
            Assert.Equal(Situacao.Inativa, rastreado.Situacao);
            Assert.True(rastreado.Ativo);
        } finally
        {
            await ApagarSeExistir(Service(), id);
        }
    }

    private static async Task<AdTeste> Ler(ISankhyaRepository<AdTeste> service, int id) =>
        (await service.FindAsync("this.ID = ?", [Parameter.Inteiro(id)])).Single();

    [RealFact]
    public async Task Insert_sem_a_chave_e_barrado_antes_de_chamar_o_servidor()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CreateAsync(new AdTeste()));
    }

    private static async Task ApagarSeExistir(ISankhyaRepository<AdTeste> service, int id)
    {
        if ((await service.FindAsync("this.ID = ?", [Parameter.Inteiro(id)])).Count > 0)
            await service.DeleteAsync(new AdTeste { Id = id });
    }
}
