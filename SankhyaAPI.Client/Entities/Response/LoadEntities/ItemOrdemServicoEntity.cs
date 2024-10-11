using System.Text.Json.Serialization;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;

namespace SankhyaAPI.Client.Entities.Response.LoadEntities;

public class ItemOrdemServicoEntity : IProxysable<ItemOrdemServicoEntity, ItemOrdemServicoProxy>
{
    [JsonPropertyName("NUMOS")] public long NumOs { get; set; }
    [JsonPropertyName("NUMITEM")] public long NumItem { get; set; }

    public ItemOrdemServicoEntity FromProxy(ItemOrdemServicoProxy proxy) => new()
    {
        NumOs = proxy.NumOs,
        NumItem = proxy.NumItem,
    };
}