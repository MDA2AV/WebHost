using System.Buffers;
using System.IO.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebHost.Exceptions;
using WebHost.Models;
using WebHost.Utils.HttpRequest;

namespace WebHost;

public sealed partial class WebHostApp
{
    public struct Http0Frame
    {
        public byte FrameType { get; init; }
        public int Id { get; init; }
        public short Route { get; init; }
        public short Payload { get; init; }
    }
    private async Task ReadHttp0Frame(PipeReader pipeReader)
    {

    }

    static async Task<byte[]> TryReadNextBytesAsync(PipeReader reader, int byteCount)
    {
        while (true)
        {
            var result = reader.TryRead(out var readResult);

            if (!result)
            {
                // If no data is currently available, read asynchronously
                readResult = await reader.ReadAsync();
            }

            var buffer = readResult.Buffer;

            // Check if there are enough bytes in the current buffer
            if (buffer.Length >= byteCount)
            {
                // Slice the buffer to get only the required number of bytes
                var slice = buffer.Slice(0, byteCount);
                var bytes = new byte[byteCount];
                slice.CopyTo(bytes);

                // Advance the reader to the end of the consumed bytes
                reader.AdvanceTo(slice.End);
                return bytes;
            }

            // If not enough bytes, advance the reader to the start of the buffer
            reader.AdvanceTo(buffer.Start, buffer.End);

            // If the reader is completed and we still don't have enough bytes, throw an exception
            if (readResult.IsCompleted)
            {
                throw new InvalidOperationException("Not enough data available to read the required number of bytes.");
            }
        }
    }

    private async Task HandleClientAsync0(Stream stream, PipeReader pipeReader, CancellationToken stoppingToken)
    {
        var context = new Context(stream);

        // Read the initial client request
        //
        var headers = await ExtractHeaders(pipeReader, stoppingToken);

        // Loop to handle multiple requests for "keep-alive" connections
        //
        while (headers != null)
        {
            var connection = GetConnectionType(headers);

            if (connection == ConnectionType.Websocket)
                await SendHandshakeResponse(context, headers);

            var headerEntries = headers.Split("\r\n");

            // Split the request into headers and body
            //
            var body = await ExtractBody(pipeReader, headers, stoppingToken);

            // Try to extract the uri from the headers
            //
            var result = RequestParser.TryExtractUri2(headerEntries[0], out (string, string) uriHeader);
            if (!result)
            {
                _logger?.LogTrace("Invalid request received, unable to parse route");
                throw new InvalidOperationServiceException("Invalid request received, unable to parse route");
            }

            var uriParams = uriHeader.Item2.Split('?');

            // Populate the context with the parsed request information
            //
            context.Request = new Http11Request(Headers: headerEntries,
                                          Body: body,
                                          Route: uriParams[0],
                                          QueryParameters: uriParams.Length > 1 ? uriParams[1] : string.Empty,
                                          HttpMethod: uriHeader.Item1);

            // Create a new scope for handling the request
            //
            await using (var scope = InternalHost.Services.CreateAsyncScope())
            {
                context.Scope = scope;

                // Retrieve and execute the middleware pipeline
                //
                var middleware = scope.ServiceProvider.GetServices<Func<IContext, Func<IContext, Task>, Task>>().ToList();

                await Pipeline(context, 0, middleware);
            }

            // Handle "keep-alive" connections
            //
            if (connection == ConnectionType.KeepAlive)
            {
                headers = await ExtractHeaders(pipeReader, stoppingToken);
            }
            else
            {
                break;
            }
        }
    }
}
