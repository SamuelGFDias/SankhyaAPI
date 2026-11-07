using SankhyaAPI.Client.Tests.Fake;
using SankhyaAPI.Client.Tests.Support;

namespace SankhyaAPI.Client.Tests.Unit;

/// <summary>
/// Chave composta em que só um campo é auto-numerado (NUNOTA informado, SEQUENCIA gerada pelo servidor).
/// </summary>
public class ChaveCompostaFakeTests : IDisposable
{
    private const string Save = "CRUDServiceProvider.saveRecord";
    private const string Remove = "CRUDServiceProvider.removeRecord";

    private readonly SankhyaFakeServer _fake = new();

    public void Dispose() => _fake.Dispose();

    private ItemNotaService Itens() => new(_fake.BaseUrl, "usuario", "senha");

    [Fact]
    public async Task Insert_envia_a_parte_informada_e_omite_a_auto_numerada()
    {
        _fake.ComEntidades(Save, ["500", "3", "2"]);

        ItemNota? criado = await Itens().CreateAsync(new ItemNota { NuNota = 500, Quantidade = 2m });

        string body = _fake.Chamadas(Save).Single().RequestMessage.Body!;
        Assert.Contains("<NUNOTA>500</NUNOTA>", body);
        Assert.DoesNotContain("<SEQUENCIA>", body);
        Assert.Equal(3, criado!.Sequencia.Value);
    }

    [Fact]
    public async Task Insert_sem_a_parte_nao_numerada_e_barrado()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Itens().CreateAsync(new ItemNota { Quantidade = 1m }));

        Assert.Equal(nameof(ItemNota.NuNota), ex.ParamName);
        Assert.Empty(_fake.Chamadas(Save));
    }

    [Fact]
    public async Task Insert_com_a_parte_auto_numerada_preenchida_e_barrado()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Itens().CreateAsync(new ItemNota { NuNota = 500, Sequencia = 1 }));

        Assert.Empty(_fake.Chamadas(Save));
    }

    [Fact]
    public async Task Update_exige_e_envia_as_duas_partes_na_chave()
    {
        _fake.ComEntidades(Save, ["500", "3", "5"]);

        await Itens().UpdateAsync(new ItemNota { NuNota = 500, Sequencia = 3, Quantidade = 5m });

        string body = _fake.Chamadas(Save).Single().RequestMessage.Body!;
        Assert.Matches(@"<key>\s*<NUNOTA>500</NUNOTA>\s*<SEQUENCIA>3</SEQUENCIA>\s*</key>", body);
        await Assert.ThrowsAsync<ArgumentNullException>(() => Itens().UpdateAsync(new ItemNota { NuNota = 500, Quantidade = 1m }));
    }

    [Fact]
    public async Task Delete_envia_as_duas_partes_no_id()
    {
        _fake.ComXmlBruto(Remove, "<responseBody />");

        await Itens().DeleteAsync(new ItemNota { NuNota = 500, Sequencia = 3 });

        Assert.Matches(@"<id>\s*<NUNOTA>500</NUNOTA>\s*<SEQUENCIA>3</SEQUENCIA>\s*</id>", _fake.Chamadas(Remove).Single().RequestMessage.Body);
    }
}
