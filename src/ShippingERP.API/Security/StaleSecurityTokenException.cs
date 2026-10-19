using Microsoft.IdentityModel.Tokens;

namespace ShippingERP.API.Security;

/// <summary>
/// The token is correctly signed and not expired, but was issued before the user's roles, permissions,
/// branch or password changed. The client should call /auth/refresh-token to get an up-to-date token,
/// so it is reported as TOKEN_EXPIRED.
/// </summary>
public sealed class StaleSecurityTokenException : SecurityTokenException
{
    public StaleSecurityTokenException()
        : base("The access token was issued before the user's security data changed.")
    {
    }
}
