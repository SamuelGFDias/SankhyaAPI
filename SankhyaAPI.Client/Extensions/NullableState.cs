using SankhyaAPI.Client.MetaData;

namespace SankhyaAPI.Client.Extensions;

public static class NullableState
{
    public static Type? GetUnderlyingType(Type nullableState)
    {
        Type type = nullableState;

        ArgumentNullException.ThrowIfNull(type);

        if (Nullable.GetUnderlyingType(type) is { } nullableUnderlyingType)
        {
            type = nullableUnderlyingType;
        }

        if (type is not { IsGenericType: true, IsGenericTypeDefinition: false }) return null;

        Type genericType = type.GetGenericTypeDefinition();
        if (!ReferenceEquals(genericType, typeof(NullableState<>))) return null;

        Type genericArgumentType = type.GetGenericArguments()[0];
        Type? underlyingType = Nullable.GetUnderlyingType(genericArgumentType);
        return underlyingType ?? genericArgumentType;

    }
}