using System.Text.Json.Serialization;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;

namespace SankhyaAPI.Client.Entities.Response.LoadEntities;

public class CabecalhoNotaEntity : IProxysable<CabecalhoNotaEntity, CabecalhoNotaProxy>
{
    [JsonPropertyName("NUNOTA")] public long NuNota { get; set; }

    public CabecalhoNotaEntity FromProxy(CabecalhoNotaProxy proxy) => new()
    {
        NuNota = proxy.NuNota,
    };
}