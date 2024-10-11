using System.Text.Json.Serialization;
using SankhyaAPI.Client.Entities.Response.Proxies;

namespace SankhyaAPI.Client.Entities.Response.LoadEntities;

public class PedidoOsItemEntity
{
    [JsonPropertyName("NUNOTA")] public long Nunota { get; set; }

    public static PedidoOsItemEntity? FromProxy(PedidoOsItemProxy? proxy)
    {
        if (proxy != null)
            return new PedidoOsItemEntity
            {
                Nunota = proxy.Nunota
            };
        return null;
    }
}