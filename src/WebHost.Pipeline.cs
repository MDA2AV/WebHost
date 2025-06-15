using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using WebHost.Protocol.Response;

namespace WebHost;

public partial class WebHostApp<TContext>
{
    /// <summary>
    /// Recursively executes the registered middleware by order of registration and invokes the final endpoint.
    /// </summary>
    /// <param name="context">The current HTTP request context, representing the state of the incoming request.</param>
    /// <param name="index">The current index in the middleware pipeline to start executing from.</param>
    /// <param name="middleware">A list of middleware components to be executed in order.</param>
    /// <returns>A task representing the asynchronous operation that processes the pipeline and returns a response.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no endpoint is found for the decoded route.</exception>
    public Task<IResponse> Pipeline(
        TContext context,
        int index,
        IList<Func<TContext, Func<TContext, Task<IResponse>>, Task<IResponse>>> middleware)
    {
        if (index < middleware.Count)
        {
            return middleware[index](context, async (ctx) => await Pipeline(ctx, index + 1, middleware));
        }

        var decodedRoute = MatchEndpoint(EncodedRoutes[context.Request.HttpMethod.ToUpper()], context.Request.Route);

        var endpoint = context.Scope.ServiceProvider
            .GetRequiredKeyedService<Func<TContext, Task<IResponse>>>(
                $"{context.Request.HttpMethod}_{decodedRoute}");

        return endpoint.Invoke(context);
    }

    /// <summary>
    /// Executes the registered middleware pipeline and returns a response by invoking the final endpoint.
    /// </summary>
    /// <param name="context">The current HTTP request context, representing the state of the incoming request.</param>
    /// <returns>A task representing the asynchronous operation that processes the pipeline and returns a response.</returns>
    public async Task<IResponse> Pipeline(TContext context)
    {
        var middleware = InternalHost.Services
            .GetServices<Func<TContext, Func<TContext, Task<IResponse>>, Task<IResponse>>>()
            .ToList();

        await using var scope = InternalHost.Services.CreateAsyncScope();
        context.Scope = scope;

        return await Pipeline(context, 0, middleware);
    }

    /// <summary>
    /// Executes the registered middleware pipeline without expecting a response. Invokes the endpoint without returning a response.
    /// </summary>
    /// <param name="context">The current HTTP request context, representing the state of the incoming request.</param>
    /// <param name="index">The current index in the middleware pipeline to start executing from.</param>
    /// <param name="middleware">A list of middleware components to be executed in order.</param>
    /// <returns>A task representing the asynchronous operation that processes the middleware pipeline without returning a response.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no endpoint is found for the decoded route.</exception>
    public Task PipelineNoResponse(
        TContext context, 
        int index, 
        IList<Func<TContext, Func<TContext, Task>, Task>> middleware)
    {
        if (index < middleware.Count)
        {
            return middleware[index](context, async (ctx) => await PipelineNoResponse(ctx, index + 1, middleware));
        }

        var decodedRoute = MatchEndpoint(EncodedRoutes[context.Request.HttpMethod.ToUpper()], context.Request.Route);

        var endpoint = context.Scope.ServiceProvider
            .GetRequiredKeyedService<Func<TContext, Task>>($"{context.Request.HttpMethod}_{decodedRoute}");

        return endpoint is null
            ? throw new InvalidOperationException("Unable to find the Invoke method on the resolved service.")
            : endpoint.Invoke(context);
    }

    /// <summary>
    /// Executes the registered middleware pipeline without expecting a response. Invokes the endpoint and completes the operation asynchronously.
    /// </summary>
    /// <param name="context">The current HTTP request context, representing the state of the incoming request.</param>
    /// <returns>A task representing the asynchronous operation that processes the middleware pipeline without returning a response.</returns>
    public async Task PipelineNoResponse(TContext context)
    {
        var middleware = InternalHost.Services.GetServices<Func<TContext, Func<TContext, Task>, Task>>().ToList();

        await using var scope = InternalHost.Services.CreateAsyncScope();
        context.Scope = scope;

        await PipelineNoResponse(context, 0, middleware);
    }

    /// <summary>
    /// Matches a given input string against a set of encoded route patterns.
    /// </summary>
    /// <param name="hashSet">A <see cref="HashSet{T}"/> containing route patterns to match against.</param>
    /// <param name="input">The input string (route) to match against the patterns.</param>
    /// <returns>The first matching route pattern from the <paramref name="hashSet"/>, or <c>null</c> if no match is found.</returns>
    public static string? MatchEndpoint(HashSet<string> hashSet, string input)
    {
        return (from entry in hashSet
                let pattern = ConvertToRegex(entry) // Convert route pattern to regex
                where Regex.IsMatch(input, pattern) // Check if input matches the regex
                select entry) // Select the matching pattern
            .FirstOrDefault(); // Return the first match or null if no match is found
    }

    /// <summary>
    /// Converts a route pattern with placeholders (e.g., ":id") into a regular expression.
    /// </summary>
    /// <param name="pattern">The route pattern to convert into a regex format.</param>
    /// <returns>A regex string that matches the given pattern.</returns>
    public static string ConvertToRegex(string pattern)
    {
        // Replace placeholders like ":id" with a regex pattern that matches any non-slash characters
        var regexPattern = Regex.Replace(pattern, @":\w+", "[^/]+");

        // Add anchors to ensure the regex matches the entire input string
        regexPattern = $"^{regexPattern}$";

        return regexPattern;
    }
}