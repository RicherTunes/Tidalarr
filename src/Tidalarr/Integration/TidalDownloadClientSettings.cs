using FluentValidation;
using FluentValidation.Results;
using Lidarr.Plugin.Common.Base;
using Tidalarr.Core.Models;
using FieldDefinition = Tidalarr.Integration.Annotations.FieldDefinitionAttribute;
using FieldType = Tidalarr.Integration.Annotations.FieldType;

namespace Tidalarr.Integration;

public class TidalDownloadClientSettings : BaseStreamingSettings
{
    private static readonly TidalDownloadClientSettingsValidator Validator = new();
    internal const int MaxCombinedDownloadConcurrency = 6;

    [FieldDefinition(SettingsDisplay.Download.PreferredQualityOrder, Label = SettingsDisplay.Download.PreferredQualityLabel, Type = FieldType.Select, SelectOptions = typeof(TidalQuality), HelpText = SettingsDisplay.Download.PreferredQualityHelpText)]
    public TidalQuality PreferredQuality { get; set; } = TidalQuality.Lossless;

    [FieldDefinition(SettingsDisplay.Download.DownloadPathOrder, Label = SettingsDisplay.Download.DownloadPathLabel, Type = FieldType.Path, HelpText = SettingsDisplay.Download.DownloadPathHelpText)]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(SettingsDisplay.Download.IncludeMqaOrder, Label = SettingsDisplay.Download.IncludeMqaLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = SettingsDisplay.Download.IncludeMqaHelpText)]
    public bool IncludeMqa { get; set; } = true;

    [FieldDefinition(SettingsDisplay.Download.ExtractFlacOrder, Label = SettingsDisplay.Download.ExtractFlacLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = SettingsDisplay.Download.ExtractFlacHelpText)]
    public bool ExtractFlac { get; set; } = true;

    [FieldDefinition(SettingsDisplay.Download.ReEncodeAACOrder, Label = SettingsDisplay.Download.ReEncodeAACLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = SettingsDisplay.Download.ReEncodeAACHelpText)]
    public bool ReEncodeAAC { get; set; } = false;

    [FieldDefinition(SettingsDisplay.Download.SaveSyncedLyricsOrder, Label = SettingsDisplay.Download.SaveSyncedLyricsLabel, Type = FieldType.Checkbox, Advanced = true)]
    public bool SaveSyncedLyrics { get; set; } = true;

    [FieldDefinition(SettingsDisplay.Download.UseLrclibOrder, Label = SettingsDisplay.Download.UseLrclibLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = SettingsDisplay.Download.UseLrclibHelpText)]
    public bool UseLRCLIB { get; set; } = false;

    [FieldDefinition(SettingsDisplay.Download.ChunkDelayOrder, Label = SettingsDisplay.Download.ChunkDelayLabel, Type = FieldType.Number, Unit = SettingsDisplay.Download.ChunkDelayUnit, Advanced = true, HelpText = SettingsDisplay.Download.ChunkDelayHelpText)]
    public int DownloadDelay { get; set; } = 0;

    [FieldDefinition(SettingsDisplay.Download.MaxConcurrentTrackDownloadsOrder, Label = SettingsDisplay.Download.MaxConcurrentTrackDownloadsLabel, Type = FieldType.Number, Advanced = true, HelpText = SettingsDisplay.Download.MaxConcurrentTrackDownloadsHelpText)]
    public int MaxConcurrentTrackDownloads { get; set; } = 2;

    [FieldDefinition(SettingsDisplay.Download.MaxConcurrentChunkDownloadsOrder, Label = SettingsDisplay.Download.MaxConcurrentChunkDownloadsLabel, Type = FieldType.Number, Advanced = true, HelpText = SettingsDisplay.Download.MaxConcurrentChunkDownloadsHelpText)]
    public int MaxConcurrentChunkDownloads { get; set; } = 2;

    internal int GetEffectiveMaxConcurrentChunkDownloads()
    {
        int tracks = Math.Max(1, MaxConcurrentTrackDownloads);
        int chunks = Math.Max(1, MaxConcurrentChunkDownloads);

        return tracks * chunks <= MaxCombinedDownloadConcurrency ? chunks : Math.Max(1, MaxCombinedDownloadConcurrency / tracks);
    }

    public override string BaseUrl { get; set; } = "https://api.tidal.com";

    public override bool IsValid(out string errorMessage)
    {
        ValidationResult validation = Validator.Validate(this);
        errorMessage = validation.IsValid ? string.Empty : validation.Errors.First().ErrorMessage;
        return validation.IsValid;
    }

    public ValidationResult ValidateFluent()
    {
        return Validator.Validate(this);
    }

    private sealed class TidalDownloadClientSettingsValidator : AbstractValidator<TidalDownloadClientSettings>
    {
        public TidalDownloadClientSettingsValidator()
        {
            _ = RuleFor(x => x.PreferredQuality)
                .Must(quality => Enum.IsDefined(typeof(TidalQuality), quality))
                .WithMessage("Preferred quality selection is invalid")
                .WithErrorCode(TidalarrValidationCodes.PreferredQualityInvalid);

            _ = RuleFor(x => x.DownloadPath)
                .NotEmpty().WithMessage("Download path is required").WithErrorCode(TidalarrValidationCodes.DownloadPathRequired)
                .Must(PathValidationExtensions.IsReasonablePath).WithMessage("Download path is invalid").WithErrorCode(TidalarrValidationCodes.DownloadPathInvalid);

            _ = RuleFor(x => x.DownloadDelay)
                .InclusiveBetween(0, 60000)
                .WithMessage("Chunk delay must be between 0 and 60000 milliseconds")
                .WithErrorCode(TidalarrValidationCodes.DownloadDelayRange);

            _ = RuleFor(x => x.MaxConcurrentTrackDownloads)
                .InclusiveBetween(1, 3)
                .WithMessage("Max concurrent track downloads must be between 1 and 3")
                .WithErrorCode(TidalarrValidationCodes.MaxConcurrentTrackDownloadsRange);

            _ = RuleFor(x => x.MaxConcurrentChunkDownloads)
                .InclusiveBetween(1, 8)
                .WithMessage("Max concurrent chunk downloads must be between 1 and 8")
                .WithErrorCode(TidalarrValidationCodes.MaxConcurrentChunkDownloadsRange);
        }
    }
}
