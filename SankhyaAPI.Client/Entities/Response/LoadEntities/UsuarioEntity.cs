using System.Text.Json.Serialization;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;

namespace SankhyaAPI.Client.Entities.Response.LoadEntities;

public class UsuarioEntity : IProxysable<UsuarioEntity, UsuarioProxy>
{
    [JsonPropertyName("CODUSU")] public long CodUsu { get; set; }

    public UsuarioEntity FromProxy(UsuarioProxy proxy) => new()
    {
        CodUsu = proxy.CodUsu,
    };
}