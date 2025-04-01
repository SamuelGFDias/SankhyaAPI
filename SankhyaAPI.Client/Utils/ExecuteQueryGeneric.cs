using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Utils;

internal static class ExecuteQueryGeneric
{
    public static ServiceRequest<T> CreateQueryEnvelope<T>(string script) where T : class, new()
    {
        var envelope = new ServiceRequest<T>
        {
            RequestBody = new RequestBody<T>
            {
                Sql = script
            }
        };
        envelope.SetServiceName(EServiceNames.ExecuteQuery);
        return envelope; 
    }
}