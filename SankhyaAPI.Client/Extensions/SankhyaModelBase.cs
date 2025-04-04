using System.Reflection;
using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.MetaData;

namespace SankhyaAPI.Client.Extensions;

public class SankhyaModelBase : IModelBase
{
    protected SankhyaModelBase()
    {
        ValidateNullableStateProperties(this);
    }

    private static void ValidateNullableStateProperties<T>(T obj)
        where T : class
    {
        Type type = obj.GetType();
        PropertyInfo[] properties = type.GetProperties();

        foreach (PropertyInfo property in properties)
        {
            // Verifica se a propriedade é NullableState<T>
            if (!IsNullableState(property.PropertyType))
            {
                throw new ArgumentException($"A propriedade '{property.Name}' não é do tipo NullableState<>.");
            }

            object? value = property.GetValue(obj);
            if (value == null) continue;

            PropertyInfo? stateProperty = property.PropertyType.GetProperty(nameof(NullableState<int>.State));
            if (stateProperty == null) continue;

            var state = (EPropertyState)stateProperty.GetValue(value)!;

            var primaryKeyAttr = property.GetCustomAttribute<KeyAttribute>();

            if (primaryKeyAttr == null) continue;

            if (state == EPropertyState.Clear)
            {
                throw new ArgumentException($"A chave primária '{property.Name}' não pode estar com estado CLEAR.");
            }

            switch (primaryKeyAttr.AutoEnumerable)
            {
                case true when state != EPropertyState.UnSet:
                    throw new ArgumentException
                        ($"A chave primária '{property.Name}' deve estar UNSET porque é AutoEnumerable.");
                case false when state != EPropertyState.Set:
                    throw new ArgumentException
                        ($"A chave primária '{property.Name}' deve estar SET porque não é AutoEnumerable.");
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