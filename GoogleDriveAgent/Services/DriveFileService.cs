using Google.Apis.Drive.v3;
using Google.Apis.Requests;
using GoogleDriveAgent.Commands;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace GoogleDriveAgent.Services;

public record TrashFailure(string Id, string Name, string Error);

public class DriveFileService
{
    private const string FileFields = "nextPageToken, files(id, name, mimeType, size, modifiedTime)";
    private const int BatchSize = 100;

    private readonly DriveService _drive;

    public DriveFileService(DriveService drive)
    {
        _drive = drive;
    }

    public async Task<string> ResolveFolderIdAsync(string folder, CancellationToken ct)
    {
        var request = _drive.Files.List();
        request.Q = $"name = '{EscapeForQuery(folder)}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
        request.Fields = "files(id, name)";
        request.PageSize = 10;

        var result = await request.ExecuteAsync(ct);
        if (result.Files is { Count: > 0 })
        {
            return result.Files[0].Id;
        }

        // Not found by name; assume the caller passed a literal folder ID.
        return folder;
    }

    public string BuildQuery(FilterSettings filters, string? resolvedFolderId)
    {
        var clauses = new List<string> { "trashed = false" };

        if (filters.Audio)
        {
            clauses.Add("mimeType contains 'audio/'");
        }

        if (!string.IsNullOrWhiteSpace(filters.MimeType))
        {
            clauses.Add($"mimeType contains '{EscapeForQuery(filters.MimeType)}'");
        }

        if (!string.IsNullOrWhiteSpace(filters.NameContains))
        {
            clauses.Add($"name contains '{EscapeForQuery(filters.NameContains)}'");
        }

        if (!string.IsNullOrWhiteSpace(resolvedFolderId))
        {
            clauses.Add($"'{EscapeForQuery(resolvedFolderId)}' in parents");
        }

        if (!string.IsNullOrWhiteSpace(filters.OlderThan) && DateTime.TryParse(filters.OlderThan, out var olderThan))
        {
            clauses.Add($"modifiedTime < '{olderThan:yyyy-MM-dd}T00:00:00'");
        }

        if (!string.IsNullOrWhiteSpace(filters.NewerThan) && DateTime.TryParse(filters.NewerThan, out var newerThan))
        {
            clauses.Add($"modifiedTime > '{newerThan:yyyy-MM-dd}T00:00:00'");
        }

        if (!string.IsNullOrWhiteSpace(filters.RawQuery))
        {
            clauses.Add($"({filters.RawQuery})");
        }

        return string.Join(" and ", clauses);
    }

    public async Task<List<DriveFile>> ListMatchesAsync(FilterSettings filters, string query, CancellationToken ct)
    {
        var matches = new List<DriveFile>();
        string? pageToken = null;

        do
        {
            var request = _drive.Files.List();
            request.Q = query;
            request.Fields = FileFields;
            request.PageSize = 1000;
            request.PageToken = pageToken;
            request.Spaces = "drive";

            var result = await request.ExecuteAsync(ct);

            foreach (var file in result.Files ?? Enumerable.Empty<DriveFile>())
            {
                if (!MatchesExtension(file, filters.Extension) || !MatchesSize(file, filters.MinSize, filters.MaxSize))
                {
                    continue;
                }

                matches.Add(file);
                if (filters.Limit is { } limit && matches.Count >= limit)
                {
                    return matches;
                }
            }

            pageToken = result.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return matches;
    }

    public async Task<(int Succeeded, List<TrashFailure> Failed)> TrashFilesAsync(
        IReadOnlyList<DriveFile> files,
        Action<int, int>? onBatchProgress,
        CancellationToken ct)
    {
        var succeeded = 0;
        var failed = new List<TrashFailure>();

        for (var i = 0; i < files.Count; i += BatchSize)
        {
            var batchFiles = files.Skip(i).Take(BatchSize).ToList();
            var batch = new BatchRequest(_drive);

            foreach (var file in batchFiles)
            {
                var patch = new DriveFile { Trashed = true };
                var updateRequest = _drive.Files.Update(patch, file.Id);
                updateRequest.Fields = "id";

                batch.Queue<DriveFile>(updateRequest, (content, error, index, message) =>
                {
                    if (error != null)
                    {
                        failed.Add(new TrashFailure(file.Id, file.Name, error.Message));
                    }
                    else
                    {
                        succeeded++;
                    }
                });
            }

            await batch.ExecuteAsync(ct);
            onBatchProgress?.Invoke(Math.Min(i + BatchSize, files.Count), files.Count);
        }

        return (succeeded, failed);
    }

    private static bool MatchesExtension(DriveFile file, string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return true;
        }

        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return file.Name is not null && file.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSize(DriveFile file, long? min, long? max)
    {
        if (min is null && max is null)
        {
            return true;
        }

        // Files without a size (e.g. native Google Docs/Sheets) can't satisfy a size filter.
        if (file.Size is null)
        {
            return false;
        }

        if (min is { } minValue && file.Size < minValue)
        {
            return false;
        }

        if (max is { } maxValue && file.Size > maxValue)
        {
            return false;
        }

        return true;
    }

    private static string EscapeForQuery(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");
}
