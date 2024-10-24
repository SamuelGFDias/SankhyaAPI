using Azure.Core;
using Microsoft.Extensions.Options;
using Refit;
using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Providers;
using SankhyaAPI.Client.Requests;
using SankhyaAPI.Client.Responses;
using SankhyaAPI.Client.Utils;
using System.Globalization;

namespace SankhyaAPI.Client.Services;


/// <summary>
/// Classe de serviço para abstração de sessão com o Sankhya. Também contém métodos para execução de queries com SQL Nativo.
/// </summary>
/// <param name="sankhyaApiConfig"></param>
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

    private async Task Logout(string sessionId)
    {
        var apiResponse = await ClientXml.Logout($"{sessionId}.master");
        apiResponse.Content?.VerificarErros();
    }

    private void ValidaResponseBodyLogin()
    {
        if (_apiResponse?.Content?.ResponseBody == null)
            throw new Exception("Alguma coisa saiu mal no login");
    }

    private async Task<ResponseBody<T>> ExecuteQuery<T>(string script)
        where T : class
    {
        var envelope = ExecuteQueryGeneric.CreateQueryEnvelope<T>(script);
        var response = await Request(ClientJson.Query, envelope);
        if (response.Content?.ResponseBody == null)
            throw new Exception("Erro de sintaxe na consulta. Confira o script SQL passado como parâmetro.");
        response.Content?.VerificarErros();

        return response.Content!.ResponseBody;
    }
    private async Task<ApiResponse<ServiceResponse<T>>> Request<T>(
        Func<string, ServiceRequest<T>, Task<ApiResponse<ServiceResponse<T>>>> client, ServiceRequest<T> envelope)
        where T : class
    {
        await LoginSankhya(_sankhyaConfig.Usuario, _sankhyaConfig.Senha);
        var response = await client(JSessionId, envelope);
        await LogoutSankhya();
        return response;
    }

    private async Task<ApiResponse<ServiceResponse<TResponse>>> RequestWithOtherResponse<TRequest, TResponse>(
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

    #endregion

    #region "Protected Methods"

    protected async Task<List<T>> LoadRequest<T>(
        string query,
        Enum entityName
    )
        where T : class, new()
    {
        var envelope = LoadRecordsGeneric.CreateLoadEnvelope<T>(entityName, query);
        var response = await Request(ClientXml.LoadRecordsGeneric, envelope);

        var entities =
            response.Content?.ResponseBody.Entities?.Entity
            ?? throw new Exception("Erro ao retornar registro");

        return entities;
    }

    protected async Task<List<T>> UpdateRequest<T>(
        List<T> requests,
        Enum entityName
    )
        where T : class, new()
    {
        var envelope = SaveRecordsGeneric.CreateUpdateEnvelope(requests, entityName);
        var response = await RequestWithOtherResponse(ClientXml.SaveRecordsGeneric, envelope);

        var entities =
            response.Content?.ResponseBody.Entities?.Entity
            ?? throw new Exception("Erro ao retornar registro");

        return entities;
    }


    protected async Task<List<T>> InsertRequest<T>(
        List<T> requests,
        Enum entityName
    )
        where T : class, new()
    {
        var envelope = SaveRecordsGeneric.CreateInsertEnvelope(requests, entityName);
        var response = await RequestWithOtherResponse(ClientXml.SaveRecordsGeneric, envelope);

        response.Content?.VerificarErros();

        var entities =
            response.Content?.ResponseBody.Entities?.Entity
            ?? throw new Exception("Erro ao retornar registro");

        return entities;

    }

    #endregion

    #region "Public Methods"


    /// <summary>
    /// Método para mapeamento de dados de uma query em SQL Nativo para uma lista de objetos. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="script">Parâmetro que recebe script em SQL Nativo para retorno de dados.</param>
    /// <returns></returns>
    public async Task<List<T>> Query<T>(string script) where T : class, new()
    {
        var response = await ExecuteQuery<T>(script);

        if (response.FieldsMetadata is null) return [];

        var fields = new Dictionary<string, List<dynamic>?>();
        var keys = response.FieldsMetadata.Select(a => a.Name).ToList();
        var rows = response.Rows;

        for (int i = 0; i < keys.Count; i++)
        {
            int order = response.FieldsMetadata[i].Order - 1;
            string key = keys[order];
            var values = rows?.Select(t => t[order]).ToList();

            fields.Add(key, values);
        }

        var list = ObjectUtilsMethods.GetListOfObjectsFromDictionary<T>(fields);

        return list;
    }

    public async Task<List<Dictionary<string, dynamic?>>> Query(string script)
    {
        var response = await ExecuteQuery<object>(script);

        if (response.FieldsMetadata is null) return [];

        var fields = new List<Dictionary<string, dynamic?>>();
        var keys = response.FieldsMetadata.Select(a => a.Name).ToList();
        var rows = response.Rows;

        for (var i = 0; i < keys.Count; i++)
        {
            int order = response.FieldsMetadata[i].Order - 1;
            string key = keys[order];
            var values = rows?.Select(t => t[order]).ToList();

            for (int j = 0; j < values?.Count; j++)
            {
                if (fields.Count <= j) fields.Add(new());
                var value = values[j];


                fields[j].Add(key,
                    value switch
                    {
                        string s => DateTime.TryParseExact(s, "ddMMyyyy HH:mm:ss", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var date) ? date : s.Trim(),
                        _ => value
                    });
            }
        }

        return fields;
    }

    /// <summary>
    /// Método para abertura de sessão no Sankhya. Deve-se sempre fechar a conexão usando <see cref="LogoutSankhya"/> após o uso.
    /// </summary>
    /// <param name="usuario"></param>
    /// <param name="interno"></param>
    /// <returns>O retorno será a classe <see cref="LoginEntity"/> com as propriedades referentes a sessão</returns>
    public async Task<LoginEntity> LoginSankhya(string usuario, string interno)
    {
        var login = await Login(usuario, interno);
        _sessionId = login.SessionId;
        JSessionId = login.JSessionId;
        return login;
    }

    /// <summary>
    /// Método para fechamento de sessão no Sankhya.
    /// </summary>
    /// <returns></returns>
    public async Task LogoutSankhya()
    {
        await Logout(_sessionId);
        _sessionId = string.Empty;
        JSessionId = string.Empty;
    }

    #endregion
}