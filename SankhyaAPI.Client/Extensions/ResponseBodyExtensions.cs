using SankhyaAPI.Client.Entities.Response.LoadEntities;
using SankhyaAPI.Client.Envelopes;

namespace SankhyaAPI.Client.Extensions;

public static class ResponseBodyExtensions
{
    public static LoginEntity GetLoginEntity(this ResponseBody<LoginEntity> responseBody)
    {
        return new LoginEntity
        {
            IdUsu = responseBody.IdUsuario,
            SessionId = responseBody.SessionId ?? string.Empty
        };
    }
}