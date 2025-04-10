using SankhyaAPI.Client.Extensions;

namespace SankhyaAPI.Client.MetaData;

public struct NullableState<T>
{
    public T? Value { get; set; }

    public bool HasValue => Value != null;
    public EPropertyState State { get; set; } = EPropertyState.UnSet;
    public NullableState() { }

    public NullableState(T value)
    {
        Value = value;
        State = EPropertyState.Set;
    }

    public override string ToString()
    {
        return Value?.ToString() ?? string.Empty;
    }

    public static NullableState<T> Clear()
    {
        return new NullableState<T>
        {
            State = EPropertyState.Clear
        };
    }
   
    public static implicit operator T?(NullableState<T?> nullableState)
    {
        return nullableState.Value;
    }

    public static implicit operator NullableState<T>(T value)
    {
        return new NullableState<T>(value);
    }

}