using WebHost.Attributes;
using WebHost;

namespace Shared;

[Key("ExampleKey")]
public class TestHandler(ExampleService exampleService) : IRequestHandler<ExampleQuery, bool>
{
    public async Task<bool> Handle(ExampleQuery query, CancellationToken cancellationToken)
    {
        await exampleService.ExecuteAsync();

        return true;
    }
}
public record ExampleQuery : IRequest<bool>;