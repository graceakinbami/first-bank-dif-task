using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NovaPay.Api.Common;

namespace NovaPay.Api.Auth;

/// <summary>
/// Minimal bearer-token check: every endpoint requires "Authorization: Bearer &lt;token&gt;" where
/// the token matches NovaPay:BearerToken in configuration. A stand-in for real OAuth2/OIDC-issued
/// JWTs (which is what a production NovaPay would front with, backed by BVN/NIN-verified identity)
/// — swapping this handler for JwtBearer later requires no controller changes.
/// </summary>
public class StaticBearerTokenAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<NovaPayOptions> novaPayOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "Bearer";
    private readonly string _expectedToken = novaPayOptions.Value.BearerToken;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header."));

        var value = authHeader.ToString();
        const string prefix = "Bearer ";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Authorization header must use the Bearer scheme."));

        var token = value[prefix.Length..].Trim();
        if (!string.Equals(token, _expectedToken, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.Fail("Invalid bearer token."));

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "novapay-client")], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
