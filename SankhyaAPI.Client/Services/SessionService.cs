using Refit;
using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Requests;
using SankhyaAPI.Client.Responses;
using SankhyaAPI.Client.Utils;
using System.Globalization;

namespace SankhyaAPI.Client.Services;

/// <summary>
/// Serviço responsável por gerenciar sessões e realizar operações na API Sankhya.
/// Esta classe fornece funcionalidades para login, logout, execução de consultas,
/// e operações CRUD em entidades da API Sankhya.
/// </summary>
public class SessionService(string baseUrl, string userName, string password) : DefaultClientService(baseUrl), ISankhyaApi
{
    /// <summary>
    /// Representa a resposta da API obtida a partir de uma chamada de serviço.
    /// Esta variável armazena o resultado de operações assíncronas, como autenticação ou validação,
    /// encapsulando o conteúdo e possíveis erros retornados pela API.
    /// </summary>
    private ApiResponse<ServiceResponse<LoginEntity>>? _apiResponse;

    /// <summary>
    /// Representa o identificador único da sessão atualmente autenticada com a API Sankhya.
    /// </summary>
    /// <remarks>
    /// Essa variável armazena o ID da sessão, que é obtido após a autenticação bem-sucedida
    /// e é utilizado para realizar chamadas subsequentes que requerem autenticação.
    /// O valor desta variável é redefinido ao realizar o logout.
    /// </remarks>
    private string _sessionId = string.Empty;

    /// <summary>
    /// Representa o identificador de sessão (JSessionId) associado à sessão ativa da API Sankhya.
    /// É utilizado para autenticar solicitações e manter o estado da sessão ao interagir com os serviços da API.
    /// O valor é atribuído no momento do login bem-sucedido e redefinido no logout.
    /// </summary>
    protected string JSessionId = string.Empty;

    /// <summary>
    /// Realiza o login do usuário, autenticando com as credenciais fornecidas e recupera os detalhes da sessão de login.
    /// </summary>
    /// <param name="usuario">O nome de usuário do usuário que está tentando fazer login.</param>
    /// <param name="interno">O identificador interno ou senha do usuário.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona. O resultado da tarefa contém os detalhes do login encapsulados em um objeto <see cref="LoginEntity"/>.</returns>
    private async Task<LoginEntity> Login(string usuario, string interno)
    {
        var request = new ServiceRequest<LoginEntity>
        {
            OutputType = "xml", RequestBody = new RequestBody { NomeUsu = usuario, Interno = interno }
        };

        if (string.IsNullOrWhiteSpace(request.RequestBody.NomeUsu))
            throw new ArgumentException("Usuário não pode ser vazio", nameof(usuario));

        if (string.IsNullOrWhiteSpace(interno))
            throw new ArgumentException("Senha não pode ser vazia", nameof(interno));

        request.SetServiceName(EServiceNames.Login);
        _apiResponse = await ClientXml.Login(request);
        _apiResponse?.Content?.VerificarErros();
        ValidaResponseBodyLogin();
        return _apiResponse!.Content!.ResponseBody.GetLoginEntity();
    }

    /// <summary>
    /// Encerra a sessão especificada no sistema Sankhya realizando o processo de logout.
    /// </summary>
    /// <param name="sessionId">O identificador único da sessão que deve ser terminada.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona de logout.</returns>
    private async Task Logout(string sessionId)
    {
        ApiResponse<ServiceResponse<object>> apiResponse = await ClientXml.Logout($"{sessionId}.master");
        apiResponse.Content?.VerificarErros();
    }

    /// <summary>
    /// Valida o corpo da resposta de uma operação de login.
    /// Lança uma exceção caso o corpo da resposta seja nulo, indicando uma falha no processo de autenticação.
    /// </summary>
    /// <exception cref="Exception">Lançada quando o corpo da resposta é nulo ou inválido.</exception>
    private void ValidaResponseBodyLogin()
    {
        if (_apiResponse?.Content?.ResponseBody == null)
            throw new Exception("Alguma coisa saiu mal no login");
    }

    /// <summary>
    /// Executa uma solicitação do cliente autenticada, garantindo que ações de login e logout sejam realizadas antes e depois da operação.
    /// </summary>
    /// <typeparam name="T">O tipo da entidade a ser processada.</typeparam>
    /// <param name="client">A função que representa a operação do cliente a ser executada.</param>
    /// <param name="envelope">O envelope contendo os dados da solicitação a serem enviados ao cliente.</param>
    /// <returns>Retorna uma tarefa que representa a operação assíncrona. O resultado da tarefa contém um <see cref="ApiResponse{T}"/> encapsulando um objeto <see cref="ServiceResponse{T}"/> com o resultado da operação.</returns>
    private async Task<ApiResponse<ServiceResponse<T>>> Execute<T>(
        Func<string, ServiceRequest<T>, Task<ApiResponse<ServiceResponse<T>>>> client,
        ServiceRequest<T> envelope
    )
        where T : class, new()
    {
        await LoginSankhya(userName, password);
        ApiResponse<ServiceResponse<T>> response = await client(JSessionId, envelope);
        await LogoutSankhya();
        return response;
    }

    /// <summary>
    /// Executa uma consulta SQL fornecida no formato de script e retorna o corpo da resposta processado.
    /// </summary>
    /// <typeparam name="T">O tipo que representa a estrutura dos dados retornados pela consulta.</typeparam>
    /// <param name="script">O script SQL que será executado como consulta.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona. O resultado da tarefa contém o corpo da resposta com os dados recuperados da consulta encapsulados em um objeto <see cref="ResponseBody{T}"/>.</returns>
    /// <exception cref="Exception">Lançada se houver um erro de sintaxe no script SQL fornecido.</exception>
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
        ApiResponse<ServiceResponse<T>> response = await Execute(
                                                       ClientXml.LoadRecordsGeneric,
                                                       LoadRecordsGeneric.CreateLoadEnvelope<T>(entityName, query));
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
        ApiResponse<ServiceResponse<T>> response = await Execute(
                                                       ClientXml.SaveRecordsGeneric,
                                                       SaveRecordsGeneric.CreateUpdateEnvelope(requests, entityName));
        response.Content?.VerificarErros();

        List<T>? entities = response.Content?.ResponseBody.Entities?.Entity;

        return entities ?? [];
    }

    /// <summary>
    /// Remove registros de uma entidade específica no sistema Sankhya utilizando as requisições fornecidas.
    /// </summary>
    /// <param name="requests">Uma lista de objetos representando os registros que devem ser removidos.</param>
    /// <param name="entityName">O nome da entidade da qual os registros serão removidos.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona. Não retorna valor.</returns>
    protected async Task DeleteRequest<T>(List<T> requests, string entityName)
        where T : class, new()
    {
        ApiResponse<ServiceResponse<T>> response = await Execute(
                                                       ClientXml.RemoveRecordsGeneric,
                                                       RemoveRecordsGeneric.CreateRemoveEnvelope(requests, entityName));
        response.Content?.VerificarErros();
    }

    /// <summary>
    /// Cria e envia uma solicitação para inserir vários registros em uma entidade especificada e retorna uma lista das entidades salvas.
    /// </summary>
    /// <typeparam name="T">O tipo da entidade sendo processada.</typeparam>
    /// <param name="requests">Uma lista de entidades a serem salvas.</param>
    /// <param name="entityName">O nome da entidade na qual os registros serão salvos.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona. O resultado da tarefa contém uma lista das entidades salvas do tipo <typeparamref name="T"/>.</returns>
    protected async Task<List<T>> CreateRequest<T>(List<T> requests, string entityName)
        where T : class, new()
    {
        ApiResponse<ServiceResponse<T>> response = await Execute(
                                                       ClientXml.SaveRecordsGeneric,
                                                       SaveRecordsGeneric.CreateInsertEnvelope(requests, entityName));
        response.Content?.VerificarErros();

        List<T>? entities = response.Content?.ResponseBody.Entities?.Entity;

        return entities ?? [];
    }

    /// <summary>
    /// Executa uma string de consulta SQL, mapeia os resultados em uma lista de objetos e retorna a lista populada.
    /// </summary>
    /// <typeparam name="T">O tipo de objetos para os quais os resultados serão mapeados.</typeparam>
    /// <param name="script">Uma string de consulta SQL que define a lógica de recuperação de dados.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona. O resultado da tarefa contém uma lista de objetos do tipo T preenchida com os dados recuperados da consulta.</returns>
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
    /// Executa um script SQL bruto e recupera os resultados como uma lista de dicionários.
    /// Cada dicionário representa uma linha, onde os nomes das colunas são as chaves e os valores correspondentes são os valores associados.
    /// </summary>
    /// <param name="script">O script de consulta SQL a ser executado.</param>
    /// <returns>Uma tarefa que representa a operação assíncrona. O resultado da tarefa contém uma lista de dicionários, onde cada dicionário representa uma linha com nomes de colunas como chaves e seus valores associados.</returns>
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
                   .Add(
                        key,
                        value switch
                        {
                            string s => DateTime.TryParseExact(
                                            s,
                                            "ddMMyyyy HH:mm:ss",
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.None,
                                            out DateTime date)
                                            ? date
                                            : s.Trim(),
                            _ => value
                        });
            }
        }

        return fields;
    }


    /// <summary>
    /// Realiza o login no Sankhya, estabelecendo uma sessão autenticada para comunicação com a API.
    /// Este método armazena o identificador da sessão e outros detalhes necessários para
    /// a execução subsequente de operações na API Sankhya.
    /// </summary>
    /// <param name="usuario">O nome de usuário utilizado para autenticação no Sankhya.</param>
    /// <param name="interno">A senha ou identificador interno utilizado para autenticação.</param>
    /// <returns>Um objeto <see cref="LoginEntity"/> contendo detalhes sobre a sessão criada, incluindo o identificador da sessão e informações relacionadas.</returns>
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
}