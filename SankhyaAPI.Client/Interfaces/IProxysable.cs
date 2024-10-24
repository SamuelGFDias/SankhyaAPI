namespace SankhyaAPI.Client.Interfaces;

[Obsolete("Esta classe é obsoleta. Agora você pode mapear o XML retornado diretamente na classe de requisição")]
public interface IProxysable<out TResponse, in TProxy>
    where TResponse : class, new()
    where TProxy : class
{
    [Obsolete("Este método é obsoleto.")]
    TResponse FromProxy(TProxy proxy);
}