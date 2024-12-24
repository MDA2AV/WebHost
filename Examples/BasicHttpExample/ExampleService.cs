using Microsoft.Extensions.Logging;

namespace BasicHttpExample;

// Example service class.
// Will be automatically disposed by the end of the request.
public class ExampleService(ILogger<ExampleService> logger) : IDisposable
{
    // To detect redundant calls
    private bool _disposed;

    private int _counter;
    public async Task ExecuteAsync()
    {
        logger.LogInformation("Current _counter value: {Counter}", _counter);
        await Task.Delay(0);
        _counter++;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            logger.LogInformation("ServiceTest disposed. {HashCode}", this.GetHashCode());
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}