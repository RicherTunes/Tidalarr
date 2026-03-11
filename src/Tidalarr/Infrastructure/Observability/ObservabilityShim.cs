using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Tidalarr.Infrastructure.Observability;

internal static class ObservabilityShim
{
    private static bool IsEnabled => string.Equals(Environment.GetEnvironmentVariable("TIDALARR_OBS"), "1", StringComparison.Ordinal);

    public static IDisposable StartApi(ILogger logger, string service, string endpoint, string? correlationId = null)
    {
        if (!IsEnabled || logger == null)
        {
            return NoopDisposable.Instance;
        }

        try
        {
            Type? extType = Type.GetType("Lidarr.Plugin.Common.Observability.LoggerExtensions, Lidarr.Plugin.Common");
            if (extType == null)
            {
                return NoopDisposable.Instance;
            }

            MethodInfo? method = extType.GetMethod("LogApiCallStarted", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                return NoopDisposable.Instance;
            }

            IDisposable? disp = method.Invoke(null, [logger, service, endpoint, correlationId]) as IDisposable;
            return disp ?? NoopDisposable.Instance;
        }
        catch
        {
            return NoopDisposable.Instance;
        }
    }

    public static void CompleteApi(ILogger logger, string service, string endpoint, int statusCode, bool success, TimeSpan duration)
    {
        if (!IsEnabled || logger == null)
        {
            return;
        }

        try
        {
            Type? extType = Type.GetType("Lidarr.Plugin.Common.Observability.LoggerExtensions, Lidarr.Plugin.Common");
            if (extType == null)
            {
                return;
            }

            MethodInfo? method = extType.GetMethod("LogApiCallCompleted", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                return;
            }

            _ = method.Invoke(null, [logger, service, endpoint, statusCode, success, duration]);
        }
        catch
        {
            // swallow
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose() { }
    }
}

