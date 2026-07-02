using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Tidalarr.Tests.Contracts;

public class CiWorkflowContractTests
{
    [Fact]
    public void GiteaCiWorkflow_RunsSecretScan()
    {
        var workflow = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".gitea", "workflows", "ci.yml"));

        AssertGiteaSecretScanContract(workflow);
    }

    [Fact]
    public void PluginRootGithubWorkflow_IsGuardedCiMirror()
    {
        var workflowsRoot = Path.Combine(FindRepositoryRoot(), ".github", "workflows");
        var workflowFiles = Directory.Exists(workflowsRoot)
            ? Directory.GetFiles(workflowsRoot, "*.yml")
                .Concat(Directory.GetFiles(workflowsRoot, "*.yaml"))
                .ToArray()
            : [];

        workflowFiles.Should().ContainSingle("the plugin carries one declared GitHub CI mirror");
        Path.GetFileName(workflowFiles.Single()).Should().Be("ci.yml");

        var workflow = File.ReadAllText(workflowFiles.Single());
        AssertGithubMirrorContract(workflow);
    }

    [Fact]
    public void GiteaCiWorkflow_RejectsGitleaksCommandOutsideSecretScanJob()
    {
        const string workflow = """
name: CI
on:
  pull_request:
jobs:
  secret-scan:
    runs-on: ubuntu-latest
    steps:
      - run: echo no-op
  lint:
    runs-on: ubuntu-latest
    steps:
      - run: |
          sha256sum -c -
          /tmp/gitleaks detect --source . --no-banner --redact --exit-code 1
  verify:
    needs: [lint, secret-scan]
    runs-on: ubuntu-latest
    steps:
      - run: echo verify
""";

        Assert.ThrowsAny<Exception>(() => AssertGiteaSecretScanContract(workflow));
    }

    private static void AssertGiteaSecretScanContract(string workflow)
    {
        var secretScan = ExtractJobBlock(workflow, "secret-scan");
        secretScan.Should().Contain("sha256sum -c -");
        secretScan.Should().Contain("/tmp/gitleaks detect --source . --no-banner --redact --exit-code 1");

        var verify = ExtractJobBlock(workflow, "verify");
        var verifyNeeds = ExtractNeeds(verify);
        verifyNeeds.Should().Contain("lint");
        verifyNeeds.Should().Contain("secret-scan");
    }

    private static void AssertGithubMirrorContract(string workflow)
    {
        const string githubOnlyGuard = "if: ${{ github.server_url == 'https://github.com' }}";

        foreach (var jobName in new[] { "secret-scan", "lint", "verify" })
        {
            ExtractJobBlock(workflow, jobName).Should().Contain(githubOnlyGuard,
                "GitHub mirror job '{0}' must not run on Gitea", jobName);
        }

        workflow.Should().Contain("run-plugin-lint-gates.ps1");
        workflow.Should().Contain("repin-common-submodule.sh --verify-only");
        workflow.Should().Contain("gitleaks detect");
        workflow.Should().Contain("scripts/verify-local.ps1");

        workflow.Should().NotContain("Invoke-FallbackGate", "fallback lint helpers can drift from Common");
        workflow.Should().NotMatchRegex(
            @"(?m)^\s*run:\s*.*(ecosystem-parity-lint|lint-date-parsing|lint-sync-over-async|lint-test-traits|lint-doc-script-refs|lint-gitea-secret-scan)\.ps1",
            "the GitHub mirror must route lint through run-plugin-lint-gates.ps1");
        workflow.Should().NotMatchRegex(
            @"-(SkipDateParsing|SkipSyncOverAsync|SkipTestTraits|SkipEcosystemParity|SkipVersionContract|SkipPluginContractTests|SkipDocRefs|SkipGiteaSecretScan)\b",
            "the GitHub mirror must run the full shared lint gate set");
    }

    private static string ExtractJobBlock(string workflow, string jobName)
    {
        var header = $"  {jobName}:";
        var lines = workflow.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => string.Equals(line.TrimEnd(), header, StringComparison.Ordinal));
        start.Should().BeGreaterThanOrEqualTo(0, $"workflow should define a top-level '{jobName}' job");

        var block = new StringBuilder(lines[start]);
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"^  [A-Za-z0-9_-]+:\s*$"))
            {
                break;
            }

            block.AppendLine();
            block.Append(lines[i]);
        }

        return block.ToString();
    }

    private static HashSet<string> ExtractNeeds(string jobBlock)
    {
        var needs = new HashSet<string>(StringComparer.Ordinal);

        var inline = Regex.Match(jobBlock, @"(?m)^\s+needs:\s*\[(?<needs>[^\]]+)\]\s*$");
        if (inline.Success)
        {
            foreach (var need in inline.Groups["needs"].Value.Split(','))
            {
                needs.Add(TrimYamlToken(need));
            }
        }

        var scalar = Regex.Match(jobBlock, @"(?m)^\s+needs:\s*(?<need>[A-Za-z0-9_-]+)\s*$");
        if (scalar.Success)
        {
            needs.Add(TrimYamlToken(scalar.Groups["need"].Value));
        }

        var block = Regex.Match(jobBlock, @"(?ms)^\s+needs:\s*\r?\n(?<items>(?:\s+-\s*.+(?:\r?\n|$))*)");
        if (block.Success)
        {
            foreach (Match item in Regex.Matches(block.Groups["items"].Value, @"(?m)^\s+-\s*(?<need>[A-Za-z0-9_-]+)\s*$"))
            {
                needs.Add(TrimYamlToken(item.Groups["need"].Value));
            }
        }

        return needs;
    }

    private static string TrimYamlToken(string value)
    {
        return value.Trim().Trim('"', '\'');
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".gitea", "workflows", "ci.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate repository root from {AppContext.BaseDirectory}");
    }
}
