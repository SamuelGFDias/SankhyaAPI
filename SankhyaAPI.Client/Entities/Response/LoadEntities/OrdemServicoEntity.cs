using System.Text.Json.Serialization;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;

namespace SankhyaAPI.Client.Entities.Response.LoadEntities;

public class OrdemServicoEntity : IProxysable<OrdemServicoEntity, OrdemServicoProxy>
{
    [JsonPropertyName("NUMOS")] public long NumOs { get; set; }
    public OrdemServicoEntity FromProxy(OrdemServicoProxy proxy) => new()
    {
        NumOs = proxy.NumOs,
    };
}