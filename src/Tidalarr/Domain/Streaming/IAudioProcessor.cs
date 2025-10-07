using System.Diagnostics;

namespace Tidalarr.Domain.Streaming;

public interface IAudioProcessor
{
    Task<(int exitCode, string stdout, string stderr)> RunFfmpegAsync(string arguments, CancellationToken ct = default);
    (int exitCode, string stdout, string stderr) RunFfprobe(string arguments);
}

public class SystemAudioProcessor : IAudioProcessor
{
    public async Task<(int exitCode, string stdout, string stderr)> RunFfmpegAsync(string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p == null) return (-1, string.Empty, "failed to start ffmpeg");
        var stdOutTask = p.StandardOutput.ReadToEndAsync();
        var stdErrTask = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, await stdOutTask, await stdErrTask);
    }

    public (int exitCode, string stdout, string stderr) RunFfprobe(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffprobe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p == null) return (-1, string.Empty, "failed to start ffprobe");
        p.WaitForExit();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        return (p.ExitCode, stdout, stderr);
    }
}


