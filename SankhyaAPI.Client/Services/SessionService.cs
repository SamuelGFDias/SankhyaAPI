using Microsoft.Extensions.Options;
using Refit;
using SankhyaAPI.Client.Entities.Response.LoadEntities;
using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Providers;
using SankhyaAPI.Client.Requests;
using SankhyaAPI.Client.Responses;
using SankhyaAPI.Client.Utils;

namespace SankhyaAPI.Client.Services;

public class SessionService(IOptions<SankhyaClientSettings> sankhyaApiConfig)
    : DefaultClientService(sankhyaApiConfig), ISankhyaApi
{
    private readonly SankhyaClientSettings _sankhyaConfig = sankhyaApiConfig.Value;
    private ApiResponse<ServiceResponse<LoginEntity>>? _apiResponse;
    private string _sessionId = string.Empty;
    public string JSessionId = string.Empty;

    #region "Private Methods"

    private async Task<LoginEntity> Login(string usuario, string interno)
    {
        var request = new ServiceRequest<LoginEntity>
        {
            OutputType = "xml",
            ServiceName = ServiceNames.MobileLoginSpLogin,
            RequestBody = new RequestBody<LoginEntity>
            {
                NomeUsu = usuario,
                Interno = interno
            }
        };
        _apiResponse = await ClientXml.Login(request);
        _apiResponse?.Content?.VerificarErros();
        ValidaResponseBodyLogin();
        return _apiResponse!.Content!.ResponseBody.GetLoginEntity();
    }

    private void ValidaResponseBodyLogin()
    {
        if (_apiResponse?.Content?.ResponseBody == null)
            throw new Exception("Alguma coisa saiu mal no login");
    }

    #endregion

    #region "Protected Methods"

    protected async Task<ApiResponse<ServiceResponse<T>>> Request<T>(
        Func<string, ServiceRequest<T>, Task<ApiResponse<ServiceResponse<T>>>> client, ServiceRequest<T> envelope)
        where T : class
    {
        await LoginSankhya(_sankhyaConfig.Usuario, _sankhyaConfig.Senha);
        var response = await client(JSessionId, envelope);
        await LogoutSankhya();
        return response;
    }

    protected async Task<ApiResponse<ServiceResponse<TResponse>>> RequestWithOtherResponse<TRequest, TResponse>(
        Func<string, ServiceRequest<TRequest>, Task<ApiResponse<ServiceResponse<TResponse>>>> client,
        ServiceRequest<TRequest> envelope)
        where TRequest : class
        where TResponse : class
    {
        await LoginSankhya(_sankhyaConfig.Usuario, _sankhyaConfig.Senha);
        var response = await client(JSessionId, envelope);
        await LogoutSankhya();
        return response;
    }

    protected async Task<List<TResponse>> LoadRequest<TResponse, TProxy>(
        string query,
        string entityName
    )
        where TResponse : class, IProxysable<TResponse, TProxy>, new()
        where TProxy : class
    {
        var envelope = await LoadRecordsGeneric.CreateLoadEnvelope<TProxy, TResponse>(entityName, query);
        var response = await Request(ClientXml.LoadRecordsGeneric, envelope);

        var entities =
            response.Content?.ResponseBody.Entities?.Entity
            ?? throw new Exception("Erro ao retornar registro");

        return entities.Select(e => new TResponse().FromProxy(e)).ToList();
    }

    protected async Task<List<TResponse>> UpdateRequest<TResponse, TProxy, TRequest>(
        List<TRequest> request,
        string entityName
    )
        where TResponse : class, IProxysable<TResponse, TProxy>, new()
        where TRequest : class, IObjectWithKey
        where TProxy : class
    {
        var envelope = await SaveRecordsGeneric.CreateUpdateEnvelope<TRequest, TResponse>(request, entityName);
        var response = await RequestWithOtherResponse(ClientXml.SaveRecordsGeneric<TRequest, TProxy>, envelope);

        var entities =
            response.Content?.ResponseBody.Entities?.Entity
            ?? throw new Exception("Erro ao retornar registro");

        return entities.Select(e => new TResponse().FromProxy(e)).ToList();
    }


    protected async Task<List<TResponse>> InsertRequest<TResponse, TProxy, TRequest>(
        List<TRequest> request,
        string entityName
    )
        where TResponse : class, IProxysable<TResponse, TProxy>, new()
        where TRequest : class
        where TProxy : class
    {
        var envelope = await SaveRecordsGeneric.CreateInsertEnvelope<TRequest, TResponse>(request, entityName);
        var response = await RequestWithOtherResponse(ClientXml.SaveRecordsGeneric<TRequest, TProxy>, envelope);

        response.Content?.VerificarErros();

        var entities =
            response.Content?.ResponseBody.Entities?.Entity
            ?? throw new Exception("Erro ao retornar registro");

        return entities.Select(e => new TResponse().FromProxy(e)).ToList();
    }

    #endregion

    #region "Public Methods"

    public async Task<List<T>> Query<T>(string script) where T : class, new()
    {
        var envelope = ExecuteQueryGeneric
            .CreateQueryEnvelope<T>(script);
        var response = await Request(ClientJson.Query, envelope);
        response.Content?.VerificarErros();
        var list = new List<T>();
        if (response.Content?.ResponseBody.Rows is { Count: > 0 })
            ObjectFromArrayValues.GetListOfObjectsFromObjectMatrix(list, response.Content?.ResponseBody.Rows!);
        return list;
    }

    public async Task<Dictionary<string, object?>> Query(string script)
    {
        var envelope = ExecuteQueryGeneric.CreateQueryEnvelope<object>(script);
        var response = await Request(ClientJson.Query, envelope);
        response.Content?.VerificarErros();

        var result = new Dictionary<string, object?>();

        if (response.Content?.ResponseBody.Rows is not { Count: > 0 }) return result;

        foreach (var row in response.Content.ResponseBody.Rows)
            for (var j = 0; j < row.Count; j++)
            {
                var fieldName = response.Content.ResponseBody.FieldsMetadata?[j].Name;
                var fieldValue = row[j];
                if (fieldName == null) continue;
                result[fieldName] = fieldValue;
            }

        return result;
    }

    public async Task<List<Dictionary<string, object?>>> QueryList(string script)
    {
        var envelope = ExecuteQueryGeneric.CreateQueryEnvelope<object>(script);
        var response = await Request(ClientJson.Query, envelope);
        response.Content?.VerificarErros();

        var result = new List<Dictionary<string, object?>>();

        if (response.Content?.ResponseBody.Rows is not { Count: > 0 }) return result;

        foreach (var row in response.Content.ResponseBody.Rows)
        {
            for (int j = 0; j < row.Count; j++)
            {
                var fieldName = response.Content.ResponseBody.FieldsMetadata?[j].Name;
                if (fieldName == null) continue;

                var fieldValue = row[j];
                var dict = new Dictionary<string, object?> { { fieldName, fieldValue } };
                result.Add(dict);
            }
        }

        return result;
    }

    public async Task<string> Logout(string sessionId)
    {
        var apiResponse = await ClientXml.Logout($"{sessionId}.master");
        apiResponse.Content?.VerificarErros();
        return apiResponse.Content!.TransactionId!;
    }

    public async Task<LoginEntity> LoginSankhya(string usuario, string interno)
    {
        var login = await Login(usuario, interno);
        _sessionId = login.SessionId;
        JSessionId = login.JSessionId;
        return login;
    }

    public async Task LogoutSankhya()
    {
        await Logout(_sessionId);
        _sessionId = string.Empty;
        JSessionId = string.Empty;
    }

    #endregion
}