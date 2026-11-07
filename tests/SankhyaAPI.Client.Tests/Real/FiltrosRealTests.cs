using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Real;

/// <summary>
/// Massa de dados na AD_TESTE para os testes de filtro, paginação e injeção: 120 registros com IDs
/// consecutivos e um marcador único no TEXTO, gravados uma vez por execução e apagados no fim.
/// </summary>
public sealed class MassaAdTeste : IAsyncLifetime
{
    public const int Quantidade = 120;

    public int PrimeiroId { get; } = Random.Shared.Next(800_000, 890_000);
    public string Marcador { get; } = "MASSA-" + Guid.NewGuid().ToString("N")[..8];

    public AdTesteService Service { get; } = SankhyaReal.Configurado
        ? new(SankhyaReal.BaseUrl!, SankhyaReal.Usuario!, SankhyaReal.Senha!)
        : null!;

    public List<AdTeste> Registros { get; } = [];

    /// <summary>Expressão que limita a busca à massa desta execução.</summary>
    public string Escopo => "this.ID BETWEEN ? AND ?";

    public Parameter[] ParametrosDoEscopo => [Parameter.Inteiro(PrimeiroId), Parameter.Inteiro(PrimeiroId + Quantidade - 1)];

    public async Task InitializeAsync()
    {
        if (!SankhyaReal.Configurado) return;

        for (int i = 0; i < Quantidade; i++)
        {
            Registros.Add(new AdTeste
            {
                Id = PrimeiroId + i,
                Texto = $"{Marcador}-{i:D3}",
                Inteiro = i,
                Decimal = i + 0.25m,
                Data = new DateOnly(2026, 1, 1).AddDays(i),
                DataHora = new DateTime(2026, 1, 1, 8, 0, 0).AddHours(i),
                Ativo = i % 2 == 0,
                Situacao = (SituacaoTeste)(i % 3)
            });
        }

        await Service.CreateManyAsync(Registros);
    }

    public async Task DisposeAsync()
    {
        if (!SankhyaReal.Configurado) return;

        List<AdTeste> existentes = await Service.FindAsync(Escopo, ParametrosDoEscopo);
        if (existentes.Count > 0)
            await Service.DeleteAsync(existentes.Select(r => new AdTeste { Id = r.Id }).ToList());
    }
}

/// <summary>
/// Filtros parametrizados de cada tipo, ordenação, paginação e injeção pelo criteria, contra a massa da AD_TESTE.
/// </summary>
[Trait("Category", "RealEscrita")]
public class FiltrosRealTests(MassaAdTeste massa) : IClassFixture<MassaAdTeste>
{
    private AdTesteService Service => massa.Service;

    private Task<List<AdTeste>> Buscar(string filtro, params Parameter[] parametros) =>
        Service.FindAsync($"{massa.Escopo} AND ({filtro})", [..massa.ParametrosDoEscopo, ..parametros], "this.ID");

    [RealFact]
    public async Task A_massa_inteira_foi_gravada_em_uma_chamada()
    {
        Assert.Equal(MassaAdTeste.Quantidade, (await Service.FindAsync(massa.Escopo, massa.ParametrosDoEscopo)).Count);
    }

    [RealFact]
    public async Task Filtro_por_texto_com_LIKE()
    {
        List<AdTeste> achados = await Buscar("this.TEXTO LIKE ?", Parameter.Texto($"{massa.Marcador}-01%"));

        Assert.Equal(Enumerable.Range(10, 10), achados.Select(r => r.Inteiro.Value));
    }

    [RealFact]
    public async Task Filtro_por_faixa_de_inteiros()
    {
        List<AdTeste> achados = await Buscar("this.INTEIRO BETWEEN ? AND ?", Parameter.Inteiro(5), Parameter.Inteiro(9));

        Assert.Equal([5, 6, 7, 8, 9], achados.Select(r => r.Inteiro.Value));
    }

    [RealFact]
    public async Task Filtro_por_decimal()
    {
        List<AdTeste> achados = await Buscar("this.AD_DECIMAL > ?", Parameter.Decimal(117.25m));

        Assert.Equal([118, 119], achados.Select(r => r.Inteiro.Value));
    }

    [RealFact]
    public async Task Filtro_por_data()
    {
        AdTeste achado = (await Buscar("this.AD_DATA = ?", Parameter.Data(new DateOnly(2026, 1, 31)))).Single();

        Assert.Equal(30, achado.Inteiro.Value);
    }

    [RealFact]
    public async Task Filtro_por_data_e_hora()
    {
        List<AdTeste> achados = await Buscar("this.DATAHORA >= ? AND this.DATAHORA < ?",
            Parameter.DataHora(new DateTime(2026, 1, 1, 10, 0, 0)),
            Parameter.DataHora(new DateTime(2026, 1, 1, 13, 0, 0)));

        Assert.Equal([2, 3, 4], achados.Select(r => r.Inteiro.Value));
    }

    [RealFact]
    public async Task Filtro_por_booleano_e_opcao()
    {
        List<AdTeste> achados = await Buscar("this.ATIVO = ? AND this.SITUACAO = ?", Parameter.Texto("S"), Parameter.Texto("P"));

        // ATIVO = par, SITUACAO P = resto 2 na divisão por 3: múltiplos de 6 mais 2.
        Assert.Equal(Enumerable.Range(0, 20).Select(k => 6 * k + 2), achados.Select(r => r.Inteiro.Value));
    }

    [RealFact]
    public async Task Ordenacao_decrescente()
    {
        List<AdTeste> achados = await Service.FindAsync(massa.Escopo, massa.ParametrosDoEscopo, "this.INTEIRO DESC");

        Assert.Equal(Enumerable.Range(0, MassaAdTeste.Quantidade).Reverse(), achados.Select(r => r.Inteiro.Value));
    }

    [RealFact]
    public async Task Paginacao_percorre_tudo_sem_repetir_nem_pular()
    {
        var vistos = new List<int>();
        var paginas = new List<SankhyaPage<AdTeste>>();
        SankhyaPage<AdTeste> pagina;
        int numero = 0;

        do
        {
            pagina = await Service.FindPageAsync(massa.Escopo, massa.ParametrosDoEscopo, numero++, "this.ID");
            paginas.Add(pagina);
            vistos.AddRange(pagina.Items.Select(r => r.Id.Value));
        } while (pagina.HasMoreResult && numero < 10);

        Assert.Equal([50, 50, 20], paginas.Select(p => p.Items.Count));
        Assert.False(paginas[^1].HasMoreResult);
        Assert.Equal(Enumerable.Range(massa.PrimeiroId, MassaAdTeste.Quantidade), vistos);
    }

    [RealFact]
    public async Task Pagina_depois_do_fim_vem_vazia()
    {
        SankhyaPage<AdTeste> pagina = await Service.FindPageAsync(massa.Escopo, massa.ParametrosDoEscopo, 5, "this.ID");

        Assert.Empty(pagina.Items);
        Assert.False(pagina.HasMoreResult);
    }

    [RealTheory]
    [InlineData("' OR '1'='1")]
    [InlineData("x' OR 1=1 --")]
    [InlineData("'; DELETE FROM AD_TESTE; --")]
    [InlineData("') OR ('a'='a")]
    [InlineData("1 OR 1=1")]
    public async Task Parametro_com_SQL_e_tratado_como_valor(string valorMalicioso)
    {
        Assert.Empty(await Buscar("this.TEXTO = ?", Parameter.Texto(valorMalicioso)));

        // Nada foi apagado nem alterado pela tentativa.
        Assert.Equal(MassaAdTeste.Quantidade, (await Service.FindAsync(massa.Escopo, massa.ParametrosDoEscopo)).Count);
    }

    [RealFact]
    public async Task Parametro_com_caracteres_de_XML_nao_quebra_o_envelope()
    {
        Assert.Empty(await Buscar("this.TEXTO = ?", Parameter.Texto("</parameter><parameter type=\"S\">x</parameter>&<![CDATA[")));
    }

    [RealFact]
    public async Task Parametro_inteiro_invalido_e_recusado_pelo_servidor()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => Buscar("this.INTEIRO = ?", new Parameter("I", "1 OR 1=1")));

        Assert.Equal(MassaAdTeste.Quantidade, (await Service.FindAsync(massa.Escopo, massa.ParametrosDoEscopo)).Count);
    }
}

/// <summary>
/// Limites de volume, só lendo a tabela de cidades (TSICID, mais de 10 mil linhas na homologação).
/// </summary>
[Trait("Category", "Real")]
public class LimitesRealTests
{
    private static CidadeService Cidades() => new(SankhyaReal.BaseUrl!, SankhyaReal.Usuario!, SankhyaReal.Senha!);

    [RealFact]
    public async Task FindAsync_sem_pagina_traz_mais_de_5_mil_registros()
    {
        List<Cidade> cidades = await Cidades().FindAsync("1 = 1");

        Assert.True(cidades.Count > 5000, $"esperava mais de 5000 cidades, vieram {cidades.Count}");
    }

    [RealFact]
    public async Task FindPageAsync_alcanca_paginas_depois_do_registro_5000()
    {
        SankhyaPage<Cidade> pagina = await Cidades().FindPageAsync("1 = 1", null, 101, "this.CODCID");

        Assert.Equal(50, pagina.Items.Count);
    }

    /// <summary>
    /// O executeQuery corta em 5000 linhas e marca burstLimit. Este teste documenta o corte.
    /// </summary>
    [RealFact]
    public async Task QueryRawAsync_e_cortado_em_5000_linhas_pelo_servidor()
    {
        List<CidadeLinha> linhas = await Cidades().QueryRawAsync<CidadeLinha>("SELECT CODCID FROM TSICID");

        Assert.Equal(5000, linhas.Count);
    }
}
