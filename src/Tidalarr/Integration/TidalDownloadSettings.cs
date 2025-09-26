using System;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using Lidarr.Plugin.Common.Base;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration;

public class TidalDownloadSettings : BaseStreamingSettings, IProviderConfig
{
    private static readonly TidalDownloadSettingsValidator Validator = new();

    [FieldDefinition(0, Label = "Preferred Quality", Type = FieldType.Select, SelectOptions = typeof(TidalQuality), HelpText = "Audio quality requested from Tidal.")]
    public TidalQuality PreferredQuality { get; set; } = TidalQuality.Lossless;

    [FieldDefinition(1, Label = "Download Path", Type = FieldType.Path, HelpText = "Destination folder for downloaded albums.")]
    public string DownloadPath { get; set; } = string.Empty;

    [FieldDefinition(2, Label = "Include MQA Masters", Type = FieldType.Checkbox, Advanced = true, HelpText = "Allow Master (MQA) releases when available.")]
    public bool IncludeMqa { get; set; } = true;

    [FieldDefinition(3, Label = "Extract FLAC from M4A", Type = FieldType.Checkbox, Advanced = true, HelpText = "Convert M4A containers to FLAC when possible.")]
    public bool ExtractFlac { get; set; } = true;

    [FieldDefinition(4, Label = "Re-encode AAC Streams", Type = FieldType.Checkbox, Advanced = true, HelpText = "Transcode AAC streams to 320kbps AAC when HiRes/Lossless are unavailable.")]
    public bool ReEncodeAAC { get; set; } = false;

    [FieldDefinition(5, Label = "Save Synced Lyrics", Type = FieldType.Checkbox, Advanced = true)]
    public bool SaveSyncedLyrics { get; set; } = true;

    [FieldDefinition(6, Label = "Use LRCLIB for Lyrics", Type = FieldType.Checkbox, Advanced = true, HelpText = "Fallback to LRCLIB when Tidal does not provide synced lyrics.")]
    public bool UseLRCLIB { get; set; } = false;

    [FieldDefinition(7, Label = "Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true, HelpText = "Delay between chunk requests used for throttling.")]
    public int DownloadDelay { get; set; } = 1000;

    [FieldDefinition(8, Label = "Min Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true)]
    public int DownloadDelayMin { get; set; } = 500;

    [FieldDefinition(9, Label = "Max Chunk Delay", Type = FieldType.Number, Unit = "ms", Advanced = true)]
    public int DownloadDelayMax { get; set; } = 2000;

    public override bool IsValid(out string errorMessage)
    {
        var validation = Validate();
        errorMessage = validation.IsValid ? string.Empty : validation.Errors.First().ErrorMessage;
        return validation.IsValid;
    }

    public NzbDroneValidationResult Validate()
    {
        return new NzbDroneValidationResult(Validator.Validate(this));
    }

    private sealed class TidalDownloadSettingsValidator : AbstractValidator<TidalDownloadSettings>
    {
        public TidalDownloadSettingsValidator()
        {
            RuleFor(x => x.DownloadPath)
                .NotEmpty().WithMessage("Download path is required")
                .IsValidPath();

            RuleFor(x => x.DownloadDelay)
                .InclusiveBetween(0, 60000)
                .WithMessage("Chunk delay must be between 0 and 60000 milliseconds");

            RuleFor(x => x.DownloadDelayMin)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(x => x.DownloadDelayMax)
                .WithMessage("Minimum delay must be less than or equal to maximum delay");

            RuleFor(x => x.DownloadDelayMax)
                .GreaterThanOrEqualTo(x => x.DownloadDelayMin)
                .LessThanOrEqualTo(60000)
                .WithMessage("Maximum delay must be between min delay and 60000 milliseconds");
        }
    }
}
