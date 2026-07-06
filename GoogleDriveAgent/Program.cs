using GoogleDriveAgent.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("gdrive-agent");

    config.AddCommand<ListFilesCommand>("list")
        .WithDescription("Search Google Drive for files matching a filter and preview them (no changes made).")
        .WithExample(["list", "--audio"])
        .WithExample(["list", "--ext", ".mp3", "--older-than", "2024-01-01"]);

    config.AddCommand<DeleteFilesCommand>("delete")
        .WithDescription("Move matching Google Drive files to Trash, after showing a preview and asking for confirmation.")
        .WithExample(["delete", "--audio"])
        .WithExample(["delete", "--folder", "Voice Recorder", "--yes"]);
});

return await app.RunAsync(args);
