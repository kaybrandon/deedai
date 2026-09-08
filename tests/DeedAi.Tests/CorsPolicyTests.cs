using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DeedAi.Tests;

public sealed class CorsPolicyTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public CorsPolicyTests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public void DeedAi_policy_uses_explicit_origins_not_any_origin_plus_credentials()
    {
        var options = _factory.Services.GetRequiredService<IOptions<CorsOptions>>().Value;
        var policy = options.GetPolicy("DeedAi");
        Assert.NotNull(policy);
        Assert.False(policy!.AllowAnyOrigin);
        Assert.Contains("http://localhost:5173", policy.Origins);
        if (policy.SupportsCredentials)
        {
            Assert.NotEmpty(policy.Origins);
            Assert.DoesNotContain("*", policy.Origins);
        }
    }
}
