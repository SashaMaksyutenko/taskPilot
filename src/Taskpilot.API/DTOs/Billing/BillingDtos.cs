namespace Taskpilot.API.DTOs.Billing;

/// <summary>The workspace's current plan and what it allows.</summary>
public class BillingStatusDto
{
    /// <summary>"Free" or "Pro".</summary>
    public string Plan { get; set; } = "Free";

    /// <summary>True when Stripe subscriptions are configured — only then do plan limits apply.</summary>
    public bool BillingEnabled { get; set; }

    /// <summary>Max projects the workspace may have (-1 = unlimited).</summary>
    public int ProjectLimit { get; set; }

    /// <summary>How many projects exist now.</summary>
    public int ProjectCount { get; set; }

    /// <summary>When the Pro period renews/ends (null on Free).</summary>
    public DateTime? RenewsAt { get; set; }

    /// <summary>True when a Stripe customer exists, so the billing portal can be opened.</summary>
    public bool CanManage { get; set; }
}

/// <summary>A hosted Stripe URL for the client to redirect to.</summary>
public class BillingUrlDto
{
    public string Url { get; set; } = string.Empty;
}
