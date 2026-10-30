using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Application.Tenants;
using EarlyEagles.Domain.Tenants;
using Mediator;
using Stripe;
using Stripe.Checkout;

namespace EarlyEagles.Api.Webhooks;

/// <summary>
/// Turns a verified Stripe event into a command (P0-043.2, P0-043.4). Called by
/// <see cref="StripeWebhookEndpoints"/> only after the signature check.
/// </summary>
/// <remarks>
/// <para>The tenant comes from the Checkout Session's
/// <c>client_reference_id</c>: the platform sets it to the tenant id when it
/// creates the session (P2-008). Stripe copies it onto the completed session
/// and nowhere else, so later subscription events find the tenant by the
/// customer it records (P0-045.2).</para>
/// <para>An event that can never succeed (no or unknown tenant, a deactivated
/// tenant, a tenant billed to another customer) is logged as an error and
/// acknowledged: retrying for three days would not change it. Database
/// failures throw, the endpoint answers 500, and Stripe retries.</para>
/// </remarks>
public sealed class StripeWebhookDispatcher
{
    private const string SubscriptionMode = "subscription";

    private readonly IBillingTenantDirectory directory;
    private readonly ITenantContext tenantContext;
    private readonly ITenantContextInitializer tenantInitializer;
    private readonly ISender sender;
    private readonly ILogger<StripeWebhookDispatcher> logger;

    public StripeWebhookDispatcher(
        IBillingTenantDirectory directory,
        ITenantContext tenantContext,
        ITenantContextInitializer tenantInitializer,
        ISender sender,
        ILogger<StripeWebhookDispatcher> logger)
    {
        this.directory = directory;
        this.tenantContext = tenantContext;
        this.tenantInitializer = tenantInitializer;
        this.sender = sender;
        this.logger = logger;
    }

    public Task DispatchAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stripeEvent);

        return stripeEvent.Type switch
        {
            EventTypes.CheckoutSessionCompleted => StartSubscriptionAsync(stripeEvent, cancellationToken),
            EventTypes.InvoicePaymentFailed => RecordPaymentFailureAsync(stripeEvent, cancellationToken),

            // Events we don't handle yet. Acknowledged.
            _ => Task.CompletedTask,
        };
    }

    private async Task StartSubscriptionAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data?.Object is not Session session)
        {
            StripeWebhookLog.CheckoutSkipped(logger, "(none)", "the event carries no Checkout Session", stripeEvent.Id);
            return;
        }

        // One-off payments are not tenant subscriptions; nothing to record.
        if (session.Mode != SubscriptionMode)
        {
            return;
        }

        if (!Guid.TryParse(session.ClientReferenceId, out var tenantId))
        {
            StripeWebhookLog.CheckoutSkipped(logger, session.Id, "client_reference_id is not a tenant id", stripeEvent.Id);
            return;
        }

        if (string.IsNullOrEmpty(session.CustomerId) || string.IsNullOrEmpty(session.SubscriptionId))
        {
            StripeWebhookLog.CheckoutSkipped(logger, session.Id, "no customer or subscription on the session", stripeEvent.Id);
            return;
        }

        if (session.CustomerId.Length > Tenant.StripeIdMaxLength || session.SubscriptionId.Length > Tenant.StripeIdMaxLength)
        {
            StripeWebhookLog.CheckoutSkipped(logger, session.Id, "customer or subscription id is too long", stripeEvent.Id);
            return;
        }

        var tenant = await directory.FindAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            StripeWebhookLog.CheckoutSkipped(logger, session.Id, $"tenant {tenantId} does not exist", stripeEvent.Id);
            return;
        }

        if (tenant.Status == TenantStatus.Deactivated)
        {
            StripeWebhookLog.CheckoutSkipped(logger, session.Id, $"tenant {tenantId} is deactivated", stripeEvent.Id);
            return;
        }

        if (tenant.StripeCustomerId is not null && tenant.StripeCustomerId != session.CustomerId)
        {
            StripeWebhookLog.CheckoutSkipped(logger, session.Id, $"tenant {tenantId} is billed to another Stripe customer", stripeEvent.Id);
            return;
        }

        InitializeTenant(tenant);

        var started = await sender
            .Send(new StartTenantSubscriptionCommand(tenant.TenantId, session.CustomerId, session.SubscriptionId), cancellationToken)
            .ConfigureAwait(false);
        StripeWebhookLog.SubscriptionStarted(logger, tenant.TenantId, session.SubscriptionId, started, stripeEvent.Id);
    }

    // P0-043.4. Invoices name the customer, not the tenant: the customer id
    // recorded at checkout (P0-045.1) finds it. Stripe retries a failing
    // invoice and sends this event each time; only the first moves the tenant.
    private async Task RecordPaymentFailureAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data?.Object is not Invoice invoice || string.IsNullOrEmpty(invoice.Id))
        {
            StripeWebhookLog.PaymentFailureSkipped(logger, "(none)", "the event carries no invoice", stripeEvent.Id);
            return;
        }

        if (invoice.Id.Length > Tenant.StripeIdMaxLength)
        {
            StripeWebhookLog.PaymentFailureSkipped(logger, "(too long)", "invoice id is too long", stripeEvent.Id);
            return;
        }

        if (string.IsNullOrEmpty(invoice.CustomerId) || invoice.CustomerId.Length > Tenant.StripeIdMaxLength)
        {
            StripeWebhookLog.PaymentFailureSkipped(logger, invoice.Id, "no usable customer on the invoice", stripeEvent.Id);
            return;
        }

        var tenant = await directory.FindByStripeCustomerAsync(invoice.CustomerId, cancellationToken).ConfigureAwait(false);
        if (tenant is null)
        {
            StripeWebhookLog.PaymentFailureSkipped(logger, invoice.Id, $"no tenant is billed to customer {invoice.CustomerId}", stripeEvent.Id);
            return;
        }

        // Suspended and deactivated tenants have lost access already; past
        // due would be a step back.
        if (tenant.Status is not (TenantStatus.Active or TenantStatus.PastDue))
        {
            StripeWebhookLog.PaymentFailureSkipped(logger, invoice.Id, $"tenant {tenant.TenantId} is {tenant.Status}", stripeEvent.Id);
            return;
        }

        InitializeTenant(tenant);

        var markedPastDue = await sender
            .Send(new RecordTenantPaymentFailureCommand(tenant.TenantId, invoice.Id), cancellationToken)
            .ConfigureAwait(false);
        StripeWebhookLog.PaymentFailed(logger, tenant.TenantId, invoice.Id, markedPastDue, stripeEvent.Id);
    }

    // The command runs as the tenant it bills, so its audit row is that
    // tenant's. Stripe calls the bare host; a resolved tenant means the
    // webhook was sent to a tenant subdomain, which Stripe never does.
    private void InitializeTenant(BillingTenant tenant)
    {
        if (tenantContext.IsResolved)
        {
            throw new InvalidOperationException("Stripe webhooks must target the bare API host, not a tenant subdomain.");
        }

        tenantInitializer.Initialize(tenant.TenantId, tenant.Slug);
    }
}
