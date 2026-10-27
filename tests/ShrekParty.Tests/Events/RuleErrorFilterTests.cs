using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using ShrekParty.Api.Events;

namespace ShrekParty.Tests.Events;

public class RuleErrorFilterTests
{
    [Fact]
    public async Task Bad_input_becomes_400_with_code()
    {
        var result = await InvokeAsync(() => throw new BadInputException("bad_count"));

        result.Should().BeOfType<BadRequest<ErrorBody>>().Which.Value!.Error.Should().Be("bad_count");
    }

    [Fact]
    public async Task Rule_violation_becomes_409_with_code()
    {
        var result = await InvokeAsync(() => throw new EventRuleException("too_late"));

        result.Should().BeOfType<Conflict<ErrorBody>>().Which.Value!.Error.Should().Be("too_late");
    }

    [Fact]
    public async Task Bug_is_not_hidden_as_bad_request()
    {
        var act = () => InvokeAsync(() => throw new ArgumentOutOfRangeException("index"));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private static async Task<object?> InvokeAsync(Func<object?> endpoint)
    {
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext());
        return await new RuleErrorFilter().InvokeAsync(context, _ => ValueTask.FromResult(endpoint()));
    }
}
