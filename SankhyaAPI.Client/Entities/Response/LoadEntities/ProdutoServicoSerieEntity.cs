using System.Text.Json.Serialization;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;

namespace SankhyaAPI.Client.Entities.Response.LoadEntities;

public class ProdutoServicoSerieEntity : IProxysable<ProdutoServicoSerieEntity, ProdutoServicoSerieProxy>
{
    [JsonPropertyName("SERIE")] public string Serie { get; set; }
    [JsonPropertyName("CODPROD")] public long CodProd { get; set; }
    [JsonPropertyName("NUMCONTRATO")] public long NumContrato { get; set; }


    public ProdutoServicoSerieEntity FromProxy(ProdutoServicoSerieProxy proxy) => new()
    {
        Serie = proxy.Serie ?? throw new Exception("Série da máquina retornou 'null'"),
        CodProd = proxy.CodProd,
        NumContrato = proxy.NumContrato
    };
}