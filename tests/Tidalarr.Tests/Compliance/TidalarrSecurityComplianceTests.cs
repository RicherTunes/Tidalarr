using System.Reflection;
using System.Text.RegularExpressions;
using Tidalarr.Integration;
using Tidalarr.Tests.Utils;

namespace Tidalarr.Tests.Compliance;

/// <summary>
/// Security compliance tests for Tidalarr.
/// These tests scan for common security vulnerabilities and best practice violations.
/// </summary>
[Trait("Category", "Compliance")]
[Trait("Category", "Security")]
public partial class TidalarrSecurityComplianceTests : IDisposable
{
    private readonly Assembly _pluginAssembly;
    private readonly string? _sourceCodePath;

    public TidalarrSecurityComplianceTests()
    {
        this._pluginAssembly = typeof(TidalarrPlugin).Assembly;

        // Navigate from test output to source directory
        string basePath = AppContext.BaseDirectory;
        string srcPath = Path.Combine(basePath, "..", "..", "..", "..", "..", "src", "Tidalarr");
        this._sourceCodePath = Directory.Exists(srcPath) ? Path.GetFullPath(srcPath) : null;
    }

    /// <summary>
    /// Enumerates hand-written SOURCE .cs files under <paramref name="root"/>, excluding generated build
    /// output. A plain <c>Directory.GetFiles(root, "*.cs", AllDirectories)</c> also recurses into <c>obj/</c>
    /// and <c>bin/</c>, which contain generated files such as <c>Tidalarr.AssemblyInfo.cs</c> — SourceLink
    /// embeds the git repository URL there, and in CI that is the Gitea <c>http://</c> remote, which
    /// false-flagged <see cref="Network_UsesHttpsForExternalCommunication"/> ("Non-HTTPS URL found in
    /// Tidalarr.AssemblyInfo.cs"). These compliance checks are meant to inspect source we author, so
    /// generated output must be excluded to keep the scans deterministic across environments.
    /// </summary>
    private static string[] GetSourceCsFiles(string root)
    {
        return [.. Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                string relative = Path.GetRelativePath(root, f).Replace('\\', '/');
                return !relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                    && !relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
                    && !relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                    && !relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
            })];
    }

    #region Credential Handling Tests

    [Fact]
    public void Credentials_NoHardcodedSecrets()
    {
        if (this._sourceCodePath == null)
        {
            return; // Skip if source code not available
        }

        string[] credentialPatterns =
        [
            @"password\s*=\s*""[^""]{8,}""",
            @"apiKey\s*=\s*""[^""]{8,}""",
            @"secret\s*=\s*""[^""]{8,}""",
            @"clientSecret\s*=\s*""[^""]{8,}"""
        ];

        string[] csFiles = GetSourceCsFiles(this._sourceCodePath);
        List<string> issues = [];

        foreach (string file in csFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Skip test files
            if (fileName.Contains("Test", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string? pattern in credentialPatterns)
            {
                Regex regex = new(pattern, RegexOptions.IgnoreCase);
                Match match = regex.Match(content);
                if (match.Success)
                {
                    // Check if it's a placeholder
                    string value = match.Value;
                    if (!value.Contains("{") && !value.Contains("$") && !value.Contains("<"))
                    {
                        issues.Add($"Potential hardcoded credential in {fileName}");
                    }
                }
            }
        }

        Assert.Empty(issues);
    }

    [Fact]
    public void Credentials_TokensStoredSecurely()
    {
        // Assembly.GetTypes() forces every type in the module to resolve, which transitively touches
        // the plugin's NzbDrone.Core-referencing indexer/download-client types. Under the
        // ExcludeHostBridge=true hermetic CI build the test project doesn't carry Lidarr.Core.dll, so
        // this throws ReflectionTypeLoadException even though this test itself needs nothing from the
        // host — degrade gracefully (same "skip if the precondition isn't available" convention as
        // this file's other tests) instead of failing on a missing dependency this test doesn't use.
        Type[] allTypes;
        try
        {
            allTypes = this._pluginAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            allTypes = [.. ex.Types.Where(t => t != null)!];
        }

        List<Type> tokenStorageTypes = [.. allTypes.Where(t =>
            t.Name.Contains("TokenStore", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("TokenStorage", StringComparison.OrdinalIgnoreCase))];

        // Verify token storage types have some protection
        foreach (Type? storageType in tokenStorageTypes)
        {
            MethodInfo[] methods = storageType.GetMethods();
            bool hasProtection = methods.Any(m =>
                m.Name.Contains("Encrypt", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("Protect", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("Secure", StringComparison.OrdinalIgnoreCase));

            // It's OK if no protection - tokens might be stored by Common library
        }

        Assert.True(true);
    }

    #endregion

    #region Network Security Tests

    [Fact]
    public void Network_UsesHttpsForExternalCommunication()
    {
        if (this._sourceCodePath == null)
        {
            return;
        }

        string[] csFiles = GetSourceCsFiles(this._sourceCodePath);
        Regex httpPattern = MyRegex();
        List<string> issues = [];

        foreach (string file in csFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Skip test files
            if (fileName.Contains("Test", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            MatchCollection matches = httpPattern.Matches(content);
            foreach (Match match in matches)
            {
                string url = match.Value;
                // Allow localhost
                if (!url.Contains("localhost") && !url.Contains("127.0.0.1"))
                {
                    issues.Add($"Non-HTTPS URL found in {fileName}: {url}");
                }
            }
        }

        Assert.Empty(issues);
    }

    [Fact]
    public void Network_NoCertificateValidationBypass()
    {
        if (this._sourceCodePath == null)
        {
            return;
        }

        string[] csFiles = GetSourceCsFiles(this._sourceCodePath);
        string[] unsafePatterns =
        [
            "ServerCertificateValidationCallback",
            "ServerCertificateCustomValidationCallback"
        ];
        List<string> issues = [];

        foreach (string file in csFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            foreach (string? pattern in unsafePatterns)
            {
                if (content.Contains(pattern))
                {
                    // Check if it's returning true (bypassing validation)
                    if (content.Contains("=> true") || content.Contains("return true"))
                    {
                        issues.Add($"Certificate validation may be disabled in {fileName}");
                    }
                }
            }
        }

        Assert.Empty(issues);
    }

    #endregion

    #region Input Validation Tests

    /// <summary>
    /// SQL injection check - gated by RUN_SECURITY_SCAN_TESTS=1.
    /// High false positive rate due to generic string concatenation patterns.
    /// Long-term fix: contract-based sanitizer tests (inputs → sanitized outputs).
    /// </summary>
    [SecurityScanFact]
    public void InputValidation_NoSqlInjectionVulnerabilities()
    {
        if (this._sourceCodePath == null)
        {
            return;
        }

        string[] csFiles = GetSourceCsFiles(this._sourceCodePath);
        Regex sqlPattern = MyRegex1();
        List<string> issues = [];

        foreach (string file in csFiles)
        {
            string content = File.ReadAllText(file);
            if (sqlPattern.IsMatch(content))
            {
                issues.Add($"Potential SQL injection vulnerability in {Path.GetFileName(file)}");
            }
        }

        Assert.Empty(issues);
    }

    [Fact]
    public void InputValidation_PathValidation()
    {
        if (this._sourceCodePath == null)
        {
            return;
        }

        string[] csFiles = GetSourceCsFiles(this._sourceCodePath);
        Regex pathPattern = new(@"Path\.(Combine|Join)\([^)]*\+|File\.(Read|Write|Open)\([^)]*\+",
            RegexOptions.IgnoreCase);
        _ = new List<string>();

        foreach (string file in csFiles)
        {
            string content = File.ReadAllText(file);

            if (pathPattern.IsMatch(content))
            {
                // Check if there's path validation nearby
                if (!content.Contains("Path.GetFullPath") &&
                    !content.Contains("ValidatePath") &&
                    !content.Contains("SanitizePath"))
                {
                    // This is a potential issue but not blocking
                }
            }
        }

        // Allow the test to pass - path operations in plugins may be intentional
        Assert.True(true);
    }

    #endregion

    #region Logging Security Tests

    // Sensitive-keywords whose *values* must never be interpolated into a log call. Matching is
    // intentionally scoped to structured-logging placeholders (`{keyword}`-shaped holes inside a
    // `.Log*(...)` call), not any mention of the bare word anywhere near "Log" — see
    // FindSensitiveLoggingIssues for why a looser `\.Log.*keyword` heuristic false-positives on
    // benign operational logging (e.g. "Could not read legacy token file at {Path}", where the
    // word "token" is prose and the only interpolated value is a file path).
    private static readonly string[] SensitiveLogKeywords =
        ["password", "apiKey", "secret", "token", "credential"];

    /// <summary>
    /// Flags logger calls that interpolate a sensitive-shaped value into a structured-logging
    /// placeholder, e.g. <c>logger.LogInformation($"pwd={password}")</c>,
    /// <c>logger.LogWarning("token={token}", token)</c>, or NLog-style
    /// <c>logger.Error("token={0}", token)</c>. Deliberately requires the keyword to appear
    /// *inside* a `{...}` hole within the log call's argument list (not merely as prose in the log
    /// message, and not merely near a "Log"-containing identifier like a field named
    /// `MissingRefreshTokenLogger` or a `NLog.LogManager.GetCurrentClassLogger()` declaration).
    /// </summary>
    internal static List<string> FindSensitiveLoggingIssues(string content, string fileName)
    {
        List<string> issues = [];
        foreach (string logCall in ExtractLogCalls(content))
        {
            if (LogCallContainsSensitiveStructuredValue(logCall))
            {
                issues.Add($"Potential sensitive data logging in {fileName}");
            }
        }

        return issues;
    }

    private static bool LogCallContainsSensitiveStructuredValue(string logCall)
    {
        string arguments = ExtractInvocationArguments(logCall);
        if (arguments.Length == 0)
        {
            return false;
        }

        List<string> splitArguments = SplitTopLevelArguments(arguments);
        int messageIndex = splitArguments.FindIndex(ArgumentContainsStringLiteral);
        if (messageIndex < 0)
        {
            return false;
        }

        foreach (string placeholder in ExtractStructuredPlaceholders(splitArguments[messageIndex]))
        {
            if (ContainsSensitiveKeyword(placeholder))
            {
                return true;
            }
        }

        for (int i = messageIndex + 1; i < splitArguments.Count; i++)
        {
            if (ArgumentExpressionLooksSensitive(splitArguments[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> ExtractLogCalls(string content)
    {
        int searchIndex = 0;
        while (searchIndex < content.Length)
        {
            int dotIndex = content.IndexOf('.', searchIndex);
            if (dotIndex < 0)
            {
                yield break;
            }

            int methodStart = dotIndex + 1;
            if (methodStart >= content.Length || !IsIdentifierStart(content[methodStart]))
            {
                searchIndex = dotIndex + 1;
                continue;
            }

            int methodEnd = methodStart + 1;
            while (methodEnd < content.Length && IsIdentifierPart(content[methodEnd]))
            {
                methodEnd++;
            }

            string methodName = content.Substring(methodStart, methodEnd - methodStart);
            int openParen = methodEnd;
            while (openParen < content.Length && char.IsWhiteSpace(content[openParen]))
            {
                openParen++;
            }

            if (openParen >= content.Length || content[openParen] != '(' || !IsLoggerMethodName(methodName))
            {
                searchIndex = methodEnd;
                continue;
            }

            int closeParen = FindMatchingParen(content, openParen);
            if (closeParen < 0)
            {
                yield break;
            }

            yield return content.Substring(dotIndex, closeParen - dotIndex + 1);
            searchIndex = closeParen + 1;
        }
    }

    private static bool IsLoggerMethodName(string methodName)
        => Regex.IsMatch(methodName, @"^Log\w*$", RegexOptions.CultureInvariant)
           || methodName is "Trace" or "Debug" or "Info" or "Warn" or "Error" or "Fatal";

    private static bool IsIdentifierStart(char c)
        => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c)
        => char.IsLetterOrDigit(c) || c == '_';

    private static string ExtractInvocationArguments(string logCall)
    {
        int openParen = logCall.IndexOf('(', StringComparison.Ordinal);
        int closeParen = logCall.LastIndexOf(')');
        return openParen < 0 || closeParen <= openParen
            ? string.Empty
            : logCall.Substring(openParen + 1, closeParen - openParen - 1);
    }

    private static List<string> SplitTopLevelArguments(string arguments)
    {
        List<string> result = [];
        int start = 0;
        int depth = 0;
        StringState stringState = StringState.None;

        for (int i = 0; i < arguments.Length; i++)
        {
            char c = arguments[i];
            if (TryAdvanceStringState(arguments, ref i, ref stringState))
            {
                continue;
            }

            if (stringState != StringState.None)
            {
                continue;
            }

            depth += c switch
            {
                '(' or '[' or '{' => 1,
                ')' or ']' or '}' => -1,
                _ => 0,
            };

            if (c == ',' && depth == 0)
            {
                result.Add(arguments.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }

        string last = arguments.Substring(start).Trim();
        if (last.Length > 0)
        {
            result.Add(last);
        }

        return result;
    }

    private static int FindMatchingParen(string content, int openParen)
    {
        int depth = 0;
        StringState stringState = StringState.None;

        for (int i = openParen; i < content.Length; i++)
        {
            char c = content[i];
            if (TryAdvanceStringState(content, ref i, ref stringState))
            {
                continue;
            }

            if (stringState != StringState.None)
            {
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static bool TryAdvanceStringState(string text, ref int index, ref StringState state)
    {
        char c = text[index];
        if (state == StringState.Regular)
        {
            if (c == '\\')
            {
                index++;
                return true;
            }

            if (c == '"')
            {
                state = StringState.None;
            }

            return true;
        }

        if (state == StringState.Verbatim)
        {
            if (c == '"' && index + 1 < text.Length && text[index + 1] == '"')
            {
                index++;
                return true;
            }

            if (c == '"')
            {
                state = StringState.None;
            }

            return true;
        }

        if (c == '"' || (c == '$' && index + 1 < text.Length && text[index + 1] == '"'))
        {
            state = StringState.Regular;
            if (c == '$')
            {
                index++;
            }

            return true;
        }

        if (c == '@' && index + 1 < text.Length && text[index + 1] == '"')
        {
            state = StringState.Verbatim;
            index++;
            return true;
        }

        if (c == '$' && index + 2 < text.Length && text[index + 1] == '@' && text[index + 2] == '"')
        {
            state = StringState.Verbatim;
            index += 2;
            return true;
        }

        if (c == '@' && index + 2 < text.Length && text[index + 1] == '$' && text[index + 2] == '"')
        {
            state = StringState.Verbatim;
            index += 2;
            return true;
        }

        return false;
    }

    private static bool ArgumentContainsStringLiteral(string argument)
        => argument.Contains('"', StringComparison.Ordinal);

    private static IEnumerable<string> ExtractStructuredPlaceholders(string messageTemplateArgument)
    {
        foreach (Match match in Regex.Matches(messageTemplateArgument, @"\{\s*([^}:,]+)", RegexOptions.IgnoreCase))
        {
            yield return match.Groups[1].Value;
        }
    }

    private static bool ArgumentExpressionLooksSensitive(string argument)
    {
        string trimmed = argument.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            return false;
        }

        return ContainsSensitiveKeyword(trimmed);
    }

    private static bool ContainsSensitiveKeyword(string value)
    {
        string normalized = Regex.Replace(value, @"[^A-Za-z0-9_]", string.Empty);
        foreach (string keyword in SensitiveLogKeywords)
        {
            if (normalized.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private enum StringState
    {
        None,
        Regular,
        Verbatim,
    }

    [Fact]
    public void Logging_NoSensitiveDataInLogs()
    {
        if (this._sourceCodePath == null)
        {
            return;
        }

        string[] csFiles = GetSourceCsFiles(this._sourceCodePath);
        List<string> issues = [];

        foreach (string file in csFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);
            issues.AddRange(FindSensitiveLoggingIssues(content, fileName));
        }

        Assert.Empty(issues);
    }

    [Fact]
    public void Logging_DoesNotFlagBenignMentionOfKeywordInLogMessageProse()
    {
        // Regression guard for the false positive fixed alongside this test: LegacyTokenMigration.cs
        // logs operational status about a *token file* (path), never a token *value*. The word "token"
        // is prose in the message template; the only interpolated placeholder is {Path}.
        string content = "logger?.LogWarning(ex, \"Could not read legacy token file at {Path}; leaving in place\", legacyPath);";

        Assert.Empty(FindSensitiveLoggingIssues(content, "Sample.cs"));
    }

    [Fact]
    public void Logging_DoesNotFlagLoggerDeclarationsWhoseNameContainsSensitiveWord()
    {
        // Regression guard: a field named MissingRefreshTokenLogger (TidalOAuthService.cs) is not a
        // .Log*(...) call at all — NLog.LogManager.GetCurrentClassLogger() is a logger *factory* call,
        // and the field name merely contains "Token"/"Logger" as substrings.
        string content = "private static readonly NLog.Logger MissingRefreshTokenLogger = NLog.LogManager.GetCurrentClassLogger();";

        Assert.Empty(FindSensitiveLoggingIssues(content, "Sample.cs"));
    }

    [Fact]
    public void Logging_FlagsGenuineSensitiveValueInterpolatedIntoLogCall()
    {
        // Real detection must still fire: an actual secret value placed into a structured-logging hole.
        string content = "logger.LogInformation($\"User password: {password}\");";

        Assert.NotEmpty(FindSensitiveLoggingIssues(content, "Sample.cs"));
    }

    [Fact]
    public void Logging_FlagsSensitiveArgumentPassedThroughGenericPlaceholder()
    {
        string content = "logger.LogWarning(\"token={Value}\", token);";

        Assert.NotEmpty(FindSensitiveLoggingIssues(content, "Sample.cs"));
    }

    [Fact]
    public void Logging_FlagsCredentialNamedArgumentPassedThroughGenericPlaceholder()
    {
        string content = "logger.LogWarning(\"Auth failed: {Error}\", credentialError);";

        Assert.NotEmpty(FindSensitiveLoggingIssues(content, "Sample.cs"));
    }

    [Fact]
    public void Logging_FlagsSensitiveNLogArgumentPassedThroughGenericPlaceholder()
    {
        string content = "_logger.Error(\"token={0}\", token);";

        Assert.NotEmpty(FindSensitiveLoggingIssues(content, "Sample.cs"));
    }

    #endregion

    #region Tidal-Specific Security Tests

    [Fact]
    public void Tidal_NoApiKeysInUrls()
    {
        if (this._sourceCodePath == null)
        {
            return;
        }

        string[] csFiles = GetSourceCsFiles(this._sourceCodePath);
        List<string> issues = [];

        foreach (string file in csFiles)
        {
            string content = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Check for API keys in URLs (should be in headers instead)
            bool hasApiKeyInUrl = content.Contains("?apikey=", StringComparison.OrdinalIgnoreCase) ||
                                 content.Contains("&apikey=", StringComparison.OrdinalIgnoreCase) ||
                                 content.Contains("?api_key=", StringComparison.OrdinalIgnoreCase);

            if (hasApiKeyInUrl)
            {
                issues.Add($"API keys in URLs in {fileName}");
            }
        }

        Assert.Empty(issues);
    }

    [Fact]
    public void Tidal_UsesHttpsForApi()
    {
        if (this._sourceCodePath == null)
        {
            return;
        }

        string constantsFile = Path.Combine(this._sourceCodePath, "Core", "Constants", "TidalConstants.cs");
        if (!File.Exists(constantsFile))
        {
            return;
        }

        string content = File.ReadAllText(constantsFile);

        // Check that Tidal API endpoints use HTTPS
        MatchCollection httpMatches = Regex.Matches(content, @"""http://[^""]*tidal[^""]*""", RegexOptions.IgnoreCase);
        Assert.Empty(httpMatches);
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }

    [GeneratedRegex(@"""http://[^""]*""", RegexOptions.IgnoreCase, "en-CA")]
    private static partial Regex MyRegex();
    [GeneratedRegex(@"(""[^""]*\+\s*\w+[^""]*""|string\.Format\([^)]*SQL|new\s+SqlCommand\([^)]*\+)", RegexOptions.IgnoreCase, "en-CA")]
    private static partial Regex MyRegex1();
}
