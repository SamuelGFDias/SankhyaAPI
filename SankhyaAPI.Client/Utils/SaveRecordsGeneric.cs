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

    public static ServiceRequest<T> CreateInsertEnvelope<T>(
        List<T> objs,
        Enum entityName)
        where T : class, new()
    {
        ObjectUtilsMethods.ValidarCamposChave(objs);
        var envelope = new ServiceRequest<T>
        {
            ServiceName = ServiceNames.CrudServiceProviderSaveRecords,
            RequestBody = new RequestBody<T>
            {
                DataSet = new DataSet
                {
                    DataRow = objs.Select(obj => new DataRow
                    {
                        Entity = obj.GetEntityForFields()
                    }).ToList()
                }
            }
        };
        var sankhyaEntity = new SankhyaEntity
        {
            Path = "",
            Field = ObjectUtilsMethods.GetFieldsFromObject(new T())
        };
        envelope.RequestBody.DataSet.SetRootEntity(entityName);
        envelope.RequestBody.DataSet.Entity.Add(sankhyaEntity);

        return envelope;
    }


    public static ServiceRequest<T> CreateUpdateEnvelope<T>(
        List<T> objs,
        Enum entityName)
        where T : class, new()
    {
        ObjectUtilsMethods.ValidarCamposChave(objs, true);
        var envelope = new ServiceRequest<T>
        {
            ServiceName = ServiceNames.CrudServiceProviderSaveRecords,
            RequestBody = new RequestBody<T>
            {
                DataSet = new DataSet
                {
                    DataRow = objs.Select(obj =>
                    {
                        var data = new DataRow
                        {
                            Entity = obj.GetEntityForFields(true),
                        };
                        data.Key.GetKeysAsXml(obj);
                        return data;
                    }).ToList()
                }
            }
        };
        var sankhyaEntity = new SankhyaEntity
        {
            Path = "",
            Field = ObjectUtilsMethods.GetFieldsFromObject(new T())
        };
        envelope.RequestBody.DataSet.SetRootEntity(entityName);
        envelope.RequestBody.DataSet.Entity.Add(sankhyaEntity);
        return envelope;
    }

    #endregion

    #region privateMethods

    private static XElement GetKeysAsXml<T>(this XElement element, T obj)
    {
        if (obj == null) throw new Exception("A entidade deve ser definida");


        // Itera sobre as propriedades da entidade
        foreach (var prop in obj.GetType().GetProperties())
        {
            var keyAttribute = prop.GetCustomAttribute<PrimaryKeyElementAttribute>();
            if (keyAttribute == null) continue;

            object? value = prop.GetValue(obj);

            if (value == null) throw new Exception($"Chave primária {keyAttribute.ElementName} não pode ser nula");

            element.Add(new XElement(keyAttribute.ElementName, ObjectUtilsMethods.GetFormattedString(value)));
        }

        return element;
    }

    private static XElement GetEntityForFields<T>(this T obj, bool isUpdate = false) where T : class
    {
        var localFields = new XElement("localFields");

        foreach (var prop in obj.GetType().GetProperties())
        {
            var keyAttribute = prop.GetCustomAttribute<PrimaryKeyElementAttribute>();
            if (keyAttribute != null)
            {
                if (isUpdate || keyAttribute.AutoEnumerable) continue;
            }

            var xmlElementName = prop.GetXmlElementName();
            object? value = prop.GetValue(obj);

            if (xmlElementName == null || value == null) continue;

            localFields.Add(new XElement(xmlElementName, ObjectUtilsMethods.GetFormattedString(value)));
        }

        return localFields;
    }

    #endregion
}