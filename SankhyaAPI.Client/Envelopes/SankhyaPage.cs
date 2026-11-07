namespace SankhyaAPI.Client.Envelopes;

/// <summary>
/// Uma página de registros do loadRecords.
/// </summary>
/// <param name="Items">Registros da página (o Sankhya devolve até 50).</param>
/// <param name="Page">Número da página, começando em 0.</param>
/// <param name="HasMoreResult">Há mais páginas depois desta.</param>
public sealed record SankhyaPage<T>(List<T> Items, int Page, bool HasMoreResult);
