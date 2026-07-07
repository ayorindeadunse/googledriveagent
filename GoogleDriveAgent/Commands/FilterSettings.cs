using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GoogleDriveAgent.Commands;

public class FilterSettings : CommandSettings
{
    [CommandOption("--audio")]
    [Description("Shorthand for files whose MIME type starts with audio/ (e.g. voice recordings).")]
    public bool Audio { get; set; }

    [CommandOption("--mime-type <MIME_TYPE>")]
    [Description("Match files whose MIME type contains this value, e.g. audio/mpeg or application/pdf.")]
    public string? MimeType { get; set; }

    [CommandOption("--ext <EXTENSION>")]
    [Description("Match files whose name ends with any of these extensions. Repeat the flag or comma-separate, e.g. --ext .mp3 --ext .wav or --ext .mp3,.wav.")]
    public string[]? Extension { get; set; }

    public string[] ExtensionList =>
        Extension is null
            ? []
            : Extension
                .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToArray();

    [CommandOption("--name-contains <TEXT>")]
    [Description("Match files whose name contains this text.")]
    public string? NameContains { get; set; }

    [CommandOption("--folder <NAME_OR_ID>")]
    [Description("Restrict to files inside this folder, given by name or Drive folder ID.")]
    public string? Folder { get; set; }

    [CommandOption("--older-than <DATE>")]
    [Description("Match files last modified before this date (yyyy-MM-dd).")]
    public string? OlderThan { get; set; }

    [CommandOption("--newer-than <DATE>")]
    [Description("Match files last modified after this date (yyyy-MM-dd).")]
    public string? NewerThan { get; set; }

    [CommandOption("--min-size <BYTES>")]
    [Description("Match files at least this many bytes.")]
    public long? MinSize { get; set; }

    [CommandOption("--max-size <BYTES>")]
    [Description("Match files at most this many bytes.")]
    public long? MaxSize { get; set; }

    [CommandOption("--query <DRIVE_QUERY>")]
    [Description("Raw Drive API search query, ANDed with any other filters. See: developers.google.com/drive/api/guides/search-files")]
    public string? RawQuery { get; set; }

    [CommandOption("--limit <N>")]
    [Description("Only act on the first N matches.")]
    public int? Limit { get; set; }

    [CommandOption("--largest")]
    [Description("Sort matches by size, largest first. Must be paired with --limit.")]
    public bool Largest { get; set; }

    [CommandOption("--credentials <PATH>")]
    [DefaultValue("credentials.json")]
    [Description("Path to the OAuth client credentials.json downloaded from Google Cloud Console.")]
    public string CredentialsPath { get; set; } = "credentials.json";

    [CommandOption("--token-store <PATH>")]
    [DefaultValue("token_store")]
    [Description("Directory where the cached OAuth token is stored between runs.")]
    public string TokenStorePath { get; set; } = "token_store";

    public override ValidationResult Validate()
    {
        if (!string.IsNullOrWhiteSpace(OlderThan) && !DateTime.TryParse(OlderThan, out _))
        {
            return ValidationResult.Error($"--older-than '{OlderThan}' is not a valid date. Use yyyy-MM-dd.");
        }

        if (!string.IsNullOrWhiteSpace(NewerThan) && !DateTime.TryParse(NewerThan, out _))
        {
            return ValidationResult.Error($"--newer-than '{NewerThan}' is not a valid date. Use yyyy-MM-dd.");
        }

        if (MinSize is < 0 || MaxSize is < 0)
        {
            return ValidationResult.Error("--min-size and --max-size must be non-negative.");
        }

        if (Largest && Limit is null)
        {
            return ValidationResult.Error("--largest must be paired with --limit N (e.g. --largest --limit 20) so it's clear how many files you mean.");
        }

        var hasFilter = Audio || !string.IsNullOrWhiteSpace(MimeType) || ExtensionList.Length > 0
            || !string.IsNullOrWhiteSpace(NameContains) || !string.IsNullOrWhiteSpace(Folder)
            || !string.IsNullOrWhiteSpace(OlderThan) || !string.IsNullOrWhiteSpace(NewerThan)
            || MinSize is not null || MaxSize is not null || !string.IsNullOrWhiteSpace(RawQuery) || Largest;

        if (!hasFilter)
        {
            return ValidationResult.Error(
                "Refusing to match every file in Drive. Pass at least one filter (--audio, --mime-type, --ext, " +
                "--name-contains, --folder, --older-than, --newer-than, --min-size, --max-size, --query, or --largest).");
        }

        return ValidationResult.Success();
    }
}
