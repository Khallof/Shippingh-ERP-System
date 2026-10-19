namespace ShippingERP.Tests.Common;

/// <summary>Settings shared by every test. The values are for tests only and are not secrets.</summary>
public static class TestSettings
{
    /// <summary>JWT key used only by tests (at least 32 characters).</summary>
    public const string JwtSecretKey = "tests-only-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";

    /// <summary>A password that satisfies the Identity rules.</summary>
    public const string StrongPassword = "Test#Pass2026";
}
