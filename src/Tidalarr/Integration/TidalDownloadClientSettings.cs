using System;
using System.Linq;
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

    [FieldDefinition(SettingsDisplay.Download.PreferredQualityOrder, Label = SettingsDisplay.Download.PreferredQualityLabel, Type = FieldType.Select, SelectOptions = typeof(TidalQuality), HelpText = "Audio quality requested from Tidal.")]
    public TidalQuality PreferredQuality { get; set; } = TidalQuality.Lossless;

    [FieldDefinition(SettingsDisplay.Download.DownloadPathOrder, Label = SettingsDisplay.Download.DownloadPathLabel, Type = FieldType.Path, HelpText = "Destination folder for downloaded albums.")]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(SettingsDisplay.Download.IncludeMqaOrder, Label = SettingsDisplay.Download.IncludeMqaLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = "Allow Master (MQA) releases when available.")]
    public bool IncludeMqa { get; set; } = true;

    [FieldDefinition(SettingsDisplay.Download.ExtractFlacOrder, Label = SettingsDisplay.Download.ExtractFlacLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = "Convert M4A containers to FLAC when possible.")]
    public bool ExtractFlac { get; set; } = true;

    [FieldDefinition(SettingsDisplay.Download.ReEncodeAACOrder, Label = SettingsDisplay.Download.ReEncodeAACLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = "Transcode AAC streams to 320kbps AAC when HiRes/Lossless are unavailable.")]
    public bool ReEncodeAAC { get; set; } = false;

    [FieldDefinition(SettingsDisplay.Download.SaveSyncedLyricsOrder, Label = SettingsDisplay.Download.SaveSyncedLyricsLabel, Type = FieldType.Checkbox, Advanced = true)]
    public bool SaveSyncedLyrics { get; set; } = true;

    [FieldDefinition(SettingsDisplay.Download.UseLrclibOrder, Label = SettingsDisplay.Download.UseLrclibLabel, Type = FieldType.Checkbox, Advanced = true, HelpText = "Fallback to LRCLIB when Tidal does not provide synced lyrics.")]
    public bool UseLRCLIB { get; set; } = false;

    [FieldDefinition(SettingsDisplay.Download.ChunkDelayOrder, Label = SettingsDisplay.Download.ChunkDelayLabel, Type = FieldType.Number, Unit = SettingsDisplay.Download.ChunkDelayUnit, Advanced = true, HelpText = "Delay between chunk requests used for throttling.")]
    public int DownloadDelay { get; set; } = 1000;

    [FieldDefinition(SettingsDisplay.Download.ChunkDelayMinOrder, Label = SettingsDisplay.Download.ChunkDelayMinLabel, Type = FieldType.Number, Unit = SettingsDisplay.Download.ChunkDelayMinUnit, Advanced = true)]
    public int DownloadDelayMin { get; set; } = 500;

    [FieldDefinition(SettingsDisplay.Download.ChunkDelayMaxOrder, Label = SettingsDisplay.Download.ChunkDelayMaxLabel, Type = FieldType.Number, Unit = SettingsDisplay.Download.ChunkDelayMaxUnit, Advanced = true)]
    public int DownloadDelayMax { get; set; } = 2000;

    public override string BaseUrl { get; set; } = "https://api.tidal.com";

    public override bool IsValid(out string errorMessage)
    {
        var validation = Validator.Validate(this);
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
            RuleFor(x => x.PreferredQuality)
                .Must(quality => Enum.IsDefined(typeof(TidalQuality), quality))
                .WithMessage("Preferred quality selection is invalid")
                .WithErrorCode(TidalarrValidationCodes.PreferredQualityInvalid);

            RuleFor(x => x.DownloadPath)
                .NotEmpty().WithMessage("Download path is required").WithErrorCode(TidalarrValidationCodes.DownloadPathRequired)
                .Must(PathValidationExtensions.IsReasonablePath).WithMessage("Download path is invalid").WithErrorCode(TidalarrValidationCodes.DownloadPathInvalid);

            RuleFor(x => x.DownloadDelay)
                .InclusiveBetween(0, 60000)
                .WithMessage("Chunk delay must be between 0 and 60000 milliseconds")
                .WithErrorCode(TidalarrValidationCodes.DownloadDelayRange);

            RuleFor(x => x.DownloadDelayMin)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Minimum delay must be greater than or equal to 0 milliseconds")
                .WithErrorCode(TidalarrValidationCodes.DownloadDelayMinRange)
                .LessThanOrEqualTo(x => x.DownloadDelayMax)
                .WithMessage("Minimum delay must be less than or equal to maximum delay")
                .WithErrorCode(TidalarrValidationCodes.DownloadDelayMinRange);

            RuleFor(x => x.DownloadDelayMax)
                .GreaterThanOrEqualTo(x => x.DownloadDelayMin)
                .WithMessage("Maximum delay must be greater than or equal to minimum delay")
                .WithErrorCode(TidalarrValidationCodes.DownloadDelayMaxRange)
                .LessThanOrEqualTo(60000)
                .WithMessage("Maximum delay must be between min delay and 60000 milliseconds")
                .WithErrorCode(TidalarrValidationCodes.DownloadDelayMaxRange);
        }
    }
}
