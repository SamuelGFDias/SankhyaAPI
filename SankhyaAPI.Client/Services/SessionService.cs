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
    protected string JSessionId = string.Empty;

    #region "Private Methods"

    private async Task<LoginEntity> Login(string usuario, string interno)
    {
        ServiceRequest<LoginEntity> request = new ServiceRequest<LoginEntity>
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
        ApiResponse<ServiceResponse<object>> apiResponse = await ClientXml.Logout($"{sessionId}.master");
        apiResponse.Content?.VerificarErros();
    }

    private void ValidaResponseBodyLogin()
    {
        if (_apiResponse?.Content?.ResponseBody == null)
            throw new Exception("Alguma coisa saiu mal no login");
    }

    private async Task<ApiResponse<ServiceResponse<T>>> Execute<T>(
        Func<
            string,
            ServiceRequest<T>,
            Task<ApiResponse<ServiceResponse<T>>>> client,
        ServiceRequest<T> envelope)
        where T : class, new()
    {
        await LoginSankhya(_sankhyaConfig.Usuario, _sankhyaConfig.Senha);
        ApiResponse<ServiceResponse<T>> response = await client(JSessionId, envelope);
        await LogoutSankhya();
        return response;
    }

    private async Task<ResponseBody<T>> ExecuteQuery<T>(string script)
        where T : class, new()
    {
        ApiResponse<ServiceResponse<T>> response = await Execute(ClientJson.Query, ExecuteQueryGeneric.CreateQueryEnvelope<T>(script));

        if (response.Content?.ResponseBody == null)
        {
            throw new Exception("Erro de sintaxe na consulta. Confira o script SQL passado como parâmetro.");
        }

        response.Content?.VerificarErros();
        return response.Content!.ResponseBody;
    }

    #endregion

    #region "Protected Methods"

    protected async Task<List<T>> LoadRequest<T>(string query, Enum entityName)
        where T : class, new()
    {
        ApiResponse<ServiceResponse<T>> response = await Execute(ClientXml.LoadRecordsGeneric,
            LoadRecordsGeneric.CreateLoadEnvelope<T>(entityName, query));
        response.Content?.VerificarErros();

        List<T> entities =
            response.Content?.ResponseBody.Entities?.Entity
            ?? throw new NullReferenceException("Nenhum registro retornado");

        return entities;
    }

    protected async Task<List<T>> UpdateRequest<T>(List<T> requests, Enum entityName)
        where T : class, new()
    {
        ApiResponse<ServiceResponse<T>> response = await Execute(ClientXml.SaveRecordsGeneric,
            SaveRecordsGeneric.CreateUpdateEnvelope(requests, entityName));
        response.Content?.VerificarErros();

        List<T> entities =
            response.Content?.ResponseBody.Entities?.Entity
            ?? throw new NullReferenceException("Nenhum registro retornado");

        return entities;
    }

    protected async Task<List<T>> InsertRequest<T>(List<T> requests, Enum entityName)
        where T : class, new()
    {
        ApiResponse<ServiceResponse<T>> response = await Execute(ClientXml.SaveRecordsGeneric,
            SaveRecordsGeneric.CreateInsertEnvelope(requests, entityName));
        response.Content?.VerificarErros();

        List<T> entities =
            response.Content?.ResponseBody.Entities?.Entity
            ?? throw new NullReferenceException("Nenhum registro retornado");

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
        ResponseBody<T> response = await ExecuteQuery<T>(script);

        if (response.FieldsMetadata is null) return [];

        Dictionary<string, List<object>?> fields = new Dictionary<string, List<object>?>();
        List<string> keys = response.FieldsMetadata.Select(a => a.Name).ToList();
        List<List<object>>? rows = response.Rows;

        for (int i = 0; i < keys.Count; i++)
        {
            int order = response.FieldsMetadata[i].Order - 1;
            string key = keys[order];
            List<object>? values = rows?.Select(t => t[order]).ToList();

            fields.Add(key, values);
        }

        List<T> list = ObjectUtilsMethods.GetListOfObjectsFromDictionary<T>(fields);

        return list;
    }

    public async Task<List<Dictionary<string, dynamic?>>> Query(string script)
    {
        ResponseBody<object> response = await ExecuteQuery<object>(script);

        if (response.FieldsMetadata is null) return [];

        List<Dictionary<string, object?>> fields = new List<Dictionary<string, object?>>();
        List<string> keys = response.FieldsMetadata.Select(a => a.Name).ToList();
        List<List<object>>? rows = response.Rows;

        for (int i = 0; i < keys.Count; i++)
        {
            int order = response.FieldsMetadata[i].Order - 1;
            string key = keys[order];
            List<object>? values = rows?.Select(t => t[order]).ToList();

            for (int j = 0; j < values?.Count; j++)
            {
                if (fields.Count <= j) fields.Add(new());
                object value = values[j];

                fields[j].Add(key,
                    value switch
                    {
                        string s =>
                            DateTime.TryParseExact(
                                s, "ddMMyyyy HH:mm:ss",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None, out DateTime date)
                                ? date
                                : s.Trim(),
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
        LoginEntity login = await Login(usuario, interno);
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