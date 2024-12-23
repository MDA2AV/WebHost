﻿using System.Text.RegularExpressions;
using WebHost.Exceptions;

namespace WebHost.Utils;

/// <summary>
/// Provides utilities for parsing HTTP requests, including extracting routes and splitting headers from the body.
/// </summary>
public static class RequestParser
{
    /// <summary>
    /// Attempts to extract the HTTP method and route path from a collection of headers.
    /// </summary>
    /// <param name="headers">The collection of headers to parse.</param>
    /// <param name="route">
    /// When the method returns <c>true</c>, contains a tuple of the HTTP method (e.g., GET, POST) and the route path (e.g., /api/resource).
    /// </param>
    /// <returns>
    /// <c>true</c> if the HTTP method and route path were successfully extracted; otherwise, <c>false</c>.
    /// </returns>
    public static bool TryExtractRoute(IEnumerable<string> headers, out (string, string) route)
    {
        const string pattern = @"^(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)\s+(/[\w\/\-]*)\s+HTTP\/\d\.\d";

        route = default;

        var match = headers
            .Select(header => Regex.Match(header, pattern))
            .FirstOrDefault(m => m.Success);

        if (match is { Success: true })
        {
            var method = match.Groups[1].Value;
            var path = match.Groups[2].Value;

            route = (method, path);
            return true;

        }

        Console.WriteLine($"Endpoint was not found.");
        return false;
    }

    /// <summary>
    /// Splits the raw HTTP request into headers and body.
    /// </summary>
    /// <param name="rawRequest">The raw HTTP request string.</param>
    /// <returns>
    /// A tuple containing the headers as an array of strings and the body as a string.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the raw request is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request is malformed, such as missing the header-body separator or having an invalid Content-Length header.
    /// </exception>
    public static (string[] Headers, string Body) SplitHeadersAndBody(string? rawRequest)
    {
        if (rawRequest is null)
        {
            throw new ArgumentNullServiceException("RawRequest is null");
        }

        // Split into headers and body candidate based on the first \r\n\r\n
        var separatorIndex = rawRequest.IndexOf("\r\n\r\n", StringComparison.Ordinal);

        if (separatorIndex == -1)
        {
            throw new InvalidOperationException("Malformed HTTP request: No header-body separator found.");
        }

        // Extract headers part
        var headersPart = rawRequest.Substring(0, separatorIndex);
        var bodyCandidate = rawRequest.Substring(separatorIndex + 4); // Skip \r\n\r\n

        // Split headers into lines
        var headers = headersPart.Split("\r\n");

        // Find Content-Length header
        var contentLength = 0;
        foreach (var header in headers)
        {
            if (!header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lengthValue = header["Content-Length:".Length..].Trim();

            if (!int.TryParse(lengthValue, out contentLength))
            {
                throw new InvalidOperationException("Invalid Content-Length header.");
            }

            break;
        }

        // Extract the body based on Content-Length
        var body = contentLength > 0 && bodyCandidate.Length >= contentLength
            ? bodyCandidate[..contentLength]
            : string.Empty;

        return (headers, body);
    }
}

