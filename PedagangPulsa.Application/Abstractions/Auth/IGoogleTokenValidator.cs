namespace PedagangPulsa.Application.Abstractions.Auth;

public class GoogleTokenValidationResult
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Picture { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public bool EmailVerified { get; set; }
}

public interface IGoogleTokenValidator
{
    Task<(GoogleTokenValidationResult? Result, string ErrorMessage)> ValidateAsync(string idToken);
}
