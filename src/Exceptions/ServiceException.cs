namespace WebHost.Exceptions;

public interface IServiceException
{
    ResponseStatus StatusCode { get; }
}

public class ServiceException(string message) : Exception(message), IServiceException
{
    public virtual ResponseStatus StatusCode => ResponseStatus.InternalServerError;
}