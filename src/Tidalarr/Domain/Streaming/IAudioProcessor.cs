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
        ProcessStartInfo psi = new()
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using Process? p = Process.Start(psi);
        if (p == null)
        {
            return (-1, string.Empty, "failed to start ffmpeg");
        }

        Task<string> stdOutTask = p.StandardOutput.ReadToEndAsync();
        Task<string> stdErrTask = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, await stdOutTask, await stdErrTask);
    }

    public (int exitCode, string stdout, string stderr) RunFfprobe(string arguments)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "ffprobe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using Process? p = Process.Start(psi);
        if (p == null)
        {
            return (-1, string.Empty, "failed to start ffprobe");
        }

        p.WaitForExit();
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        return (p.ExitCode, stdout, stderr);
    }
}


