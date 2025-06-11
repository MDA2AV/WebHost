using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WebHost.Http11.Context;


public class Http11Context : IContext
{
    public Stream Stream { get; set; } = null!;
    public IHttpRequest Request { get; set; } = null!;
    public AsyncServiceScope Scope { get; set; }
    public T Resolve<T>() where T : notnull => Scope.ServiceProvider.GetRequiredService<T>();
    public IResponse Response { get; set; } = null!;

    public CancellationToken CancellationToken { get; set; }
}

public interface IBuilder<out T>
{
    T Build();
}
public interface IResponseModification<out TBuilder>
{

    /// <summary>
    /// Specifies the HTTP status code of the response.
    /// </summary>
    /// <param name="status">The HTTP status code of the response</param>
    TBuilder Status(ResponseStatus status);

    /// <summary>
    /// Specifies the HTTP status code of the response.
    /// </summary>
    /// <param name="status">The status code of the response</param>
    /// <param name="reason">The reason phrase of the response (such as "Not Found" for 404)</param>
    TBuilder Status(int status, string reason);

    /// <summary>
    /// Sets the given header field on the response. Changing HTTP
    /// protocol headers may cause incorrect behavior.
    /// </summary>
    /// <param name="key">The name of the header to be set</param>
    /// <param name="value">The value of the header field</param>
    TBuilder Header(string key, string value);

    /// <summary>
    /// Sets the expiration date of the response.
    /// </summary>
    /// <param name="expiryDate">The expiration date of the response</param>
    TBuilder Expires(DateTime expiryDate);

    /// <summary>
    /// Sets the point in time when the requested resource has been
    /// modified last.
    /// </summary>
    /// <param name="modificationDate">The point in time when the requested resource has been modified last</param>
    TBuilder Modified(DateTime modificationDate);

    /// <summary>
    /// Adds the given cookie to the response.
    /// </summary>
    /// <param name="cookie">The cookie to be added</param>
    TBuilder Cookie(Cookie cookie);

    /// <summary>
    /// Sets the encoding of the content.
    /// </summary>
    /// <param name="encoding">The encoding of the content</param>
    TBuilder Encoding(string encoding);
}
public interface IResponseBuilder : IBuilder<IResponse>, IResponseModification<IResponseBuilder>
{
    /// <summary>
    /// Specifies the length of the content stream, if known.
    /// </summary>
    /// <param name="length">The length of the content stream</param>
    IResponseBuilder Length(ulong length);
}
public class ResponseBuilder : IResponseBuilder
{
    public IResponse Build()
    {
        throw new NotImplementedException();
    }

    public IResponseBuilder Status(ResponseStatus status)
    {
        throw new NotImplementedException();
    }

    public IResponseBuilder Status(int status, string reason)
    {
        throw new NotImplementedException();
    }

    public IResponseBuilder Header(string key, string value)
    {
        throw new NotImplementedException();
    }

    public IResponseBuilder Expires(DateTime expiryDate)
    {
        throw new NotImplementedException();
    }

    public IResponseBuilder Modified(DateTime modificationDate)
    {
        throw new NotImplementedException();
    }

    public IResponseBuilder Cookie(Cookie cookie)
    {
        throw new NotImplementedException();
    }

    public IResponseBuilder Encoding(string encoding)
    {
        throw new NotImplementedException();
    }

    public IResponseBuilder Length(ulong length)
    {
        throw new NotImplementedException();
    }
}


/// <summary>
/// The response to be sent to the connected client for a given request.
/// </summary>
public interface IResponse : IDisposable
{

    #region Protocol

    /// <summary>
    /// The HTTP response code.
    /// </summary>
    ResponseStatus Status { get; set; }

    #endregion

    #region Headers

    /// <summary>
    /// Define, when this resource will expire.
    /// </summary>
    DateTime? Expires { get; set; }

    /// <summary>
    /// Define, when this resource has been changed the last time.
    /// </summary>
    DateTime? Modified { get; set; }

    /// <summary>
    /// Retrieve or set the value of a header field.
    /// </summary>
    /// <param name="field">The name of the header field</param>
    /// <returns>The value of the header field</returns>
    string? this[string field] { get; set; }

    /// <summary>
    /// The headers of the HTTP response.
    /// </summary>
    IEditableHeaderCollection Headers { get; }

    #endregion

    #region Content

    IContent Content { get; set; }

    /// <summary>
    /// The type of the content.
    /// </summary>
    ReadOnlyMemory<byte> ContentType { get; set; }

    /// <summary>
    /// The encoding of the content (e.g. "br").
    /// </summary>
    string? ContentEncoding { get; set; }

    /// <summary>
    /// The number of bytes the content consists of.
    /// </summary>
    ulong? ContentLength { get; set; }

    #endregion

}
public class Http11Response : IResponse
{
    private readonly ResponseHeaderCollection _headers = new();

    public ResponseStatus Status { get; set; } = ResponseStatus.Ok;
    public DateTime? Expires { get; set; }
    public DateTime? Modified { get; set; }

    public string? this[string field]
    {
        get => _headers.GetValueOrDefault(field);
        set
        {
            if (value is not null)
            {
                _headers[field] = value;
            }
            else
            {
                _headers.Remove(field);
            }
        }
    }

    public IEditableHeaderCollection Headers => _headers;

    // TODO: Implement ContentType, should take a delegate to build the content?
    public ReadOnlyMemory<byte> ContentType { get; set; }

    public IContent Content { get; set; }

    public string? ContentEncoding { get; set; }

    public ulong? ContentLength { get; set; }

    #region IDisposable Support

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        Headers.Dispose();

        _disposed = true;
    }

    #endregion
}

public interface IContent
{
    ulong? Length { get; }

    ValueTask<ulong?> CalculateChecksumAsync();

    ValueTask WriteAsync(Stream target, uint bufferSize);
}

public sealed class RawContent(ReadOnlyMemory<byte> data) : IContent
{
    public ulong? Length { get; } = (ulong)data.Length;

    public ValueTask<ulong?> CalculateChecksumAsync() => new((ulong)data.GetHashCode());

    public async ValueTask WriteAsync(Stream target, uint bufferSize)
    {
        await target.WriteAsync(data);
        await target.FlushAsync();
    }
}

public sealed class JsonContent(object data, JsonSerializerOptions options) : IContent
{
    public ulong? Length => null;

    public ValueTask<ulong?> CalculateChecksumAsync() => new((ulong)data.GetHashCode());

    public async ValueTask WriteAsync(Stream target, uint bufferSize)
    {
        await JsonSerializer.SerializeAsync(target, data, data.GetType(), options);
    }
}

public interface IHeaderCollection : IReadOnlyDictionary<string, string>, IDisposable;

public interface IEditableHeaderCollection : IDictionary<string, string>, IDisposable;

public sealed class ResponseHeaderCollection : PooledDictionary<string, string>, IHeaderCollection,
    IEditableHeaderCollection
{

}