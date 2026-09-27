using Mentekus.Api.Features.Auth;
using Xunit;

namespace Mentekus.Api.Tests;

public class AuthUnitTests
{
    [Fact]
    public void PasswordHasher_VerifiesTheOriginalPassword()
    {
        var hash = PasswordHasher.Hash("correct horse");

        Assert.True(PasswordHasher.Verify("correct horse", hash));
        Assert.False(PasswordHasher.Verify("wrong horse", hash));
        Assert.False(PasswordHasher.Verify("correct horse", "not-a-hash"));
    }

    [Theory]
    [InlineData("Development", "POST", false)]
    [InlineData("development", "PUT", false)]
    [InlineData("Testing", "POST", true)]
    [InlineData("Production", "DELETE", true)]
    [InlineData("Production", "GET", false)]
    [InlineData("Testing", "HEAD", false)]
    public void XsrfPolicy_SkipsValidationOnlyInDevelopment(string environment, string method, bool required)
    {
        Assert.Equal(required, XsrfPolicy.RequiresValidation(environment, method));
    }
}
