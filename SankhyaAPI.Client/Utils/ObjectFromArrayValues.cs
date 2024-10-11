using System.Reflection;
using System.Text.Json.Serialization;
using Refit;
using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Responses;

namespace SankhyaAPI.Client.Utils;

public static class ObjectFromArrayValues
{
    public static void FillFielsFromObjectToTypedObject(object destObj, object srcObj)
    {
        var props1 = PropertiesFromObject(destObj);
        var props2 = PropertiesFromObject(srcObj);
        for (var i = 0; i < props1.Length; i++)
        {
            var value2 = props2[i].GetValue(srcObj);
            if (value2 is not null) props1[i].SetValue(destObj, value2);
        }
    }

    public static void FillFielsFromObjectList(object destObj, List<object> srcObjList)
    {
        var props1 = PropertiesFromObject(destObj);
        var minLength = Math.Min(props1.Length, srcObjList.Count);
        for (var i = 0; i < minLength; i++)
        {
            var value2 = srcObjList[i];
            props1[i].SetValue(destObj, value2);
        }
    }

    public static PropertyInfo[] PropertiesFromObject(object obj)
    {
        return obj.GetType().GetProperties();
    }

    public static void GetListOfObjectsFromObjectMatrix<T>(List<T> objs, List<List<object>> srcObjMatrix)
        where T : class?, new()
    {
        T objectDest;
        foreach (var obj in srcObjMatrix)
        {
            objectDest = new T();
            FillFielsFromObjectList(objectDest, obj);
            objs.Add(objectDest);
        }
    }

    public static List<Field> GetFieldsFromObject(object obj)
    {
        var fields = new List<Field>();
        var props = PropertiesFromObject(obj);
        for (var i = 0; i < props.Length; i++)
        {
            var customNameAttribule = props[i].GetCustomAttribute<JsonPropertyNameAttribute>();
            if (customNameAttribule != null)
                fields.Add(
                    new Field
                    {
                        Nome = customNameAttribule.Name
                    }
                );
            else
                fields.Add(
                    new Field
                    {
                        Nome = props[i].Name
                    }
                );
        }

        return fields;
    }

    public static List<Field> GetFieldsFromObjectJsonAttribOnly(object obj)
    {
        var fields = new List<Field>();
        var props = PropertiesFromObject(obj);
        foreach (var prop in props)
        {
            var customNameAttribute = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            fields.Add(new Field { Nome = customNameAttribute?.Name ?? prop.Name });
            //var value = prop.GetValue(obj);
            //if (value != null && !string.IsNullOrEmpty(value.ToString()))
            //{
            //    var customNameAttribute = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            //    fields.Add(new Field { Nome = customNameAttribute?.Name ?? prop.Name });
            //}
        }

        return fields;
    }

    public static List<T> MapperResponse<T>(this ApiResponse<ServiceResponse<T>> response) where T : class, new()
    {
        var lista = new List<T>();
        if (response.Content?.ResponseBody.Rows is { Count: > 0 })
            GetListOfObjectsFromObjectMatrix(lista, response.Content?.ResponseBody.Rows!);
        return lista;
    }
}