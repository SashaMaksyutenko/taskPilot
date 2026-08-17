using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Taskpilot.API.Common;
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
    private readonly ILogger<BillingService> _logger;

    public BillingService(TaskpilotDbContext context, IStripeBillingClient stripe, ILogger<BillingService> logger)
    {
        _context = context;
        _stripe = stripe;
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
    public async Task<Result<string>> CreateCheckoutAsync(string userEmail, string successUrl, string cancelUrl)
    {
        if (!_stripe.IsEnabled)
            return Result<string>.Fail("Subscriptions are not configured.");

        var settings = await GetOrCreateAsync();
        return await _stripe.CreateSubscriptionCheckoutAsync(settings.StripeCustomerId, userEmail, successUrl, cancelUrl);
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
                await SyncRenewalAsync(settings);
                break;

            case "customer.subscription.updated":
                var active = Str(obj, "status") is "active" or "trialing";
                settings.Plan = active ? PlanPro : PlanFree;
                settings.PlanRenewsAt = active ? UnixToUtc(obj, "current_period_end") : null;
                if (!active) settings.StripeSubscriptionId = null;
                break;

            case "customer.subscription.deleted":
                settings.Plan = PlanFree;
                settings.StripeSubscriptionId = null;
                settings.PlanRenewsAt = null;
                break;

            default:
                return; // event we don't act on
        }

        settings.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Billing webhook applied: {Type} → plan {Plan}", type, settings.Plan);
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
