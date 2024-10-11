using System.Text.Json.Serialization;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;

namespace SankhyaAPI.Client.Entities.Response.LoadEntities;

public class LocalFinanceiroEntity : IProxysable<LocalFinanceiroEntity, LocalFinanceiroProxy>
{
    [JsonPropertyName("CODLOCAL")] public long CodLocal { get; set; }

    public LocalFinanceiroEntity FromProxy(LocalFinanceiroProxy proxy) => new()
    {
        CodLocal = proxy.CodLocal,
    };
}