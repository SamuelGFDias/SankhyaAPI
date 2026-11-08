using System.Text.Json;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Unit;

/// <summary>
/// Mapa fluente (#24), update parcial nos três modos (#25), chave composta validada (#26) e NullableState imutável (#16).
/// </summary>
public class MapaFluenteFakeTests : IDisposable
{
    private const string Load = "CRUDServiceProvider.loadRecords";
    private const string Save = "CRUDServiceProvider.saveRecord";

    private readonly SankhyaFakeServer _fake = new();

    static MapaFluenteFakeTests()
    {
        SankhyaMaps.Register<OrdemServicoMap>();
        SankhyaMaps.Register<ProdutoPocoMap>();
    }

    public void Dispose() => _fake.Dispose();

    private ISankhyaRepository<OrdemServico> Ordens() => _fake.CreateClient().Repository<OrdemServico>();

    private static string Corpo(IReadOnlyList<WireMock.Logging.ILogEntry> chamadas) => chamadas.Single().RequestMessage!.Body!;

    [Fact]
    public async Task Insert_projeta_o_campo_calculado_em_data_e_hora_e_preenche_a_chave_gerada()
    {
        _fake.WithRawXml(Save,
            "<responseBody><entities total=\"1\"><metadata><fields><field name=\"NUMOS\"/></fields></metadata><entity><f0>77</f0></entity></entities></responseBody>");
        var ordem = new OrdemServico { Descricao = "Troca", DataExecucao = new DateTime(2026, 11, 7, 14, 30, 0), Status = StatusOs.Aberta };

        await Ordens().CreateAsync(ordem);

        string body = Corpo(_fake.Calls(Save));
        Assert.Contains("rootEntity=\"OrdemServico\"", body);
        Assert.Contains("<DT_FINAL>07/11/2026</DT_FINAL>", body);
        Assert.Contains("<HR_FINAL>1430</HR_FINAL>", body);
        Assert.Contains("<STATUS>A</STATUS>", body);
        Assert.DoesNotContain("<NUMOS>", body);
        Assert.Equal(77, ordem.Numero);
    }

    [Fact]
    public async Task Leitura_recompoe_o_campo_calculado_e_le_entidade_ligada_e_apresentacao()
    {
        _fake.WithRawXml(Load,
            """
            <responseBody><entities total="1"><metadata><fields>
              <field name="NUMOS"/><field name="DESCRICAO"/><field name="DT_FINAL"/><field name="HR_FINAL"/><field name="STATUS"/>
              <field name="Parceiro_NOMEPARC"/><field name="Usuario_NOMEUSU"/>
            </fields></metadata>
            <entity><f0>5</f0><f1>Troca</f1><f2>07/11/2026 00:00:00</f2><f3>905</f3><f4>F</f4><f5>ACME  </f5><f6>SUP</f6></entity>
            </entities></responseBody>
            """);

        OrdemServico ordem = (await Ordens().FindAsync("1 = 1")).Single();

        Assert.Equal(5, ordem.Numero);
        Assert.Equal(new DateTime(2026, 11, 7, 9, 5, 0), ordem.DataExecucao);
        Assert.Equal(StatusOs.Fechada, ordem.Status);
        Assert.Equal("ACME", ordem.NomeParceiro);
        Assert.Equal("SUP", ordem.NomeUsuario);

        string body = Corpo(_fake.Calls(Load));
        Assert.Contains("<entity path=\"Parceiro\"><field name=\"NOMEPARC\" /></entity>", body);
        Assert.Contains("includePresentationFields=\"S\"", body);
        Assert.DoesNotContain("name=\"Usuario_NOMEUSU\"", body);
    }

    [Fact]
    public async Task Update_com_campos_explicitos_manda_so_eles_e_a_chave()
    {
        _fake.WithEntities(Save, ["5"]);
        var ordem = new OrdemServico { Numero = 5, Descricao = "Nova", DataExecucao = DateTime.Now, Status = StatusOs.Fechada };

        await Ordens().UpdateAsync(ordem, u => u.Set(x => x.Descricao).Clear(x => x.DataExecucao));

        string body = Corpo(_fake.Calls(Save));
        Assert.Contains("<DESCRICAO>Nova</DESCRICAO>", body);
        Assert.Contains("<DT_FINAL />", body);
        Assert.Contains("<HR_FINAL />", body);
        Assert.DoesNotContain("<STATUS>", body);
        Assert.Matches(@"<key>\s*<NUMOS>5</NUMOS>\s*</key>", body);
    }

    [Fact]
    public async Task Update_sem_campos_em_modelo_sem_rastreio_nem_NullableState_e_recusado()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Ordens().UpdateAsync(new OrdemServico { Numero = 5 }));

        Assert.Contains("Track", ex.Message);
        Assert.Empty(_fake.Calls(Save));
    }

    [Fact]
    public async Task Update_rastreado_manda_so_o_que_mudou_e_null_vira_apagar()
    {
        _fake.WithEntities(Save, ["5"]);
        var repo = Ordens();
        var ordem = repo.Track(new OrdemServico { Numero = 5, Descricao = "Antes", DataExecucao = new DateTime(2026, 1, 1), Status = StatusOs.Aberta });

        ordem.Descricao = null;
        ordem.Status = StatusOs.Fechada;
        await repo.UpdateAsync(ordem);

        string body = Corpo(_fake.Calls(Save));
        Assert.Contains("<DESCRICAO />", body);
        Assert.Contains("<STATUS>F</STATUS>", body);
        Assert.DoesNotContain("<DT_FINAL", body);
    }

    [Fact]
    public async Task UpdateAll_manda_todos_os_campos_mapeados()
    {
        _fake.WithEntities(Save, ["5"]);

        await Ordens().UpdateAllAsync(new OrdemServico { Numero = 5, Descricao = "X", Status = StatusOs.Aberta });

        string body = Corpo(_fake.Calls(Save));
        Assert.Contains("<DESCRICAO>X</DESCRICAO>", body);
        Assert.Contains("<DT_FINAL />", body);
        Assert.Contains("<STATUS>A</STATUS>", body);
        Assert.DoesNotContain("NOMEPARC", body.Split("<dataRow>")[1]);
    }

    [Fact]
    public void Modelos_do_projeto_usam_os_acessores_gerados_pelo_source_generator()
    {
        Assert.True(SankhyaAccessorRegistry.IsGenerated(typeof(OrdemServico)));
        Assert.True(SankhyaAccessorRegistry.IsGenerated(typeof(Produto)));
    }

    [Fact]
    public void Mapa_com_dois_campos_auto_numerados_na_chave_e_rejeitado()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new DuasAutoMap().Compilar());

        Assert.Contains("mais de um campo auto-numerado", ex.Message);
    }

    [Fact]
    public void Mapa_que_escreve_a_mesma_coluna_duas_vezes_e_rejeitado()
    {
        Assert.Throws<InvalidOperationException>(() => new ColunaRepetidaMap().Compilar());
    }

    [Fact]
    public async Task Mapa_fluente_e_atributos_geram_o_mesmo_envelope()
    {
        _fake.WithEntities(Save, ["10"]);
        var cliente = _fake.CreateClient();

        await cliente.Repository<Produto>().UpdateAsync(new Produto { CodProd = 10, DescrProd = "X", Preco = 1.5m, Ativo = true });
        await cliente.Repository<ProdutoPoco>().UpdateAsync(new ProdutoPoco { CodProd = 10, DescrProd = "X", Preco = 1.5m, Ativo = true },
            u => u.Set(x => x.DescrProd).Set(x => x.Preco).Set(x => x.Ativo));
        await cliente.Repository<Produto>().CreateAsync(new Produto { DescrProd = "Y", Preco = 2m });
        await cliente.Repository<ProdutoPoco>().CreateAsync(new ProdutoPoco { DescrProd = "Y", Preco = 2m });

        string[] corpos = _fake.Calls(Save).Select(c => c.RequestMessage!.Body!).ToArray();
        Assert.Equal(corpos[0], corpos[1]);
        Assert.Equal(corpos[2], corpos[3]);
    }

    [Fact]
    public void NullableState_e_imutavel_e_tem_os_tres_estados()
    {
        NullableState<string> set = "X";
        NullableState<string> clear = NullableState<string>.Clear();
        NullableState<string> unset = default;

        Assert.Equal((EPropertyState.Set, "X"), (set.State, set.Value));
        Assert.Equal(EPropertyState.Clear, clear.State);
        Assert.Equal(EPropertyState.UnSet, unset.State);
        Assert.Equal(NullableState<string>.Unset, unset);
        Assert.Null(typeof(NullableState<string>).GetProperty(nameof(NullableState<string>.Value))!.SetMethod);
        Assert.Equal("\"X\"", JsonSerializer.Serialize(set));
        Assert.Equal(EPropertyState.Clear, JsonSerializer.Deserialize<NullableState<int>>("null").State);
        Assert.Equal(5, JsonSerializer.Deserialize<NullableState<int>>("5").Value);
    }

    [Fact]
    public void Ida_e_volta_do_mapa_fluente_preserva_os_valores()
    {
        var exemplo = new OrdemServico
        {
            Descricao = "Ação", DataExecucao = new DateTime(2026, 11, 7, 23, 59, 0), Status = StatusOs.Fechada
        };

        OrdemServico volta = SankhyaMapAssert.RoundTrip(exemplo);

        Assert.Equal(exemplo.Descricao, volta.Descricao);
        Assert.Equal(exemplo.DataExecucao, volta.DataExecucao);
        Assert.Equal(exemplo.Status, volta.Status);
    }

    private sealed class DuasAuto
    {
        public int A { get; set; }
        public int B { get; set; }
    }

    // Mapas inválidos de propósito: os testes conferem a rejeição em tempo de execução; os analisadores SNK001 e
    // SNK002 já os acusam na compilação.
#pragma warning disable SNK001, SNK002
    private sealed class DuasAutoMap : SankhyaEntityMap<DuasAuto>
    {
        public DuasAutoMap()
        {
            Entity("X");
            Key(x => x.A, "A").AutoNumbered();
            Key(x => x.B, "B").AutoNumbered();
        }
    }

    private sealed class ColunaRepetidaMap : SankhyaEntityMap<DuasAuto>
    {
        public ColunaRepetidaMap()
        {
            Entity("X");
            Key(x => x.A, "A");
            Property(x => x.B, "A");
        }
    }
#pragma warning restore SNK001, SNK002
}

internal static class MapaTestExtensions
{
    public static void Compilar(this ISankhyaEntityMap map) => map.Build();
}
