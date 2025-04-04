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
/// Classe de serviço para gerenciar a abstração de sessões com o Sankhya.
/// Também inclui métodos para executar consultas usando SQL nativo.
/// </summary>
/// <param name="sankhyaApiConfig">Configurações de configuração para o cliente da API Sankhya.</param>
public abstract class SessionService(IOptions<SankhyaClientSettings> sankhyaApiConfig)
    : DefaultClientService(sankhyaApiConfig), ISankhyaApi
{
    /// <summary>
    /// Armazena as configurações do cliente Sankhya necessárias para autenticação e interação com a API.
    /// Esta variável contém informações como credenciais e outros parâmetros de configuração.
    /// </summary>
    private readonly SankhyaClientSettings _sankhyaConfig = sankhyaApiConfig.Value;

    /// <summary>
    /// Representa a resposta da API obtida a partir de uma chamada de serviço.
    /// Esta variável encapsula o resultado de uma operação assíncrona para respostas de serviços
    /// específicas para funcionalidades de login e verificações de validação.
    /// </summary>
    private ApiResponse<ServiceResponse<LoginEntity>>? _apiResponse;

    /// <summary>
    /// Representa o identificador atual da sessão autenticada com a API Sankhya.
    /// </summary>
    /// <remarks>
    /// Esta variável armazena o ID da sessão recuperado após o login no sistema Sankhya utilizando a API de Login.
    /// É essencial para manter o estado da sessão e é necessário para realizar operações que exigem autenticação.
    /// O valor é redefinido ao efetuar o logout.
    /// </remarks>
    private string _sessionId = string.Empty;

    /// <summary>
    /// Representa o identificador de sessão (JSessionId) para a sessão atual da API Sankhya.
    /// É utilizado para autenticar e manter o estado da sessão nos serviços da API Sankhya.
    /// Esta variável é atribuída após o login bem-sucedido e é limpa durante o logout.
    /// </summary>
    protected string JSessionId = string.Empty;

    #region "Private Methods"

    /// <summary>
    /// Realiza o login do usuário autenticando com as credenciais fornecidas e recupera os detalhes da sessão de login.
    /// </summary>
    /// <param name="usuario">O nome de usuário do usuário que está tentando fazer login.</param>
    /// <param name="interno">O identificador interno ou senha do usuário.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona. O resultado da tarefa contém os detalhes do login encapsulados em um objeto <see cref="LoginEntity"/>.</returns>
    private async Task<LoginEntity> Login(string usuario, string interno)
    {
        var request = new ServiceRequest<LoginEntity>
        {
            OutputType = "xml", RequestBody = new RequestBody<LoginEntity> { NomeUsu = usuario, Interno = interno }
        };
        request.SetServiceName(EServiceNames.Login);
        _apiResponse = await ClientXml.Login(request);
        _apiResponse?.Content?.VerificarErros();
        ValidaResponseBodyLogin();
        return _apiResponse!.Content!.ResponseBody.GetLoginEntity();
    }

    /// <summary>
    /// Executa o processo de logout de uma sessão específica no sistema Sankhya.
    /// </summary>
    /// <param name="sessionId">Identificador da sessão que será encerrada.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona de logout.</returns>
    private async Task Logout(string sessionId)
    {
        ApiResponse<ServiceResponse<object>> apiResponse = await ClientXml.Logout($"{sessionId}.master");
        apiResponse.Content?.VerificarErros();
    }

    /// <summary>
    /// Valida o corpo da resposta de uma operação de login.
    /// Lança uma exceção caso o corpo da resposta seja nulo, indicando uma falha no processo de login.
    /// </summary>
    /// <exception cref="Exception">Lançada quando o corpo da resposta é nulo.</exception>
    private void ValidaResponseBodyLogin()
    {
        if (_apiResponse?.Content?.ResponseBody == null)
            throw new Exception("Alguma coisa saiu mal no login");
    }

    /// <summary>
    /// Executes a client request with authentication, ensuring that proper login and logout actions are performed before and after the request.
    /// </summary>
    /// <typeparam name="T">The type of the entity to be processed.</typeparam>
    /// <param name="client">The function representing the client operation to be executed.</param>
    /// <param name="envelope">The envelope containing the request data to be sent to the client.</param>
    /// <returns>Returns a task representing the asynchronous operation. The task result contains an <see cref="ApiResponse{T}"/> wrapping a <see cref="ServiceResponse{T}"/> object with the operation result.</returns>
    private async Task<ApiResponse<ServiceResponse<T>>> Execute<T>
    (
        Func<
            string,
            ServiceRequest<T>,
            Task<ApiResponse<ServiceResponse<T>>>> client,
        ServiceRequest<T> envelope
    )
        where T : class, new()
    {
        await LoginSankhya(_sankhyaConfig.Usuario, _sankhyaConfig.Senha);
        ApiResponse<ServiceResponse<T>> response = await client(JSessionId, envelope);
        await LogoutSankhya();
        return response;
    }

    /// <summary>
    /// Executa uma consulta SQL na forma de script e retorna o corpo da resposta processado.
    /// </summary>
    /// <typeparam name="T">O tipo que representa a estrutura dos dados retornados pela consulta.</typeparam>
    /// <param name="script">O script da consulta SQL a ser executado.</param>
    /// <returns>O corpo da resposta contendo os dados recuperados da consulta.</returns>
    /// <exception cref="Exception">Lançada se houver um erro de sintaxe no script SQL fornecido.</exception>
    private async Task<ResponseBody<T>> ExecuteQuery<T>(string script)
        where T : class, new()
    {
        ApiResponse<ServiceResponse<T>> response = await Execute
                                                   (
                                                       ClientJson.Query,
                                                       ExecuteQueryGeneric.CreateQueryEnvelope<T>(script)
                                                   );

        if (response.Content?.ResponseBody == null)
        {
            throw new Exception("Erro de sintaxe na consulta. Confira o script SQL passado como parâmetro.");
        }

        response.Content?.VerificarErros();
        return response.Content!.ResponseBody;
    }

    #endregion

    #region "Protected Methods"

    /// <summary>
    /// Carrega e recupera os dados de uma consulta para uma lista de objetos do tipo especificado.
    /// </summary>
    /// <typeparam name="T">O tipo da entidade que será retornada na lista.</typeparam>
    /// <param name="query">A string contendo a consulta que será executada.</param>
    /// <param name="entityName">O nome da entidade pela qual os dados serão buscados.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona. O resultado da tarefa contém uma lista de objetos do tipo especificado com os dados recuperados.</returns>
    protected async Task<List<T>> LoadRequest<T>(string query, string entityName)
        where T : class, new()
    {
        ApiResponse<ServiceResponse<T>> response = await Execute
                                                   (
                                                       ClientXml.LoadRecordsGeneric,
                                                       LoadRecordsGeneric.CreateLoadEnvelope<T>(entityName, query)
                                                   );
        response.Content?.VerificarErros();

        List<T>? entities = response.Content?.ResponseBody.Entities?.Entity;

        return entities ?? [];
    }

    /// <summary>
    /// Atualiza uma lista de entidades em uma coleção de entidades especificada no sistema.
    /// </summary>
    /// <typeparam name="T">O tipo das entidades a serem atualizadas.</typeparam>
    /// <param name="requests">A lista de entidades a serem atualizadas.</param>
    /// <param name="entityName">O nome da coleção de entidades onde as atualizações serão aplicadas.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona. O resultado da tarefa contém uma lista de entidades atualizadas.</returns>
    protected async Task<List<T>> UpdateRequest<T>(List<T> requests, string entityName)
        where T : class, new()
    {
        ApiResponse<ServiceResponse<T>> response = await Execute
                                                   (
                                                       ClientXml.SaveRecordsGeneric,
                                                       SaveRecordsGeneric.CreateUpdateEnvelope(requests, entityName)
                                                   );
        response.Content?.VerificarErros();

        List<T>? entities = response.Content?.ResponseBody.Entities?.Entity;

        return entities ?? [];
    }

    /// <summary>
    /// Creates and sends a request to insert multiple records into a specified entity and returns a list of the saved entities.
    /// </summary>
    /// <typeparam name="T">The type of the entity being processed.</typeparam>
    /// <param name="requests">A list of entities to be saved.</param>
    /// <param name="entityName">The name of the entity to which the records will be saved.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a list of saved entities of type <typeparamref name="T"/>.</returns>
    protected async Task<List<T>> CreateRequest<T>(List<T> requests, string entityName)
        where T : class, new()
    {
        ApiResponse<ServiceResponse<T>> response = await Execute
                                                   (
                                                       ClientXml.SaveRecordsGeneric,
                                                       SaveRecordsGeneric.CreateInsertEnvelope(requests, entityName)
                                                   );
        response.Content?.VerificarErros();

        List<T>? entities = response.Content?.ResponseBody.Entities?.Entity;

        return entities ?? [];
    }

    /// <summary>
    /// Executes a SQL query string, maps the results into a list of objects, and returns the populated list.
    /// </summary>
    /// <typeparam name="T">The type of objects to which the results will be mapped.</typeparam>
    /// <param name="script">A SQL query string that defines the data retrieval logic.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a list of objects of type T populated with the data retrieved from the query.</returns>
    protected async Task<List<T>> Query<T>(string script)
        where T : class, new()
    {
        ResponseBody<T> response = await ExecuteQuery<T>(script);

        if (response.FieldsMetadata is null) return [];

        var fields = new Dictionary<string, List<object>?>();
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

    /// <summary>
    /// Executes a raw SQL script and retrieves the results as a list of dictionaries.
    /// Each dictionary represents a row with column names as keys and their respective values.
    /// </summary>
    /// <param name="script">The SQL query script to execute.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of dictionaries where each dictionary represents a row with column names as keys and associated values.</returns>
    protected async Task<List<Dictionary<string, dynamic?>>> Query(string script)
    {
        ResponseBody<object> response = await ExecuteQuery<object>(script);

        if (response.FieldsMetadata is null) return [];

        var fields = new List<Dictionary<string, object?>>();
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

                fields[j]
                   .Add
                    (
                        key,
                        value switch
                        {
                            string s =>
                                DateTime.TryParseExact
                                (
                                    s,
                                    "ddMMyyyy HH:mm:ss",
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.None,
                                    out DateTime date
                                )
                                    ? date
                                    : s.Trim(),
                            _ => value
                        }
                    );
            }
        }

        return fields;
    }

    #endregion

    #region "Public Methods"

    /// <summary>
    /// Realiza o login no Sankhya, estabelecendo uma nova sessão para interação com a API.
    /// Deve-se garantir que o método <see cref="LogoutSankhya"/> seja chamado ao término do uso da sessão.
    /// </summary>
    /// <param name="usuario">O nome do usuário utilizado para autenticação no Sankhya.</param>
    /// <param name="interno">A senha ou identificador interno para autenticação.</param>
    /// <returns>Um objeto <see cref="LoginEntity"/> contendo as informações da sessão, como o identificador da sessão e dados relacionados.</returns>
    public async Task<LoginEntity> LoginSankhya(string usuario, string interno)
    {
        LoginEntity login = await Login(usuario, interno);
        _sessionId = login.SessionId;
        JSessionId = login.JSessionId;
        return login;
    }

    /// <summary>
    /// Método para encerrar a sessão ativa no sistema Sankhya.
    /// Este método limpa os identificadores de sessão e finaliza a comunicação com a API.
    /// </summary>
    /// <returns>Uma tarefa assíncrona que representa a operação de logout do usuário na API Sankhya.</returns>
    public async Task LogoutSankhya()
    {
        await Logout(_sessionId);
        _sessionId = string.Empty;
        JSessionId = string.Empty;
    }

    #endregion
}