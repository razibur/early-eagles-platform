using EarlyEagles.Domain.Notifications;
using EarlyEagles.Domain.Tenants;
using FluentValidation;

namespace EarlyEagles.Application.Notifications;

/// <summary>Turns a subscription the domain would refuse into a 400 before the handler runs.</summary>
public sealed class SubscribeToPushCommandValidator : AbstractValidator<SubscribeToPushCommand>
{
    public SubscribeToPushCommandValidator()
    {
        RuleFor(c => c.ClerkUserId)
            .Must(TenantMember.IsValidClerkUserId);

        RuleFor(c => c.Endpoint)
            .Must(PushSubscription.IsValidEndpoint)
            .WithMessage("endpoint must be the push service URL from PushSubscription.toJSON(): https, on a named host.");

        RuleFor(c => c.P256dh)
            .Must(PushSubscription.IsValidKey)
            .WithMessage("keys.p256dh must be the base64url key from PushSubscription.toJSON().");

        RuleFor(c => c.Auth)
            .Must(PushSubscription.IsValidKey)
            .WithMessage("keys.auth must be the base64url secret from PushSubscription.toJSON().");
    }
}
