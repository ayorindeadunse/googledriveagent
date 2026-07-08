using GoogleDriveAgent.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("gdrive-agent");

    config.AddCommand<ListFilesCommand>("list")
        .WithDescription("Search Google Drive for files matching a filter and preview them (no changes made).")
        .WithExample(["list", "--audio"])
        .WithExample(["list", "--ext", ".mp3", "--older-than", "2024-01-01"])
        .WithExample(["list", "--largest", "--limit", "20"]);

    config.AddCommand<DeleteFilesCommand>("delete")
        .WithDescription("Move matching Google Drive files to Trash, after showing a preview and asking for confirmation.")
        .WithExample(["delete", "--audio"])
        .WithExample(["delete", "--folder", "Voice Recorder", "--yes"])
        .WithExample(["delete", "--largest", "--limit", "20"]);

    config.AddCommand<UsageCommand>("usage")
        .WithDescription("Show total Drive storage usage, broken down by active files vs. Trash.")
        .WithExample(["usage"]);

    config.AddCommand<DuplicatesCommand>("duplicates")
        .WithDescription("Find files with identical content across Drive and report how much space they waste (no changes made).")
        .WithExample(["duplicates"])
        .WithExample(["duplicates", "--folder", "Photos", "--min-size", "1000000"]);

    config.AddCommand<DeleteDuplicatesCommand>("delete-duplicates")
        .WithDescription("Keep one copy of each duplicate file and move the rest to Trash, after showing a preview and asking for confirmation.")
        .WithExample(["delete-duplicates"])
        .WithExample(["delete-duplicates", "--keep", "newest", "--yes"]);
});

return await app.RunAsync(args);
