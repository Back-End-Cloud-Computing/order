using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Ganjj.Order.Api.Authentication;

public static class AuthenticationExtensions
{
    public const string AdminPolicy = "Admin";

    public static IServiceCollection AddOrderAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtAuthenticationOptions.SectionName)
            .Get<JwtAuthenticationOptions>() ?? new JwtAuthenticationOptions();
        if (string.IsNullOrWhiteSpace(jwt.PublicKeyPath))
        {
            throw new InvalidOperationException(
                $"{JwtAuthenticationOptions.SectionName}:PublicKeyPath is required.");
        }
        if (string.IsNullOrWhiteSpace(jwt.Issuer))
        {
            throw new InvalidOperationException($"{JwtAuthenticationOptions.SectionName}:Issuer is required.");
        }

        var publicKeyPath = Path.IsPathRooted(jwt.PublicKeyPath)
            ? jwt.PublicKeyPath
            : Path.Combine(AppContext.BaseDirectory, jwt.PublicKeyPath);
        if (!File.Exists(publicKeyPath))
        {
            throw new InvalidOperationException($"JWT public key file was not found at '{publicKeyPath}'.");
        }

        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(File.ReadAllText(publicKeyPath));
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            throw new InvalidOperationException("The configured JWT public key is not a valid PEM RSA key.", exception);
        }

        var signingKey = new RsaSecurityKey(rsa.ExportParameters(false));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = false,
                    NameClaimType = "email",
                    RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var type = context.Principal?.FindFirst("typ")?.Value;
                        var subject = context.Principal?.FindFirst("sub")?.Value;
                        var email = context.Principal?.FindFirst("email")?.Value;
                        if (!string.Equals(type, "access", StringComparison.Ordinal) ||
                            !Guid.TryParse(subject, out var accountId) || accountId == Guid.Empty ||
                            string.IsNullOrWhiteSpace(email))
                        {
                            context.Fail(
                                "The token must be an access token with valid sub and email claims.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
            options.AddPolicy(AdminPolicy, policy => policy.RequireClaim("role", "ADMIN")));

        return services;
    }
}
