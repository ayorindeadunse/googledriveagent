using System.ComponentModel;
using GoogleDriveAgent.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace GoogleDriveAgent.Commands;

public class DeleteFilesSettings : FilterSettings
{
    [CommandOption("-y|--yes")]
    [Description("Skip the confirmation prompt (for scripted/repeat runs).")]
    public bool Yes { get; set; }
}

public class DeleteFilesCommand : AsyncCommand<DeleteFilesSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, DeleteFilesSettings settings, CancellationToken cancellationToken)
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
            AnsiConsole.MarkupLine("[yellow]No files matched. Nothing to delete.[/]");
            return 0;
        }

        ListFilesCommand.RenderMatchTable(matches, previewLimit: 25);
        AnsiConsole.MarkupLine($"[bold red]{matches.Count}[/] file(s) will be moved to Trash (recoverable from Drive's Trash).");

        if (!settings.Yes && !AnsiConsole.Confirm($"Trash these {matches.Count} file(s)?", defaultValue: false))
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled. No files were changed.[/]");
            return 0;
        }

        var succeeded = 0;
        List<TrashFailure> failures = new();

        await AnsiConsole.Progress().StartAsync(async ctx =>
        {
            var task = ctx.AddTask("Trashing files", maxValue: matches.Count);
            (succeeded, failures) = await fileService.TrashFilesAsync(
                matches,
                (done, _) => task.Value = done,
                cancellationToken);
            task.Value = matches.Count;
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
