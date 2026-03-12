using System.Reflection;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;

namespace SankhyaAPI.Client.Extensions;

public class SankhyaModelBase : IModelBase
{
    public static void ValidateNullableStateProperties<T>(List<T> objetos, bool isUpdate = false)
        where T : class
    {
        foreach (T obj in objetos)
            ValidateNullableStateProperties(obj, isUpdate);
    }

    private static void ValidateNullableStateProperties<T>(T obj, bool isUpdate)
        where T : class
    {
        Type type = obj.GetType();
        PropertyInfo[] properties = type.GetProperties();

        foreach (PropertyInfo property in properties)
        {
            if (!IsNullableState(property.PropertyType))
            {
                throw new ArgumentException($"A propriedade '{property.Name}' não é do tipo NullableState<>.");
            }

            object? value = property.GetValue(obj);
            if (value == null) continue;

            EPropertyState state;

            if (value is INullableState nullableStateValue)
                state = nullableStateValue.State;
            else
                continue;


            var primaryKeyAttr = property.GetCustomAttribute<KeyAttribute>();

            if (primaryKeyAttr == null) continue;

            if (state == EPropertyState.Clear)
            {
                throw new ArgumentException($"A chave primária '{property.Name}' não pode estar com estado CLEAR.");
            }
        }
    }

    private static bool IsNullableState(Type type)
    {
        if (!type.IsGenericType) return false;
        Type genericType = type.GetGenericTypeDefinition();
        return genericType == typeof(NullableState<>);
    }
}