namespace WebHost.Exceptions;

public sealed class UnauthorizedServiceException(string message = "Unauthorized") : ServiceException(message)
{
    public override ResponseStatus StatusCode => ResponseStatus.Unauthorized;
}

public sealed class NotFoundServiceException(string message = "Not found") : ServiceException(message)
{
    public override ResponseStatus StatusCode => ResponseStatus.NotFound;
}

public sealed class ArgumentNullServiceException(string message = "Null argument") : ServiceException(message)
{
    public override ResponseStatus StatusCode => ResponseStatus.InternalServerError;
}

public sealed class InvalidPlatformServiceException(string message = "Invalid platform. Valid platforms: Windows, Android") : ServiceException(message)
{
    public override ResponseStatus StatusCode => ResponseStatus.InternalServerError;
}

public sealed class FileNotFoundServiceException(string message = "File not found") : ServiceException(message)
{
    public override ResponseStatus StatusCode => ResponseStatus.NotFound;
}

public sealed class InvalidOperationServiceException(string message = "Invalid Operation Error") : ServiceException(message)
{
    public override ResponseStatus StatusCode => ResponseStatus.InternalServerError;
}

public sealed class ArgumentOutOfRangeServiceException(string message = "Argument out of range Error") : ServiceException(message)
{
    public override ResponseStatus StatusCode => ResponseStatus.InternalServerError;
}

public sealed class ServiceUnavailableServiceException(string message = "Service unavailable") : ServiceException(message)
{
    public override ResponseStatus StatusCode => ResponseStatus.ServiceUnavailable;
}

public sealed class ConnectionClosedServiceException(string message = "Connection was closed.") : ServiceException(message)
{
    public override ResponseStatus StatusCode => ResponseStatus.ServiceUnavailable;
}