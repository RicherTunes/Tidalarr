#if NET9_0_OR_GREATER
using System;
using System.IO;
using System.Threading.Tasks;
using TidalCLI;
using Xunit;

namespace Tidalarr.Tests;

public class TidalCliIntegrationTests
{
    [Fact]
    public async Task RunAsync_TestOauth_PrintsExpectedMarkers()
    {
        var sw = new StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        try
        {
            var code = await Program.RunAsync(new[] { "test-oauth" });
            var output = sw.ToString();
            Assert.Equal(0, code);
            Assert.Contains("OAuth URL Generated Successfully", output);
            Assert.Contains("code_challenge_method=S256", output);
            Assert.Contains("client_id", output);
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    [Fact]
    public async Task RunAsync_TestCallback_PrintsValidAndInvalid()
    {
        var sw = new StringWriter();
        var original = Console.Out;
        Console.SetOut(sw);
        try
        {
            var code = await Program.RunAsync(new[] { "test-callback" });
            var output = sw.ToString();
            Assert.Equal(0, code);
            Assert.Contains("Valid Callback Test", output);
            Assert.Contains("Success: True", output);
            Assert.Contains("Invalid Callback Test", output);
            Assert.Contains("error", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}





#endif

