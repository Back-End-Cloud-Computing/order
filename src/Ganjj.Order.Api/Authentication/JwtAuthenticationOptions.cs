namespace Ganjj.Order.Api.Authentication;

public sealed class JwtAuthenticationOptions
{
    public const string SectionName = "Authentication:Jwt";

    public string SigningKey { get; init; } = string.Empty;
    public string? Issuer { get; init; }
    public string? Audience { get; init; }
}
