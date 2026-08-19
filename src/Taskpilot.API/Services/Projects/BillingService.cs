using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Taskpilot.API.Common;
using Taskpilot.API.Configuration;
using Taskpilot.API.Data;
using Taskpilot.API.DTOs.Billing;
using Taskpilot.API.Models;

namespace Taskpilot.API.Services;

/// <inheritdoc />
public class BillingService : IBillingService
{
    /// <summary>Projects the Free plan allows (enforced only when Stripe billing is configured).</summary>
    public const int FreeProjectLimit = 3;

    private const string PlanFree = "Free";
    private const string PlanPro = "Pro";

    private readonly TaskpilotDbContext _context;
    private readonly IStripeBillingClient _stripe;
    private readonly StripeOptions _options;
    private readonly INotificationService _notifications;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        TaskpilotDbContext context,
        IStripeBillingClient stripe,
        IOptions<StripeOptions> options,
        INotificationService notifications,
        ILogger<BillingService> logger)
    {
        _context = context;
        _stripe = stripe;
        _options = options.Value;
        _notifications = notifications;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BillingStatusDto> GetStatusAsync()
    {
        var settings = await GetOrCreateAsync();
        var billingEnabled = _stripe.IsEnabled;
        var projectCount = await _context.Projects.CountAsync();
        var limit = billingEnabled && settings.Plan == PlanFree ? FreeProjectLimit : -1;

        return new BillingStatusDto
        {
            Plan = settings.Plan,
            BillingEnabled = billingEnabled,
            ProjectLimit = limit,
            ProjectCount = projectCount,
            RenewsAt = settings.PlanRenewsAt,
            CanManage = !string.IsNullOrEmpty(settings.StripeCustomerId),
            PastDue = settings.PlanPastDue,
            AnnualAvailable = _options.AnnualConfigured,
        };
    }

    /// <inheritdoc />
    public async Task<bool> CanCreateProjectAsync()
    {
        // No payment provider ⇒ nothing to upgrade to ⇒ never gate.
        if (!_stripe.IsEnabled) return true;

        var plan = await _context.OrganizationSettings.Select(s => s.Plan).FirstOrDefaultAsync() ?? PlanFree;
        if (plan == PlanPro) return true;

        return await _context.Projects.CountAsync() < FreeProjectLimit;
    }

    /// <inheritdoc />
    public async Task<bool> IsProAsync()
    {
        if (!_stripe.IsEnabled) return true; // no billing ⇒ everything unlocked
        var plan = await _context.OrganizationSettings.Select(s => s.Plan).FirstOrDefaultAsync() ?? PlanFree;
        return plan == PlanPro;
    }

    /// <inheritdoc />
    public async Task<Result<string>> CreateCheckoutAsync(string userEmail, string successUrl, string cancelUrl, bool annual)
    {
        if (!_stripe.IsEnabled)
            return Result<string>.Fail("Subscriptions are not configured.");

        // Fall back to monthly if a yearly price isn't configured.
        var priceId = annual && _options.AnnualConfigured ? _options.ProAnnualPriceId : _options.ProPriceId;
        var settings = await GetOrCreateAsync();
        return await _stripe.CreateSubscriptionCheckoutAsync(priceId, settings.StripeCustomerId, userEmail, successUrl, cancelUrl);
    }

    /// <inheritdoc />
    public async Task<Result<string>> CreatePortalAsync(string returnUrl)
    {
        var settings = await GetOrCreateAsync();
        if (string.IsNullOrEmpty(settings.StripeCustomerId))
            return Result<string>.Fail("There is no subscription to manage yet.");

        return await _stripe.CreatePortalSessionAsync(settings.StripeCustomerId, returnUrl);
    }

    /// <inheritdoc />
    public async Task ProcessWebhookAsync(string payload)
    {
        JsonElement obj;
        string? type;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("object", out obj))
                return;
            // Clone so it survives the using-scope of the document.
            obj = obj.Clone();
        }
        catch (JsonException)
        {
            return;
        }

        var settings = await GetOrCreateAsync();
        switch (type)
        {
            case "checkout.session.completed" when Str(obj, "mode") == "subscription":
                settings.StripeCustomerId = Str(obj, "customer") ?? settings.StripeCustomerId;
                settings.StripeSubscriptionId = Str(obj, "subscription") ?? settings.StripeSubscriptionId;
                settings.Plan = PlanPro;
                settings.PlanPastDue = false;
                await SyncRenewalAsync(settings);
                break;

            case "customer.subscription.updated":
                var status = Str(obj, "status");
                if (status is "active" or "trialing")
                {
                    // Healthy: on Pro, payment current.
                    settings.Plan = PlanPro;
                    settings.PlanPastDue = false;
                    settings.PlanRenewsAt = UnixToUtc(obj, "current_period_end");
                }
                else if (status == "past_due")
                {
                    // Renewal failed but Stripe is retrying — keep Pro during the grace window.
                    settings.Plan = PlanPro;
                    settings.PlanPastDue = true;
                }
                else
                {
                    // canceled / unpaid / incomplete_expired — grace exhausted.
                    settings.Plan = PlanFree;
                    settings.PlanPastDue = false;
                    settings.PlanRenewsAt = null;
                    settings.StripeSubscriptionId = null;
                }
                break;

            case "invoice.payment_failed":
                // A renewal charge failed — flag it and tell the admins to fix their card.
                settings.PlanPastDue = true;
                await NotifyAdminsAsync("A subscription payment failed. Update your card in Plan & billing to keep Pro.");
                break;

            case "invoice.paid":
            case "invoice.payment_succeeded":
                // Payment recovered.
                settings.PlanPastDue = false;
                break;

            case "customer.subscription.deleted":
                settings.Plan = PlanFree;
                settings.PlanPastDue = false;
                settings.StripeSubscriptionId = null;
                settings.PlanRenewsAt = null;
                break;

            default:
                return; // event we don't act on
        }

        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Billing webhook applied: {Type} → plan {Plan} (pastDue={PastDue})", type, settings.Plan, settings.PlanPastDue);
    }

    /// <summary>Sends an in-app notification to every admin (dunning / billing alerts).</summary>
    private async Task NotifyAdminsAsync(string message)
    {
        var adminIds = await _context.Users
            .Where(u => u.Role == Role.Admin && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();
        foreach (var id in adminIds)
            await _notifications.CreateAsync(id, NotificationType.General, message, "/admin?tab=settings");
    }

    private async Task SyncRenewalAsync(OrganizationSettings settings)
    {
        if (string.IsNullOrEmpty(settings.StripeSubscriptionId)) return;
        var sub = await _stripe.GetSubscriptionAsync(settings.StripeSubscriptionId);
        if (sub.Succeeded) settings.PlanRenewsAt = sub.Value.renewsAt;
    }

    private async Task<OrganizationSettings> GetOrCreateAsync()
    {
        var settings = await _context.OrganizationSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new OrganizationSettings { Id = OrganizationSettings.SingletonId };
            _context.OrganizationSettings.Add(settings);
            await _context.SaveChangesAsync();
        }
        return settings;
    }

    private static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static DateTime? UnixToUtc(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.TryGetInt64(out var unix)
            ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime
            : null;
}
