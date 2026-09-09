using DeedAi.Infrastructure.Security;

namespace DeedAi.Tests;

public sealed class PasswordRulesTests
{
    [Theory]
    [InlineData("ChangeMe!1")]
    [InlineData("NewPass!99")]
    [InlineData("Aa1!aaaa")]
    public void Accepts_identity_style_passwords(string password)
    {
        Assert.Null(PasswordRules.Validate(password));
    }

    [Theory]
    [InlineData("short1!")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoDigits!!")]
    [InlineData("NoSymbol12")]
    [InlineData("password")]
    public void Rejects_passwords_missing_a_rule(string password)
    {
        Assert.Equal(PasswordRules.RequirementMessage, PasswordRules.Validate(password));
    }

    [Fact]
    public void Required_password_cannot_be_empty()
    {
        Assert.Equal(PasswordRules.RequiredMessage, PasswordRules.Validate(""));
        Assert.Equal(PasswordRules.RequiredMessage, PasswordRules.Validate("   "));
        Assert.Null(PasswordRules.Validate(null, required: false));
        Assert.Null(PasswordRules.Validate("", required: false));
    }
}
