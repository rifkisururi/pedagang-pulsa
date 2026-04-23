using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PedagangPulsa.Application.Abstractions.Auth;

namespace PedagangPulsa.Infrastructure.Auth;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly string _clientId;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(
        IConfiguration configuration,
        ILogger<GoogleTokenValidator> logger)
    {
        _clientId = configuration["GoogleAuth:ClientId"]
            ?? throw new InvalidOperationException("GoogleAuth:ClientId is not configured");
        _logger = logger;
    }

    public async Task<(GoogleTokenValidationResult? Result, string ErrorMessage)> ValidateAsync(string idToken)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_clientId]
            });

            return (new GoogleTokenValidationResult
            {
                Email = payload.Email,
                Name = payload.Name,
                Picture = payload.Picture,
                Subject = payload.Subject,
                GivenName = payload.GivenName,
                FamilyName = payload.FamilyName,
                EmailVerified = payload.EmailVerified
            }, string.Empty);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Invalid Google ID token");
            return (null, "Invalid Google ID token");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Google ID token");
            return (null, "Failed to validate Google ID token");
        }
    }
}
