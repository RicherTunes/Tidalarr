namespace Tidalarr.Domain.Streaming;

public interface IAudioFormatHandler
{
    Task<string> ProcessAudioFileAsync(
        string inputPath,
        string codecs,
        bool extractFlac = true,
        bool keepOriginal = false,
        IAudioProcessor? audio = null);
}

