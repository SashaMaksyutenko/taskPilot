namespace Taskpilot.API.DTOs.Auth;

/// <summary>Returned when starting 2FA enrollment: the secret and the QR/otpauth URI.</summary>
public class TwoFactorSetupDto
{
    public string Secret { get; set; } = string.Empty;
    public string OtpauthUri { get; set; } = string.Empty;
}

/// <summary>A TOTP code submitted to enable or disable 2FA.</summary>
public class TwoFactorCodeDto
{
    public string Code { get; set; } = string.Empty;
}
