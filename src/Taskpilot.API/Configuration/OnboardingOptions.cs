namespace Taskpilot.API.Configuration;

/// <summary>
/// What a brand-new account starts with. On the public demo an empty dashboard makes the
/// app look like it does nothing, so a small starter project is created by default; a real
/// deployment can switch it off with <c>Onboarding__CreateSampleProject=false</c>.
/// </summary>
public class OnboardingOptions
{
    /// <summary>Create a starter project with a few example tasks for every new account.</summary>
    public bool CreateSampleProject { get; set; } = true;
}
