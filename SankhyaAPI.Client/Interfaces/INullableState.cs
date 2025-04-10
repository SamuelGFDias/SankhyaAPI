using SankhyaAPI.Client.Extensions;

namespace SankhyaAPI.Client.Interfaces;

public interface INullableState
{
    EPropertyState State { get; }
    bool HasValue { get; }
}
