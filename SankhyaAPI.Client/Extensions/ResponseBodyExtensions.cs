using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Responses;

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