namespace SankhyaAPI.Client.Interfaces;

public interface IProxysable<out TResponse, in TProxy>
    where TResponse : class, new()
    where TProxy : class
{
    TResponse FromProxy(TProxy proxy);
}