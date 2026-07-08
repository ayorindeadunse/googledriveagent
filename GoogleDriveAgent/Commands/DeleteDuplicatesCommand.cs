using System.ComponentModel;
using GoogleDriveAgent.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace GoogleDriveAgent.Commands;

public enum KeepPolicy
{
    Oldest,
    Newest
}

public class DeleteDuplicatesSettings : DuplicateSettings
{
    [CommandOption("--keep <oldest|newest>")]
    [DefaultValue(KeepPolicy.Oldest)]
    [Description("Which copy in each duplicate set survives. Default: oldest (usually the original, before re-uploads).")]
    public KeepPolicy Keep { get; set; } = KeepPolicy.Oldest;

    [CommandOption("-y|--yes")]
    [Description("Skip the confirmation prompt (for scripted/repeat runs).")]
    public bool Yes { get; set; }
}

public class DeleteDuplicatesCommand : AsyncCommand<DeleteDuplicatesSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DeleteDuplicatesSettings settings, CancellationToken cancellationToken)
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
            AnsiConsole.MarkupLine("[yellow]No duplicates found. Nothing to delete.[/]");
            return 0;
        }

        if (settings.Limit is { } limit)
        {
            groups = groups.Take(limit).ToList();
        }

        var toTrash = new List<DriveFile>();
        foreach (var group in groups)
        {
            var survivorIndex = settings.Keep == KeepPolicy.Oldest ? 0 : group.Files.Count - 1;
            toTrash.AddRange(group.Files.Where((_, i) => i != survivorIndex));
        }

        DuplicatesCommand.RenderDuplicateTable(groups);

        var totalWasted = groups.Sum(g => g.WastedBytes);
        AnsiConsole.MarkupLine(
            $"Keeping the [bold]{settings.Keep.ToString().ToLowerInvariant()}[/] copy of each set. " +
            $"[bold red]{toTrash.Count}[/] file(s) across {groups.Count} set(s) will be moved to Trash, " +
            $"reclaiming [bold red]{Formatting.FormatSize(totalWasted)}[/].");

        if (!settings.Yes && !AnsiConsole.Confirm($"Trash these {toTrash.Count} duplicate file(s)?", defaultValue: false))
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled. No files were changed.[/]");
            return 0;
        }

        var succeeded = 0;
        List<TrashFailure> failures = new();

        await AnsiConsole.Progress().StartAsync(async ctx =>
        {
            var task = ctx.AddTask("Trashing duplicates", maxValue: toTrash.Count);
            (succeeded, failures) = await fileService.TrashFilesAsync(
                toTrash,
                (done, _) => task.Value = done,
                cancellationToken);
            task.Value = toTrash.Count;
        });

        AnsiConsole.MarkupLine($"[green]{succeeded}[/] file(s) moved to Trash.");

        if (failures.Count > 0)
        {
            AnsiConsole.MarkupLine($"[red]{failures.Count}[/] failed:");
            foreach (var failure in failures.Take(20))
            {
                AnsiConsole.MarkupLine($"  [red]x[/] {failure.Name.EscapeMarkup()} ({failure.Id}): {failure.Error.EscapeMarkup()}");
            }
        }

        return failures.Count > 0 ? 1 : 0;
    }
}
