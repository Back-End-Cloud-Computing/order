using System.Text;
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
        if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                $"{JwtAuthenticationOptions.SectionName}:SigningKey must contain at least 32 bytes.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuer = !string.IsNullOrWhiteSpace(jwt.Issuer),
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = !string.IsNullOrWhiteSpace(jwt.Audience),
                    ValidAudience = jwt.Audience,
                    NameClaimType = "id"
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrWhiteSpace(context.Token) &&
                            context.Request.Cookies.TryGetValue("accessToken", out var cookieToken))
                        {
                            context.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
            options.AddPolicy(AdminPolicy, policy => policy.RequireAssertion(context =>
                context.User.Claims.Any(claim =>
                    claim.Type == "isAdmin" &&
                    string.Equals(claim.Value, "true", StringComparison.OrdinalIgnoreCase)))));

        return services;
    }
}
