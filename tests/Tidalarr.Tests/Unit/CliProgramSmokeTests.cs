using System;
using System.IO;
using System.Threading.Tasks;
using TidalCLI;
using Xunit;

namespace Tidalarr.Tests.Unit;

public class CliProgramSmokeTests
{
    [Fact]
    public async Task RunAsync_WithNoArguments_ExecutesTestOauthFlow()
    {
        var originalOut = Console.Out;
        using var capture = new StringWriter();
        Console.SetOut(capture);
        try
        {
            var exitCode = await Program.RunAsync(Array.Empty<string>());

            Assert.Equal(0, exitCode);

            var output = capture.ToString();
            Assert.Contains("OAuth URL Generated", output);
            Assert.Contains("code_challenge", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_WithTraceFlag_SetsEnvironmentVariable()
    {
        var originalOut = Console.Out;
        var originalTrace = Environment.GetEnvironmentVariable("TIDALARR_HTTP_TRACE");
        using var capture = new StringWriter();
        Console.SetOut(capture);
        try
        {
            Environment.SetEnvironmentVariable("TIDALARR_HTTP_TRACE", null, EnvironmentVariableTarget.Process);

            var exitCode = await Program.RunAsync(new[] { "--trace-http", "test-oauth" });

            Assert.Equal(0, exitCode);
            Assert.Equal("1", Environment.GetEnvironmentVariable("TIDALARR_HTTP_TRACE"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TIDALARR_HTTP_TRACE", originalTrace, EnvironmentVariableTarget.Process);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_TestCallback_PrintsCallbackSummary()
    {
        var originalOut = Console.Out;
        using var capture = new StringWriter();
        Console.SetOut(capture);
        try
        {
            var exitCode = await Program.RunAsync(new[] { "test-callback" });

            Assert.Equal(0, exitCode);

            var output = capture.ToString();
            Assert.Contains("Testing OAuth Callback Parsing", output);
            Assert.Contains("Valid Callback Test", output);
            Assert.Contains("Invalid Callback Test", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task RunAsync_WithUnknownCommand_PrintsHelpfulMessage()
    {
        var originalOut = Console.Out;
        using var capture = new StringWriter();
        Console.SetOut(capture);
        try
        {
            var exitCode = await Program.RunAsync(new[] { "definitely-not-a-command" });

            Assert.Equal(0, exitCode);

            var output = capture.ToString();
            Assert.Contains("Unknown command", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}


