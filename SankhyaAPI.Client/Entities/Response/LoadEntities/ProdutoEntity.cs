using System.Text.Json.Serialization;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;

namespace SankhyaAPI.Client.Entities.Response.LoadEntities;

public class ProdutoEntity : IProxysable<ProdutoEntity, ProdutoProxy>
{
    [JsonPropertyName("CODPROD")] public long CodProd { get; set; }
   
    public ProdutoEntity FromProxy(ProdutoProxy proxy) => new()
    {
        CodProd = proxy.CodProd,
       
    };
}