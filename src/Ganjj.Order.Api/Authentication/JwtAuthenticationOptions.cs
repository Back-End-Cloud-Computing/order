namespace Ganjj.Order.Api.Authentication;

public sealed class JwtAuthenticationOptions
{
    public const string SectionName = "Authentication:Jwt";

    public string PublicKeyPath { get; init; } = "public.pem";
    public string Issuer { get; init; } = "ganjj-authorization";
}
