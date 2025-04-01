using System.Reflection;
using System.Xml.Linq;
using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.MetaData;
using SankhyaAPI.Client.Requests;

namespace SankhyaAPI.Client.Utils;

internal static class SaveRecordsGeneric
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
        envelope.SetServiceName(EServiceNames.SaveRecords);
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
        envelope.SetServiceName(EServiceNames.RemoveRecords);
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
        foreach (PropertyInfo? prop in obj.GetType().GetProperties())
        {
            var keyAttribute = prop.GetCustomAttribute<PrimaryKeyElementAttribute>();
            if (keyAttribute == null) continue;

            object? value = prop.GetValue(obj);

            if (value == null) throw new Exception($"Chave prim?ria {keyAttribute.ElementName} n?o pode ser nula");

            string? convertedValue = obj.GetFormattedString(prop);

            element.Add(new XElement(keyAttribute.ElementName, convertedValue));
        }

        return element;
    }

    private static XElement GetEntityForFields<T>(this T obj, bool isUpdate = false) where T : class
    {
        var localFields = new XElement("localFields");

        foreach (PropertyInfo? prop in obj.GetType().GetProperties())
        {
            var keyAttribute = prop.GetCustomAttribute<PrimaryKeyElementAttribute>();
            if (keyAttribute != null)
            {
                if (isUpdate || keyAttribute.AutoEnumerable) continue;
            }

            string? xmlElementName = prop.GetXmlElementName();

            if (xmlElementName == null) continue;

            // Obt?m o valor formatado como string
            string? convertedValue = obj.GetFormattedString(prop);

            bool isNullableState = prop.PropertyType.IsGenericType &&
                                   prop.PropertyType.GetGenericTypeDefinition() == typeof(NullableState<>);

            bool isClear = false;

            if (isNullableState)
            {
                object? value = prop.GetValue(obj);
                if (value != null)
                {
                    PropertyInfo? stateProperty = value.GetType().GetProperty("State");
                    if (stateProperty != null)
                    {
                        // Obt?m o valor da propriedade "State"
                        object? stateValue = stateProperty.GetValue(value);

                        isClear = stateValue != null && (EPropertyState)stateValue == EPropertyState.Clear;
                    }
                }
            }
            
            // if (convertedValue != null && !isClear) continue;
            
            if (convertedValue != null || isNullableState) 
                localFields.Add(isClear && isUpdate
                    ? new XElement(xmlElementName)
                    : new XElement(xmlElementName, convertedValue));
        }

        return localFields;
    }

    #endregion
}