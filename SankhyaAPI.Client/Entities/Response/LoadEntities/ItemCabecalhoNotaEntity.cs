using System.Text.Json.Serialization;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;

namespace SankhyaAPI.Client.Entities.Response.LoadEntities;

public class ItemCabecalhoNotaEntity : IProxysable<ItemCabecalhoNotaEntity, ItemCabecalhoNotaProxy>
{
    [JsonPropertyName("NUNOTA")] public long NuNota { get; set; }
    [JsonPropertyName("SEQUENCIA")] public long Sequencia { get; set; }

    public ItemCabecalhoNotaEntity FromProxy(ItemCabecalhoNotaProxy proxy) => new()
    {
        NuNota = proxy.NuNota,
        Sequencia = proxy.Sequencia,
    };
}