using SankhyaAPI.Client.Exceptions;
using Xunit.Abstractions;

namespace SankhyaAPI.Client.Tests.Real;

public class Pedido
{
    public int Nunota { get; set; }
    public int NumNota { get; set; }
    public int Empresa { get; set; }
    public int Parceiro { get; set; }
    public int Top { get; set; }
    public int TipoVenda { get; set; }
    public int Natureza { get; set; }
    public int CentroCusto { get; set; }
    public int Vendedor { get; set; }
    public string TipoMovimento { get; set; } = "P";
    public DateOnly DataNegociacao { get; set; }
    public string? Observacao { get; set; }
}

public sealed class PedidoMap : SankhyaEntityMap<Pedido>
{
    public PedidoMap()
    {
        Entity("CabecalhoNota");
        Key(x => x.Nunota, "NUNOTA").AutoNumbered();
        Property(x => x.NumNota, "NUMNOTA");
        Property(x => x.Empresa, "CODEMP");
        Property(x => x.Parceiro, "CODPARC");
        Property(x => x.Top, "CODTIPOPER");
        Property(x => x.TipoVenda, "CODTIPVENDA");
        Property(x => x.Natureza, "CODNAT");
        Property(x => x.CentroCusto, "CODCENCUS");
        Property(x => x.Vendedor, "CODVEND");
        Property(x => x.TipoMovimento, "TIPMOV");
        Property(x => x.DataNegociacao, "DTNEG");
        Property(x => x.Observacao, "OBSERVACAO");
    }
}

public class ItemPedido
{
    public int Nunota { get; set; }
    public int Sequencia { get; set; }
    public int Produto { get; set; }
    public string Volume { get; set; } = "UN";
    public int Local { get; set; }
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public string UsoProduto { get; set; } = "R";
}

public sealed class ItemPedidoMap : SankhyaEntityMap<ItemPedido>
{
    public ItemPedidoMap()
    {
        Entity("ItemNota");
        Key(x => x.Nunota, "NUNOTA");
        Key(x => x.Sequencia, "SEQUENCIA").AutoNumbered();
        Property(x => x.Produto, "CODPROD");
        Property(x => x.Volume, "CODVOL");
        Property(x => x.Local, "CODLOCALORIG");
        Property(x => x.Quantidade, "QTDNEG");
        Property(x => x.ValorUnitario, "VLRUNIT");
        Property(x => x.UsoProduto, "USOPROD");
    }
}

public class ContatoParceiro
{
    public int Parceiro { get; set; }
    public int Contato { get; set; }
    public string? Nome { get; set; }
    public string? Email { get; set; }
    public int? Endereco { get; set; }
    public string? Numero { get; set; }
    public int? Bairro { get; set; }
    public int? Cidade { get; set; }
    public string? Cep { get; set; }
}

public sealed class ContatoParceiroMap : SankhyaEntityMap<ContatoParceiro>
{
    public ContatoParceiroMap()
    {
        Entity("Contato");
        Key(x => x.Parceiro, "CODPARC");
        Key(x => x.Contato, "CODCONTATO").AutoNumbered();
        Property(x => x.Nome, "NOMECONTATO");
        Property(x => x.Email, "EMAIL");
        Property(x => x.Endereco, "CODEND");
        Property(x => x.Numero, "NUMEND");
        Property(x => x.Bairro, "CODBAI");
        Property(x => x.Cidade, "CODCID");
        Property(x => x.Cep, "CEP");
    }
}

/// <summary>
/// Pedido de venda real na homologação (TGFCAB/TGFITE, com dezenas de triggers): chave composta com SEQUENCIA
/// numerada pelo servidor e erros vindos das regras do banco. Cada teste apaga o que cria.
/// </summary>
[Trait("Category", "RealNota")]
public class NotaRealTests(ITestOutputHelper saida)
{
    static NotaRealTests()
    {
        SankhyaMaps.Register<PedidoMap>();
        SankhyaMaps.Register<ItemPedidoMap>();
        SankhyaMaps.Register<ContatoParceiroMap>();
    }

    private static Pedido NovoPedido(string marca) => new()
    {
        Empresa = 1, Parceiro = 4, Top = 31, TipoVenda = 86, DataNegociacao = DateOnly.FromDateTime(DateTime.Today),
        Observacao = marca
    };

    private static ItemPedido NovoItem(int nunota, decimal quantidade) => new()
    {
        Nunota = nunota, Produto = 284, Local = 55070501, Quantidade = quantidade, ValorUnitario = 89.07m
    };

    private static Task<SankhyaException> Erro(Func<Task> acao) => Assert.ThrowsAnyAsync<SankhyaException>(acao);

    [RealFact]
    public async Task Chave_composta_com_a_parte_numerada_pelo_servidor_em_contato_de_parceiro()
    {
        await using SankhyaClient cliente = SankhyaReal.Cliente(SessionMode.Shared);
        var contatos = cliente.Repository<ContatoParceiro>();
        var contato = new ContatoParceiro
        {
            Parceiro = 4, Nome = "TESTE SANKHYAAPI", Endereco = 206520, Numero = "3456", Bairro = 21996, Cidade = 5003, Cep = "24452005"
        };

        try
        {
            await contatos.CreateAsync(contato);
            Assert.True(contato.Contato > 0, "o servidor deveria numerar CODCONTATO e o pacote preencher no objeto");

            await contatos.UpdateAsync(new ContatoParceiro { Parceiro = 4, Contato = contato.Contato, Email = "teste@exemplo.com" }, u => u.Set(x => x.Email));
            ContatoParceiro? lido = await contatos.GetAsync([4, contato.Contato]);
            Assert.Equal("teste@exemplo.com", lido!.Email);
            Assert.Equal("TESTE SANKHYAAPI", lido.Nome);

            await Assert.ThrowsAsync<SankhyaNotFoundException>(() =>
                contatos.UpdateAsync(new ContatoParceiro { Parceiro = 4, Contato = 987654, Email = "x@x.com" }, u => u.Set(x => x.Email)));
        } finally
        {
            if (contato.Contato > 0) await contatos.DeleteAsync(new ContatoParceiro { Parceiro = 4, Contato = contato.Contato });
        }

        Assert.Null(await contatos.GetAsync([4, contato.Contato]));
    }

    [RealFact]
    public async Task Leitura_por_chave_composta_em_item_de_nota()
    {
        await using SankhyaClient cliente = SankhyaReal.Cliente(SessionMode.Shared);
        Dictionary<string, object?> existente = (await cliente.Sql.QueryAsDictionaryAsync(
            "SELECT TOP 1 NUNOTA, SEQUENCIA, CODPROD FROM TGFITE ORDER BY NUNOTA DESC, SEQUENCIA")).Single();

        ItemPedido? item = await cliente.Repository<ItemPedido>().GetAsync([existente["NUNOTA"]!, existente["SEQUENCIA"]!]);

        Assert.Equal(Convert.ToInt32(existente["CODPROD"]), item!.Produto);
    }

    [RealFact]
    public async Task Erros_das_regras_do_Sankhya_e_das_triggers_viram_categorias()
    {
        await using SankhyaClient cliente = SankhyaReal.Cliente(SessionMode.Shared);
        var pedidos = cliente.Repository<Pedido>();
        string marca = "TESTE-SANKHYAAPI-" + Guid.NewGuid().ToString("N")[..8];
        var pedido = NovoPedido(marca);
        DateOnly hoje = DateOnly.FromDateTime(DateTime.Today);

        try
        {
            await pedidos.CreateAsync(pedido);
            Assert.True(pedido.Nunota > 0);

            // Trigger do banco (RAISERROR): regra de negócio, com a mensagem da regra sem o prefixo SQL-50001.
            var trigger = await Erro(() => pedidos.UpdateAsync(new Pedido { Nunota = pedido.Nunota, DataNegociacao = new DateOnly(1900, 1, 1) }, u => u.Set(x => x.DataNegociacao)));
            Assert.IsType<SankhyaBusinessRuleException>(trigger);
            Assert.StartsWith("Ano inferior ao limite permitido", trigger.Message);
            saida.WriteLine($"trigger: {trigger.Message}");

            // Validação do próprio Sankhya: regra de negócio.
            var validacao = await Erro(() => cliente.Repository<ContatoParceiro>().CreateAsync(new ContatoParceiro { Parceiro = 4, Nome = marca }));
            Assert.Equal(SankhyaErrorKind.BusinessRule, validacao.Kind);
            Assert.Contains("endereço do contato é obrigatório", validacao.Message);

            // Referência inexistente: valor inválido.
            var empresa = await Erro(() => pedidos.CreateAsync(new Pedido { Empresa = 9999, Parceiro = 4, Top = 31, TipoVenda = 86, DataNegociacao = hoje, Observacao = marca }));
            Assert.Equal((SankhyaErrorKind.InvalidValue, "CORE_E00912"), (empresa.Kind, empresa.TsErrorCode));
            var top = await Erro(() => pedidos.CreateAsync(new Pedido { Empresa = 1, Parceiro = 4, Top = 99999, TipoVenda = 86, DataNegociacao = hoje, Observacao = marca }));
            Assert.Equal(SankhyaErrorKind.InvalidValue, top.Kind);
            var truncado = await Erro(() => pedidos.UpdateAsync(new Pedido { Nunota = pedido.Nunota, TipoVenda = 99999 }, u => u.Set(x => x.TipoVenda)));
            Assert.Equal(SankhyaErrorKind.InvalidValue, truncado.Kind);

            // Chave estrangeira do banco: violação de restrição, com o campo.
            var fk = await Erro(() => pedidos.CreateAsync(new Pedido { Empresa = 1, Parceiro = 999999999, Top = 31, TipoVenda = 86, DataNegociacao = hoje, Observacao = marca }));
            Assert.Equal((SankhyaErrorKind.ConstraintViolation, "CODPARC"), (fk.Kind, fk.Field));

            // Entidade que não aceita gravação pelo CRUD genérico: status 0 sem mensagem vira Unknown explicado.
            var crud = await Erro(() => cliente.Repository<ItemPedido>().CreateAsync(NovoItem(pedido.Nunota, 1)));
            Assert.Equal(SankhyaErrorKind.Unknown, crud.Kind);
            Assert.Contains("sem mensagem", crud.Message);
        } finally
        {
            foreach (Pedido p in await pedidos.FindAsync("this.OBSERVACAO = ?", [Parameter.Texto(marca)]))
                await pedidos.DeleteAsync(p);
        }

        Assert.Empty(await pedidos.FindAsync("this.OBSERVACAO = ?", [Parameter.Texto(marca)]));
    }
}
