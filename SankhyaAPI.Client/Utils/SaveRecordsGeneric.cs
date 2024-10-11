using System.Reflection;
using System.Xml.Linq;
using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Utils;

public static class SaveRecordsGeneric
{
    #region public Methods

    public static Task<ServiceRequest<TRequest>> CreateInsertEnvelope<TRequest, TResponse>(
        List<TRequest> objs,
        string entityName)
        where TRequest : class
        where TResponse : class, new()
    {
        var envelope = new ServiceRequest<TRequest>
        {
            ServiceName = ServiceNames.CrudServiceProviderSaveRecords,
            RequestBody = new RequestBody<TRequest>
            {
                DataSet = new DataSet<TRequest>
                {
                    RootEntity = entityName,
                    DataRow = objs.Select(obj => new DataRow<TRequest> { Entity = obj }).ToList()
                }
            }
        };
        var sankhyaEntity = new SankhyaEntity
        {
            Path = "",
            Field = ObjectFromArrayValues.GetFieldsFromObjectJsonAttribOnly(new TResponse())
        };
        envelope.RequestBody.DataSet.Entity.Add(sankhyaEntity);

        return Task.FromResult(envelope);
    }


    public static Task<ServiceRequest<TRequest>> CreateUpdateEnvelope<TRequest, TResponse>(
        List<TRequest> objs, string entityName)
        where TRequest : class, IObjectWithKey
        where TResponse : class, new()
    {
        var envelope = new ServiceRequest<TRequest>
        {
            ServiceName = ServiceNames.CrudServiceProviderSaveRecords,
            RequestBody = new RequestBody<TRequest>
            {
                DataSet = new DataSet<TRequest>
                {
                    RootEntity = entityName,
                    DataRow = objs.Select(obj =>
                    {
                        var data = new DataRow<TRequest>
                        {
                            Entity = obj,
                        };
                        data.Key.GetKeysAsXml(obj);
                        return data;
                    }).ToList()
                }
            }
        };
        var sankhyaEntity = new SankhyaEntity
            { Path = "", Field = ObjectFromArrayValues.GetFieldsFromObjectJsonAttribOnly(new TResponse()) };
        envelope.RequestBody.DataSet.Entity.Add(sankhyaEntity);
        return Task.FromResult(envelope);
    }

    #endregion

    #region Sobrecargas

    public static Task<ServiceRequest<TRequest>> CreateInsertEnvelope<TRequest, TResponse>(TRequest obj,
        string entityName) where TRequest : class where TResponse : class, new()
    {
        return CreateInsertEnvelope<TRequest, TResponse>([obj], entityName);
    }

    public static Task<ServiceRequest<T>> CreateInsertEnvelope<T>(T obj, string entityName) where T : class, new()
    {
        return CreateInsertEnvelope<T, T>([obj], entityName);
    }

    public static Task<ServiceRequest<TRequest>> CreateUpdateEnvelope<TRequest, TResponse>(TRequest obj,
        string entityName)
        where TRequest : class, IObjectWithKey
        where TResponse : class, new()
    {
        return CreateUpdateEnvelope<TRequest, TResponse>([obj], entityName);
    }

    public static Task<ServiceRequest<T>> CreateUpdateEnvelope<T>(List<T> objs, string entityName)
        where T : class, IObjectWithKey, new()
    {
        return CreateUpdateEnvelope<T, T>(objs, entityName);
    }

    #endregion

    #region privateMethods

    private static XElement GetKeysAsXml<T>(this XElement element, T obj)
    {
        if (obj == null) throw new Exception("A entidade deve ser definida");


        // Itera sobre as propriedades da entidade
        foreach (var prop in obj.GetType().GetProperties())
        {
            var keyAttribute = prop.GetCustomAttribute<PrimaryKeyElement>();
            if (keyAttribute != null)
            {
                var value = prop.GetValue(obj);
                if (value == null) throw new Exception($"Chave primária {keyAttribute.ElementName} não pode ser nula");

                // Adiciona cada chave primária como um elemento XML individual
                element.Add(new XElement(keyAttribute.ElementName, value));
            }
        }

        return element;
    }

    #endregion
}