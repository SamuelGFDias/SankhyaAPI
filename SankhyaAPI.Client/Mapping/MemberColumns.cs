namespace SankhyaAPI.Client.Mapping;

/// <summary>
/// Peças comuns aos mapas fluente, por atributos e gerado.
/// </summary>
internal static class MemberColumns
{
    public static INullableStateAccessor AccessorFor(Type nullableStateType) =>
        (INullableStateAccessor)typeof(NullableStateAccessor<>).MakeGenericType(nullableStateType.GetGenericArguments()[0])
                                                                .GetField(nameof(NullableStateAccessor<int>.Instance))!
                                                                .GetValue(null)!;

    public static void AddColumn(MemberMap member, string column, SankhyaType type, IReadOnlyDictionary<object, string>? options)
    {
        member.Writes.Add(new WriteColumn(column, v => SankhyaFormat.Format(v, type, options)));
        member.Read = SingleRead(member, column, type, options?.ToDictionary(kv => kv.Value, kv => kv.Key));
    }

    public static ReadSpec SingleRead(MemberMap member, string column, SankhyaType type, IReadOnlyDictionary<string, object>? options) =>
        new([column], t => SankhyaFormat.Parse(t[0], member.Type, type, options, member.Name));
}
