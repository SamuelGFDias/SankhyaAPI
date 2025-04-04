using System.Globalization;
using System.Reflection;
using System.Xml.Serialization;
using SankhyaAPI.Client.Envelopes;
using SankhyaAPI.Client.Extensions;
using SankhyaAPI.Client.MetaData;

namespace SankhyaAPI.Client.Utils;

public static class ObjectUtilsMethods
{
    #region PublicMethods

    public static List<Field> GetFieldsFromObject(object obj)
    {
        PropertyInfo[] props = PropertiesFromObject(obj);
        
        return props.Select(t => new Field { Nome = t.GetXmlElementName() }).ToList();
    }

    public static string GetFormattedString(object value) =>
        value switch
        {
            string s => s.Trim(),
            float f => f.ToString("G", CultureInfo.InvariantCulture),
            decimal d => d.ToString("G", CultureInfo.InvariantCulture),
            double db => db.ToString("G", CultureInfo.InvariantCulture),
            Enum e => e.GetXmlEnumValue(),
            DateTime dt => dt.ToString("dd/MM/yyyy HH:mm:ss"),
            DateOnly dt => dt.ToString("dd/MM/yyyy"),
            TimeOnly time => time.ToString("HHmm"),
            bool b => b ? "S" : "N",
            _ => value.ToString()!
        };

    public static string? GetFormattedString<T>(this T obj, PropertyInfo property)
    {
        object? value = property.GetValue(obj);

        if (value == null)
            return null;

        Type propertyType = GetPropertyValueType(property.PropertyType);

        return propertyType switch
        {
            // Caso seja string
            _ when propertyType == typeof(string) => (value as string)?.Trim(),

            // Caso seja float
            _ when propertyType == typeof(float) => ((float)value).ToString("G", CultureInfo.InvariantCulture),

            // Caso seja decimal
            _ when propertyType == typeof(decimal) => ((decimal)value).ToString("G", CultureInfo.InvariantCulture),

            // Caso seja double
            _ when propertyType == typeof(double) => ((double)value).ToString("G", CultureInfo.InvariantCulture),

            // Caso seja enum
            _ when propertyType.IsEnum => ((Enum)value).GetXmlEnumValue(),

            // Caso seja DateTime
            _ when propertyType == typeof(DateTime) => ((DateTime)value).ToString("dd/MM/yyyy HH:mm:ss"),

            // Caso seja DateOnly
            _ when propertyType == typeof(DateOnly) => ((DateOnly)value).ToString("dd/MM/yyyy"),

            // Caso seja TimeOnly
            _ when propertyType == typeof(TimeOnly) => ((TimeOnly)value).ToString("HHmm"),

            // Caso seja bool
            _ when propertyType == typeof(bool) => (bool)value ? "S" : "N",

            // Caso nenhum dos tipos acima seja encontrado
            _ => value.ToString()
        };
    }


    public static object? ConvertForPropertyType(string xmlElementValue, PropertyInfo property)
    {
        try
        {
            Type? propertyType = GetPropertyValueType(property.PropertyType);

            if (string.IsNullOrWhiteSpace(xmlElementValue) && propertyType != typeof(bool)) return null;

            object convertedValue = propertyType switch
            {
                // Conversão para TimeOnly
                not null when propertyType == typeof(TimeOnly) =>
                    TimeOnly.TryParse(xmlElementValue, out TimeOnly time)
                        ? time
                        : TimeOnly.TryParseExact(xmlElementValue.PadLeft(4, '0'), "HHmm", out time)
                            ? time
                            : throw new Exception($"Valor '{xmlElementValue}' não é uma hora válida"),

                // Conversão para DateOnly
                not null when propertyType == typeof(DateOnly) =>
                    DateOnly.TryParse(xmlElementValue, out DateOnly date)
                        ? date
                        : DateOnly.TryParseExact(xmlElementValue, "dd/MM/yyyy", out date)
                            ? date
                            : DateOnly.TryParseExact(xmlElementValue, "ddMMyyyy", out date)
                                ? date
                                : throw new Exception($"Valor '{xmlElementValue}' não é uma data válida"),

                // Conversão para DateTime
                not null when propertyType == typeof(DateTime) =>
                    DateTime.TryParse(xmlElementValue, out DateTime dateTime)
                        ? dateTime
                        : DateTime.TryParseExact(xmlElementValue, "ddMMyyyy HH:mm:ss",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out dateTime)
                            ? dateTime
                            : DateTime.TryParseExact(xmlElementValue, "ddMMyyyy",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out dateTime)
                                ? dateTime
                                : throw new Exception($"Valor '{xmlElementValue}' não é uma data válida"),

                // Conversão para Enums
                { IsEnum: true } => GetEnumValueFromXml(propertyType, xmlElementValue),

                // Conversão para Boolean
                not null when propertyType == typeof(bool) => xmlElementValue switch
                {
                    "S" => true,
                    "N" => false,
                    "" => false,
                    _ => throw new Exception($"Valor '{xmlElementValue}' não é um boolean válido")
                },

                // Conversão para String
                not null when propertyType == typeof(string) =>
                    !string.IsNullOrWhiteSpace(xmlElementValue)
                        ? xmlElementValue.Trim()
                        : xmlElementValue,

                // Conversão padrão para outros tipos
                _ => Convert.ChangeType(xmlElementValue, propertyType!)
            };

            if (property.PropertyType is not { IsGenericType: true } genericType ||
                genericType.GetGenericTypeDefinition() != typeof(NullableState<>)) return convertedValue;

            // Obtém o tipo genérico subjacente (T)
            Type innerType = property.PropertyType.GetGenericArguments()[0];

            // Cria a instância de NullableState<T> dinamicamente
            Type nullableStateType = typeof(NullableState<>).MakeGenericType(innerType);
            return Activator.CreateInstance(nullableStateType, convertedValue);
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Erro ao converter o valor '{xmlElementValue}' para a propriedade '{property.Name}': {ex.Message}");
        }
    }

    public static List<T> GetListOfObjectsFromDictionary<T>(Dictionary<string, List<dynamic>?> fields)
        where T : class, new()
    {
        if (fields.Values.Count == 0) return [];

        int maxCount = fields.Values.Max(list => list?.Count ?? 0);

        var objs = new List<T>();

        for (int i = 0; i < maxCount; i++)
        {
            var objectDest = new T();
            PropertyInfo[] props = PropertiesFromObject(objectDest);

            foreach (PropertyInfo prop in props)
            {
                KeyValuePair<string, List<dynamic>?> field =
                    fields.FirstOrDefault(a => a.Key == prop.GetXmlElementName());

                if (field.Value == null || field.Value.Count <= i) continue;

                dynamic? value = field.Value[i];

                // Não tirar? do convertedValue
                // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
                dynamic? convertedValue = ConvertForPropertyType(value?.ToString() ?? "", prop);

                prop.SetValue(objectDest, convertedValue);
            }


            objs.Add(objectDest);
        }

        return objs;
    }

    public static string GetXmlEnumValue(this Enum enumValue)
    {
        Type type = enumValue.GetType();
        string value = enumValue.ToString();
        FieldInfo? field = type.GetField(value);

        if (field == null) throw new Exception($"Nenhum enumerador encontrado para a valor passado: {value}");

        if (Attribute.GetCustomAttribute(field, typeof(XmlEnumAttribute)) is not XmlEnumAttribute
            {
                Name: not null
            } attribute)
        {
            throw new ArgumentException("Enumerador não pode conter propriedades sem o atributo XmlEnum implementado",
                field.Name);
        }

        return attribute.Name;
    }

    public static string? GetXmlElementName(this PropertyInfo prop)
    {
        var xmlElementAttr = prop.GetCustomAttribute<XmlElementAttribute>();
        KeyAttribute? primaryKeyAttr = prop.GetPrimaryKeyAttribute();
        return xmlElementAttr?.ElementName ?? primaryKeyAttr?.ElementName;
    }

    public static void ValidarCamposChave<T>(List<T> objs, bool isUpdate = false)
        where T : class, new()
    {
        List<PropertyInfo> autoEnumerableKeys = GetKeysPropertiesFromObject<T>(true);
        List<PropertyInfo> keys = GetKeysPropertiesFromObject<T>();


        if (!isUpdate
                ? autoEnumerableKeys is not { Count: > 0 }
                : keys is not { Count: > 0 }
           ) return;

        foreach (T item in objs)
        {
            List<PropertyInfo> propertyKeys = item.GetType().GetProperties()
                .Where(!isUpdate
                    ? autoEnumerableKeys.Contains
                    : keys.Contains)
                .ToList();

            foreach (PropertyInfo property in propertyKeys)
            {
                object? value = property.GetValue(item);

                switch (isUpdate)
                {
                    case true when value == null:
                        throw new ArgumentNullException(
                            property.Name,
                            "Os campos que fazem parte da chave primária não podem estar vazios em operações de atualização.");
                    case false when value != null:
                        throw new ArgumentException(
                            "Os campos que fazem parte da chave primária com atributos auto enumerados não podem estar preenchidos em operações de inserção.",
                            property.Name);
                }
            }
        }
    }

    public static T GetEnumValueFromXml<T>(string xmlValue)
        where T : Enum
    {
        Type enumType = typeof(T);
        FieldInfo[] fields = enumType.GetFields();
        FieldInfo? matchField = fields.FirstOrDefault(f =>
            f.GetCustomAttribute<XmlEnumAttribute>()?.Name == xmlValue);

        return matchField != null
            ? (T)Enum.Parse(enumType, matchField.Name)
            : throw new ArgumentException($"Valor '{xmlValue}' não é válido para o enum '{enumType.Name}'");
    }

    public static object GetEnumValueFromXml(Type enumType, string xmlValue)
    {
        FieldInfo[] fields = enumType.GetFields();
        FieldInfo? matchField = fields.FirstOrDefault(f =>
            f.GetCustomAttribute<XmlEnumAttribute>()?.Name == xmlValue);

        return matchField != null
            ? Enum.Parse(enumType, matchField.Name)
            : throw new ArgumentException($"Valor '{xmlValue}' não é válido para o enum '{enumType.Name}'");
    }

    //public static PropertyState GetPropertyState<T>(PropertyInfo propertyInfo)
    //{
    //    return nullableState.State;
    //}

    public static Type GetPropertyValueType(this Type propertyType)
    {
        Type? type = NullableState.GetUnderlyingType(propertyType);

        type ??= Nullable.GetUnderlyingType(propertyType);

        type ??= propertyType;

        return type;
    }
    #endregion


    #region PrivateMethods


    private static PropertyInfo[] PropertiesFromObject(object obj)
    {
        return obj.GetType().GetProperties();
    }


    private static List<PropertyInfo> GetKeysPropertiesFromObject<T>(bool isAutoEnumerable = false)
        where T : class, new()
    {
        var obj = new T();
        PropertyInfo[] properties = obj.GetType().GetProperties();

        if (isAutoEnumerable)
            return properties.Where(p =>
                isAutoEnumerable
                    ? p.GetPrimaryKeyAttribute() is { AutoEnumerable: true }
                    : p.GetPrimaryKeyAttribute() != null).ToList();

        return properties.Where(p => p.GetPrimaryKeyAttribute() != null).ToList();
    }

    private static KeyAttribute? GetPrimaryKeyAttribute(this PropertyInfo prop) =>
        prop.GetCustomAttribute<KeyAttribute>();

    #endregion
}