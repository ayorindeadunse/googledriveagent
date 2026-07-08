using System.ComponentModel;
using GoogleDriveAgent.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GoogleDriveAgent.Commands;

public class UsageSettings : CommandSettings
{
    [CommandOption("--credentials <PATH>")]
    [DefaultValue("credentials.json")]
    [Description("Path to the OAuth client credentials.json downloaded from Google Cloud Console.")]
    public string CredentialsPath { get; set; } = "credentials.json";

    [CommandOption("--token-store <PATH>")]
    [DefaultValue("token_store")]
    [Description("Directory where the cached OAuth token is stored between runs.")]
    public string TokenStorePath { get; set; } = "token_store";
}

public class UsageCommand : AsyncCommand<UsageSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, UsageSettings settings, CancellationToken cancellationToken)
    {
        using var drive = await DriveAuthService.AuthenticateAsync(settings.CredentialsPath, settings.TokenStorePath, cancellationToken);

        var request = drive.About.Get();
        request.Fields = "storageQuota";
        var about = await request.ExecuteAsync(cancellationToken);
        var quota = about.StorageQuota;

        var limit = quota.Limit;
        var usage = quota.Usage ?? 0;
        var usageInDrive = quota.UsageInDrive ?? 0;
        var usageInTrash = quota.UsageInDriveTrash ?? 0;

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Metric");
        table.AddColumn("Size");

        table.AddRow("Total used (all Google services)", Formatting.FormatSize(usage));
        table.AddRow("  of which: active Drive files", Formatting.FormatSize(usageInDrive));
        table.AddRow("  of which: sitting in Trash", Formatting.FormatSize(usageInTrash));
        table.AddRow("Storage limit", limit is null ? "unlimited" : Formatting.FormatSize(limit));

        AnsiConsole.Write(table);

        if (usageInTrash > 0)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Note:[/] Trash still counts against your quota. {Formatting.FormatSize(usageInTrash)} is " +
                "sitting in Trash right now — empty it in Drive (or via Drive's \"Empty trash\") to actually reclaim that space.");
        }

        return 0;
    }
}
