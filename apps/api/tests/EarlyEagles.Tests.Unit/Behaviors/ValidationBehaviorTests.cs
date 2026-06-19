using EarlyEagles.Application.Behaviors;
using FluentValidation;
using Mediator;

namespace EarlyEagles.Tests.Unit.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task No_Validators_Registered_Calls_Handler()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([]);
        var handlerCalled = false;

        var response = await behavior.Handle(
            new SampleRequest("ok"),
            (_, _) =>
            {
                handlerCalled = true;
                return ValueTask.FromResult("ok");
            },
            CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.Equal("ok", response);
    }

    [Fact]
    public async Task All_Validators_Pass_Calls_Handler()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([new SampleRequestNameValidator()]);
        var handlerCalled = false;

        var response = await behavior.Handle(
            new SampleRequest("valid-name"),
            (_, _) =>
            {
                handlerCalled = true;
                return ValueTask.FromResult("ok");
            },
            CancellationToken.None);

        Assert.True(handlerCalled);
        Assert.Equal("ok", response);
    }

    [Fact]
    public async Task Single_Validator_Failure_Throws_And_Skips_Handler()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([new SampleRequestNameValidator()]);
        var handlerCalled = false;

        var ex = await Assert.ThrowsAsync<ValidationException>(async () =>
            await behavior.Handle(
                new SampleRequest(string.Empty),
                (_, _) =>
                {
                    handlerCalled = true;
                    return ValueTask.FromResult("ok");
                },
                CancellationToken.None));

        Assert.False(handlerCalled);
        Assert.Single(ex.Errors);
        Assert.Equal(nameof(SampleRequest.Name), ex.Errors.First().PropertyName);
    }

    [Fact]
    public async Task Multiple_Validators_Failing_Combine_Errors_From_All()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>(
            [new SampleRequestNameValidator(), new SampleRequestEmailValidator()]);

        // Name empty fails NotEmpty rule; Email malformed fails EmailAddress rule.
        var ex = await Assert.ThrowsAsync<ValidationException>(async () =>
            await behavior.Handle(
                new SampleRequest(string.Empty) { Email = "not-an-email" },
                (_, _) => ValueTask.FromResult("ok"),
                CancellationToken.None));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(SampleRequest.Name));
        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(SampleRequest.Email));
    }

    [Fact]
    public async Task Null_Next_Throws_Argument_Null_Exception()
    {
        var behavior = new ValidationBehavior<SampleRequest, string>([]);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await behavior.Handle(new SampleRequest("ok"), null!, CancellationToken.None));
    }

    private sealed record SampleRequest(string Name) : IRequest<string>
    {
        public string Email { get; init; } = "valid@example.com";
    }

    private sealed class SampleRequestNameValidator : AbstractValidator<SampleRequest>
    {
        public SampleRequestNameValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }

    private sealed class SampleRequestEmailValidator : AbstractValidator<SampleRequest>
    {
        public SampleRequestEmailValidator()
        {
            RuleFor(x => x.Email).EmailAddress();
        }
    }
}
