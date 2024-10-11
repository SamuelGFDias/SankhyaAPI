using Microsoft.Extensions.Options;
using SankhyaAPI.Client.Entities.Request;
using SankhyaAPI.Client.Entities.Response.LoadEntities;
using SankhyaAPI.Client.Entities.Response.Proxies;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Providers;

namespace SankhyaAPI.Client.Services.Instancias;

public sealed class UsuarioService
    (IOptions<SankhyaClientSettings> sankhyaConfig)
    : 
        SessionService(sankhyaConfig),
        IAutoEnumerableEntityService<UsuarioEntity, UsuarioRequest>
{
    #region publicMethods

    public async Task<List<UsuarioEntity>> Inserir(List<UsuarioRequest> requests)
    {
        return await
            InsertRequest<UsuarioEntity, UsuarioProxy, UsuarioRequest>(
                requests, EntityNames.Usuario);
    }

    public async Task<UsuarioEntity> Inserir(UsuarioRequest request)
    {
        var response = await Inserir([request]);
        return response.First();
    }

    public async Task<List<UsuarioEntity>> Atualizar(List<UsuarioRequest> requests)
    {
        return await
            UpdateRequest<UsuarioEntity, UsuarioProxy, UsuarioRequest>(
                requests, EntityNames.Usuario);
    }

    public async Task<UsuarioEntity> Atualizar(UsuarioRequest request)
    {
        var response = await Atualizar([request]);
        return response.First();
    }

    public async Task<List<UsuarioEntity>> Recuperar(string query)
    {
        return await LoadRequest<UsuarioEntity, UsuarioProxy>(query, EntityNames.Usuario);
    }
    #endregion
}