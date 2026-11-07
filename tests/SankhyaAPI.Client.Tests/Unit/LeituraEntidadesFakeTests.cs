using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.Tests.Fake;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Unit;

/// <summary>
/// Leitura de &lt;entities&gt; do loadRecords/saveRecord (#6) e filtro parametrizado com paginação (#2).
/// </summary>
public class LeituraEntidadesFakeTests : IDisposable
{
    private const string Load = "CRUDServiceProvider.loadRecords";

    private readonly SankhyaFakeServer _fake = new();

    public void Dispose() => _fake.Dispose();

    private ProdutoService Produtos() => new(_fake.BaseUrl, "usuario", "senha");
    private UsuarioService Usuarios() => new(_fake.BaseUrl, "usuario", "senha");

    [Fact]
    public async Task Colunas_sao_mapeadas_pela_metadata_mesmo_fora_de_ordem()
    {
        // Como o Sankhya real: metadata com os nomes, colunas extras de apresentação no fim.
        _fake.ComXmlBruto(Load,
            """
            <responseBody><entities total="1"><metadata><fields>
              <field name="DESCRPROD" /><field name="CODPROD" /><field name="PRECO" /><field name="Grupo_DESCRGRUPO" />
            </fields></metadata>
            <entity><f0>CANETA</f0><f1>10</f1><f2>1.5</f2><f3>PAPELARIA</f3></entity></entities></responseBody>
            """);

        Produto produto = (await Produtos().FindAsync("1 = 1")).Single();

        Assert.Equal(10, produto.CodProd.Value);
        Assert.Equal("CANETA", produto.DescrProd.Value);
        Assert.Equal(1.5m, produto.Preco.Value);
    }

    [Fact]
    public async Task Campo_omitido_nao_desalinha_os_seguintes()
    {
        _fake.ComXmlBruto(Load,
            """
            <responseBody><entities total="1"><metadata><fields>
              <field name="CODPROD" /><field name="DESCRPROD" /><field name="PRECO" /><field name="DTALTER" /><field name="ATIVO" />
            </fields></metadata>
            <entity><f0>10</f0><f2>2.5</f2><f4>S</f4></entity></entities></responseBody>
            """);

        Produto produto = (await Produtos().FindAsync("1 = 1")).Single();

        Assert.Equal(10, produto.CodProd.Value);
        Assert.Equal(2.5m, produto.Preco.Value);
        Assert.True(produto.Ativo.Value);
        Assert.Null(produto.DescrProd.Value);
    }

    [Fact]
    public async Task Elementos_pelo_nome_tambem_sao_aceitos()
    {
        _fake.ComXmlBruto(Load,
            "<responseBody><entities total=\"1\"><entity><DESCRPROD>LAPIS</DESCRPROD><CODPROD>3</CODPROD></entity></entities></responseBody>");

        Produto produto = (await Produtos().FindAsync("1 = 1")).Single();

        Assert.Equal(3, produto.CodProd.Value);
        Assert.Equal("LAPIS", produto.DescrProd.Value);
    }

    [Fact]
    public async Task Leitura_para_no_fim_de_entities()
    {
        _fake.ComXmlBruto(Load,
            """
            <responseBody><entities total="1"><entity><f0>0</f0><f1>SUP</f1></entity></entities>
            <entity><f0>99</f0><f1>FORA</f1></entity><chave valor="ABC" /></responseBody>
            """);

        Usuario usuario = (await Usuarios().FindAsync("1 = 1")).Single();

        Assert.Equal("SUP", usuario.NomeUsu.Value);
    }

    [Fact]
    public async Task FindAsync_parametrizado_envia_o_valor_fora_da_expressao()
    {
        _fake.ComEntidades(Load, ["0", "SUP"]);
        const string valorMalicioso = "X' OR 1=1 --";

        await Usuarios().FindAsync(
            "this.NOMEUSU = ? AND this.CODUSU = ?",
            [Parameter.Texto(valorMalicioso), Parameter.Inteiro(0)],
            "this.CODUSU");

        string body = _fake.Chamadas(Load).Single().RequestMessage.Body!;
        Assert.Contains("<expression>this.NOMEUSU = ? AND this.CODUSU = ?</expression>", body);
        Assert.Contains("<parameter type=\"S\">X' OR 1=1 --</parameter>", body);
        Assert.Contains("<parameter type=\"I\">0</parameter>", body);
        Assert.Contains("orderByExpression=\"this.CODUSU\"", body);
    }

    [Fact]
    public async Task Filtro_parametrizado_e_acessivel_pela_interface()
    {
        _fake.ComEntidades(Load, ["0", "SUP"]);
        IBaseService<Usuario> service = Usuarios();

        Assert.Single(await service.FindAsync("this.CODUSU = ?", [Parameter.Inteiro(0)]));
        Assert.Contains("<parameter type=\"I\">0</parameter>", _fake.Chamadas(Load).Single().RequestMessage.Body);
    }

    [Fact]
    public async Task FindPageAsync_pede_a_pagina_e_le_hasMoreResult()
    {
        _fake.ComXmlBruto(Load,
            "<responseBody><entities total=\"1\" hasMoreResult=\"true\" offsetPage=\"2\" offset=\"100\"><entity><f0>0</f0><f1>SUP</f1></entity></entities></responseBody>");

        SankhyaPage<Usuario> pagina = await Usuarios().FindPageAsync("1 = 1", null, 2, "this.CODUSU");

        string body = _fake.Chamadas(Load).Single().RequestMessage.Body!;
        Assert.Contains("disableRowsLimit=\"false\"", body);
        Assert.Contains("offsetPage=\"2\"", body);
        Assert.Equal(2, pagina.Page);
        Assert.True(pagina.HasMoreResult);
        Assert.Single(pagina.Items);
    }

    [Fact]
    public async Task FindAsync_sem_pagina_nao_envia_offsetPage()
    {
        _fake.ComEntidades(Load, ["0", "SUP"]);

        await Usuarios().FindAsync("1 = 1");

        string body = _fake.Chamadas(Load).Single().RequestMessage.Body!;
        Assert.Contains("disableRowsLimit=\"true\"", body);
        Assert.DoesNotContain("offsetPage", body);
    }

    [Fact]
    public async Task Texto_com_acento_vai_em_utf8_com_charset_coerente()
    {
        _fake.ComEntidades(Load, ["0", "SUP"]);

        await Usuarios().FindAsync("this.NOMEUSU = ?", [Parameter.Texto("JOÃO")]);

        var request = _fake.Chamadas(Load).Single().RequestMessage;
        byte[] bytes = request.BodyAsBytes!;
        string contentType = request.Headers!["Content-Type"].Single();

        Assert.Contains("utf-8", contentType, StringComparison.OrdinalIgnoreCase);
        Assert.True(ContemSequencia(bytes, [0xC3, 0x83]), "'Ã' deveria estar em UTF-8 (C3 83)");
        string corpo = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("ISO-8859-1", corpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveRecord_tambem_declara_utf8()
    {
        _fake.ComEntidades("CRUDServiceProvider.saveRecord", ["10", "AÇÃO"]);

        await Produtos().UpdateAsync(new Produto { CodProd = 10, DescrProd = "AÇÃO" });

        var request = _fake.Chamadas("CRUDServiceProvider.saveRecord").Single().RequestMessage;
        Assert.Contains("utf-8", request.Headers!["Content-Type"].Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<DESCRPROD>AÇÃO</DESCRPROD>", System.Text.Encoding.UTF8.GetString(request.BodyAsBytes!));
    }

    private static bool ContemSequencia(byte[] bytes, byte[] sequencia) =>
        Enumerable.Range(0, bytes.Length - sequencia.Length + 1)
                  .Any(i => bytes.AsSpan(i, sequencia.Length).SequenceEqual(sequencia));
}
