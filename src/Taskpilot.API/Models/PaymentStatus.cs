namespace Taskpilot.API.Models;

/// <summary>Whether a completed marketplace task has been paid out to the assignee.</summary>
public enum PaymentStatus
{
    /// <summary>Not paid yet (default).</summary>
    Unpaid = 0,

    /// <summary>A checkout session was created and is awaiting completion at Stripe.</summary>
    Pending = 1,

    /// <summary>Payment confirmed as completed.</summary>
    Paid = 2,
}
