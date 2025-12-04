namespace Tidalarr.Tests.Utils;

public sealed class CliFactAttribute : FactAttribute
{
    public CliFactAttribute()
    {
        string? enabled = Environment.GetEnvironmentVariable("RUN_REAL_CLI_TESTS");
        if (!string.Equals(enabled, "1", StringComparison.Ordinal))
        {
            Skip = "Set RUN_REAL_CLI_TESTS=1 to enable CLI/packaging tests on this machine.";
        }
    }
}

