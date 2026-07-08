using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GoogleDriveAgent.Commands;

public class DuplicateSettings : CommandSettings
{
    [CommandOption("--folder <NAME_OR_ID>")]
    [Description("Restrict the scan to files inside this folder, given by name or Drive folder ID.")]
    public string? Folder { get; set; }

    [CommandOption("--min-size <BYTES>")]
    [Description("Ignore duplicate sets smaller than this many bytes (reduces noise from tiny files).")]
    public long? MinSize { get; set; }

    [CommandOption("--limit <N>")]
    [Description("Only show/act on the first N duplicate sets, sorted by wasted space (largest first).")]
    public int? Limit { get; set; }

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
        if (MinSize is < 0)
        {
            return ValidationResult.Error("--min-size must be non-negative.");
        }

        if (Limit is < 1)
        {
            return ValidationResult.Error("--limit must be at least 1.");
        }

        return ValidationResult.Success();
    }
}
