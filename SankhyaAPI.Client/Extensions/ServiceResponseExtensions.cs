using SankhyaAPI.Client.Responses;

namespace SankhyaAPI.Client.Extensions;

public static class ServiceResponseExtensions
{
    public static void VerificarErros<TEntity>(
        this ServiceResponse<TEntity> serviceResponse)
        where TEntity : class
    {
        if (serviceResponse.StatusMessage != null)
            throw new Exception($"SNK Error: {serviceResponse.StatusMessage}");

        if (serviceResponse.Error.StatusMessage != null)
            throw new Exception(
                $"SNK Error: {serviceResponse.Error.TsErrorCode} - {serviceResponse.Error.StatusMessage}");
    }
}