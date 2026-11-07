using SankhyaAPI.Client.Envelopes;
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
    private static AdTesteService Service() => new(SankhyaReal.BaseUrl!, SankhyaReal.Usuario!, SankhyaReal.Senha!);

    [RealFact]
    public async Task Cria_le_e_apaga_um_registro()
    {
        AdTesteService service = Service();
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

    [RealFact]
    public async Task Insert_sem_a_chave_e_barrado_antes_de_chamar_o_servidor()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CreateAsync(new AdTeste()));
    }

    private static async Task ApagarSeExistir(AdTesteService service, int id)
    {
        if ((await service.FindAsync("this.ID = ?", [Parameter.Inteiro(id)])).Count > 0)
            await service.DeleteAsync(new AdTeste { Id = id });
    }
}
