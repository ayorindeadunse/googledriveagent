using GoogleDriveAgent.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace GoogleDriveAgent.Commands;

public class ListFilesCommand : AsyncCommand<FilterSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, FilterSettings settings, CancellationToken cancellationToken)
    {
        using var drive = await DriveAuthService.AuthenticateAsync(settings.CredentialsPath, settings.TokenStorePath, cancellationToken);
        var fileService = new DriveFileService(drive);

        string? folderId = null;
        if (!string.IsNullOrWhiteSpace(settings.Folder))
        {
            folderId = await fileService.ResolveFolderIdAsync(settings.Folder, cancellationToken);
        }

        var query = fileService.BuildQuery(settings, folderId);
        AnsiConsole.MarkupLine($"[grey]Query:[/] {query.EscapeMarkup()}");

        List<DriveFile> matches = null!;
        await AnsiConsole.Status().StartAsync("Searching Drive...", async _ =>
        {
            matches = await fileService.ListMatchesAsync(settings, query, cancellationToken);
        });

        if (matches.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No files matched.[/]");
            return 0;
        }

        RenderMatchTable(matches, previewLimit: 50);
        AnsiConsole.MarkupLine($"[bold]{matches.Count}[/] file(s) matched.");

        return 0;
    }

    internal static void RenderMatchTable(List<DriveFile> matches, int previewLimit)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Name");
        table.AddColumn("Type");
        table.AddColumn("Size");
        table.AddColumn("Modified");

        foreach (var file in matches.Take(previewLimit))
        {
            table.AddRow(
                (file.Name ?? "(unnamed)").EscapeMarkup(),
                (file.MimeType ?? "-").EscapeMarkup(),
                Formatting.FormatSize(file.Size),
                file.ModifiedTimeDateTimeOffset?.ToString("yyyy-MM-dd") ?? "-");
        }

        AnsiConsole.Write(table);

        if (matches.Count > previewLimit)
        {
            AnsiConsole.MarkupLine($"[grey]...and {matches.Count - previewLimit} more.[/]");
        }
    }
}
