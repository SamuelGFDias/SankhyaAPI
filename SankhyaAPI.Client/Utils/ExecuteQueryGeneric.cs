using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Utils;

public static class ExecuteQueryGeneric
{
    public static ServiceRequest<T> CreateQueryEnvelope<T>(string script) where T : class
    {
        var envelope = new ServiceRequest<T>
        {
            ServiceName = ServiceNames.DbExplorerSpExecuteQuery,
            RequestBody = new RequestBody<T>
            {
                Sql = script
            }
        };
        return envelope;
    }
}