using SankhyaAPI.Client.Interfaces;
using SankhyaAPI.Client.Utils;

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
        foreach (PropertyMap property in ModelMap.For(obj.GetType()).Properties)
        {
            if (!property.IsNullableState)
                throw new ArgumentException($"A propriedade '{property.Name}' não é do tipo NullableState<>.");

            if (property.Key != null && property.GetState(obj) == EPropertyState.Clear)
                throw new ArgumentException($"A chave primária '{property.Name}' não pode estar com estado CLEAR.");
        }
    }
}
