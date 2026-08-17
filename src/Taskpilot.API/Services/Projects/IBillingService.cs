using Taskpilot.API.Common;
using Taskpilot.API.DTOs.Billing;

namespace Taskpilot.API.Services;

/// <summary>
/// Workspace billing: the current plan, the plan-gated project limit, Stripe subscription checkout
/// and the billing portal, and applying Stripe webhook events to the plan. Single-tenant, so it
/// always operates on the one organization-settings row.
/// </summary>
public interface IBillingService
{
    /// <summary>Current plan, limits and manage-ability for the workspace.</summary>
    Task<BillingStatusDto> GetStatusAsync();

    /// <summary>True if another project may be created under the current plan.</summary>
    Task<bool> CanCreateProjectAsync();

    /// <summary>Starts a Pro subscription checkout; returns the hosted Stripe URL.</summary>
    Task<Result<string>> CreateCheckoutAsync(string userEmail, string successUrl, string cancelUrl);

    /// <summary>Opens the Stripe billing portal to manage/cancel; returns the hosted URL.</summary>
    Task<Result<string>> CreatePortalAsync(string returnUrl);

    /// <summary>Applies a (signature-verified) Stripe webhook event to the plan.</summary>
    Task ProcessWebhookAsync(string payload);
}
