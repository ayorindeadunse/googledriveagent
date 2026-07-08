using GoogleDriveAgent.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace GoogleDriveAgent.Commands;

public class DuplicatesCommand : AsyncCommand<DuplicateSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DuplicateSettings settings, CancellationToken cancellationToken)
    {
        using var drive = await DriveAuthService.AuthenticateAsync(settings.CredentialsPath, settings.TokenStorePath, cancellationToken);
        var fileService = new DriveFileService(drive);

        string? folderId = null;
        if (!string.IsNullOrWhiteSpace(settings.Folder))
        {
            folderId = await fileService.ResolveFolderIdAsync(settings.Folder, cancellationToken);
        }

        var query = fileService.BuildDuplicateScanQuery(folderId);
        AnsiConsole.MarkupLine($"[grey]Query:[/] {query.EscapeMarkup()}");

        List<DriveFile> allFiles = null!;
        await AnsiConsole.Status().StartAsync(
            "Scanning Drive for duplicates (reads every file's checksum, can take a while on a large Drive)...",
            async _ => { allFiles = await fileService.ListAllForDuplicateScanAsync(query, cancellationToken); });

        var groups = DriveFileService.FindDuplicateGroups(allFiles, settings.MinSize);

        if (groups.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No duplicates found.[/]");
            return 0;
        }

        var totalWasted = groups.Sum(g => g.WastedBytes);
        var shown = settings.Limit is { } limit ? groups.Take(limit).ToList() : groups;

        RenderDuplicateTable(shown);

        if (shown.Count < groups.Count)
        {
            AnsiConsole.MarkupLine($"[grey](showing the {shown.Count} largest of {groups.Count} duplicate sets by wasted space; raise --limit to see more)[/]");
        }

        AnsiConsole.MarkupLine(
            $"[bold]{groups.Count}[/] duplicate set(s) found, [bold red]{Formatting.FormatSize(totalWasted)}[/] reclaimable " +
            "by keeping one copy of each (see delete-duplicates).");

        return 0;
    }

    internal static void RenderDuplicateTable(List<DuplicateGroup> groups)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Copies");
        table.AddColumn("Each");
        table.AddColumn("Wasted");
        table.AddColumn("Name(s)");

        foreach (var group in groups)
        {
            var names = string.Join(", ", group.Files.Select(f => f.Name ?? "(unnamed)").Distinct());
            table.AddRow(
                group.Count.ToString(),
                Formatting.FormatSize(group.FileSize),
                Formatting.FormatSize(group.WastedBytes),
                names.EscapeMarkup());
        }

        AnsiConsole.Write(table);
    }
}
