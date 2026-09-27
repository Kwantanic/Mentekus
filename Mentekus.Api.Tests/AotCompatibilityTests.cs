using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit;

namespace Mentekus.Api.Tests;

public class AotCompatibilityTests
{
    [Fact]
    public void DynamicCode_IsDisabled()
    {
        Assert.False(RuntimeFeature.IsDynamicCodeSupported);
    }

    [Fact]
    public void ReflectionJson_IsDisabled()
    {
        Assert.False(JsonSerializer.IsReflectionEnabledByDefault);
    }

    [Fact]
    public void UnregisteredType_CannotUseReflectionJson()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            JsonSerializer.Serialize(new UnregisteredProbe("aot")));

        Assert.Contains("Reflection-based serialization has been disabled", exception.Message);
    }

    private sealed record UnregisteredProbe(string Name);
}
