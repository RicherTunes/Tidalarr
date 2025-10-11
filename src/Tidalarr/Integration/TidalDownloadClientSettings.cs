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

    [FieldDefinition(20, Label = "Preferred Quality", Type = FieldType.Select, SelectOptions = typeof(TidalQuality), HelpText = "Audio quality requested from Tidal.")]
    public TidalQuality PreferredQuality { get; set; } = TidalQuality.Lossless;

    [FieldDefinition(21, Label = "Download Path", Type = FieldType.Path, HelpText = "Destination folder for downloaded albums.")]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(22, Label = "Include MQA Masters", Type = FieldType.Checkbox, Advanced = true, HelpText = "Allow Master (MQA) releases when available.")]
    public bool IncludeMqa { get; set; } = true;

    [FieldDefinition(23, Label = "Extract FLAC from M4A", Type = FieldType.Checkbox, Advanced = true, HelpText = "Convert M4A containers to FLAC when possible.")]
    public bool ExtractFlac { get; set; } = true;

    [FieldDefinition(24, Label = "Re-encode AAC Streams", Type = FieldType.Checkbox, Advanced = true, HelpText = "Transcode AAC streams to 320kbps AAC when HiRes/Lossless are unavailable.")]
    public bool ReEncodeAAC { get; set; } = false;

    [FieldDefinition(25, Label = "Save Synced Lyrics", Type = FieldType.Checkbox, Advanced = true)]
    public bool SaveSyncedLyrics { get; set; } = true;

    [FieldDefinition(26, Label = "Use LRCLIB for Lyrics", Type = FieldType.Checkbox, Advanced = true, HelpText = "Fallback to LRCLIB when Tidal does not provide synced lyrics.")]
    public bool UseLRCLIB { get; set; } = false;

    [FieldDefinition(27, Label = "Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true, HelpText = "Delay between chunk requests used for throttling.")]
    public int DownloadDelay { get; set; } = 1000;

    [FieldDefinition(28, Label = "Min Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true)]
    public int DownloadDelayMin { get; set; } = 500;

    [FieldDefinition(29, Label = "Max Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true)]
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
