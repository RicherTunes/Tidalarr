namespace Tidalarr.Domain.Streaming;

public sealed class DefaultAudioFormatHandler : IAudioFormatHandler
{
    public Task<string> ProcessAudioFileAsync(
        string inputPath,
        string codecs,
        bool extractFlac = true,
        bool keepOriginal = false,
        IAudioProcessor? audio = null)
    {
        return AudioFormatHandler.ProcessAudioFileAsync(
            inputPath,
            codecs,
            extractFlac: extractFlac,
            keepOriginal: keepOriginal,
            audio: audio);
    }
}

