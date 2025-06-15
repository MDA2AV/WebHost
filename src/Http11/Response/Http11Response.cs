using WebHost.Http11.Response.Contents;
using WebHost.Protocol;
using WebHost.Protocol.Response;
using WebHost.Protocol.Response.Headers;

namespace WebHost.Http11.Response;

public class Http11Response : IResponse
{
    private readonly ResponseHeaderCollection _headers = new();

    public FlexibleResponseStatus Status { get; set; } = new FlexibleResponseStatus(ResponseStatus.Ok);

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

    public FlexibleContentType? ContentType { get; set; }

    public IContent? Content { get; set; }

    public string? ContentEncoding { get; set; }

    public ulong? ContentLength { get; set; }

    #region IDisposable Support

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) 
            return;

        Headers.Dispose();

        _disposed = true;
    }

    #endregion
}