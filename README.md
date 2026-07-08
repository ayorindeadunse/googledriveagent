# GoogleDriveAgent

A console tool for bulk-cleaning Google Drive: describe what you want gone with a filter, review the exact list of matches, confirm once, and it moves all of them to Trash — instead of checking boxes 100 at a time in the Drive web UI.

Deletes always go to **Trash** (recoverable from Drive, same as clicking Delete in the web UI). Nothing is permanently deleted.

## One-time Google Cloud setup

You need your own OAuth client so the tool can act on your Drive account.

1. Go to the [Google Cloud Console](https://console.cloud.google.com/), create a project (or pick an existing one).
2. **APIs & Services > Library** → search "Google Drive API" → Enable.
3. **APIs & Services > OAuth consent screen** → User type "External" → fill in the required fields → keep publishing status as **Testing** → under "Test users" add your own Google account email.
4. **APIs & Services > Credentials** → Create Credentials → OAuth client ID → Application type **Desktop app** → Create.
5. Download the JSON for that client and save it as `credentials.json` in this project's working directory (or anywhere, and pass `--credentials /path/to/file.json`).

The first time you run any command, a browser window opens for you to sign in and consent. After that, a refresh token is cached in `token_store/` (or wherever `--token-store` points) so you won't be prompted again.

`credentials.json` and `token_store/` are already in `.gitignore` — never commit them.

## Build & run

```bash
cd GoogleDriveAgent
dotnet build
dotnet run -- list --audio
```

## Usage

Always preview with `list` before running `delete` with the same filters.

```bash
# Preview every audio file in Drive
dotnet run -- list --audio

# Preview audio files older than a date, inside a specific folder
dotnet run -- list --audio --older-than 2024-01-01 --folder "Voice Recorder"

# Trash all matched audio files (asks for confirmation first)
dotnet run -- delete --audio

# Trash only the first 5 matches, as a trial run
dotnet run -- delete --audio --limit 5

# Skip the confirmation prompt (e.g. for a repeat run you've already verified)
dotnet run -- delete --audio --folder "Voice Recorder" --yes

# Preview the 20 largest files in your whole Drive, regardless of type
dotnet run -- list --largest --limit 20

# Trash the 10 largest files (asks for confirmation first)
dotnet run -- delete --largest --limit 10

# The 15 largest audio files specifically
dotnet run -- list --audio --largest --limit 15

# Multiple extensions at once - repeat the flag, or comma-separate
dotnet run -- list --ext .mp3 --ext .wav --ext .m4a
dotnet run -- delete --ext .mp3,.wav,.m4a

# See your real storage breakdown (active files vs. Trash, which still counts against quota)
dotnet run -- usage

# Find duplicate files (identical content) and how much space they waste
dotnet run -- duplicates

# Keep one copy of each duplicate and trash the rest (asks for confirmation first)
dotnet run -- delete-duplicates
dotnet run -- delete-duplicates --keep newest --min-size 100000
```

### Filters (combine as many as you like; all are ANDed together)

| Flag | Matches |
|---|---|
| `--audio` | MIME type starts with `audio/` |
| `--mime-type <value>` | MIME type contains `<value>` |
| `--ext <.mp3>` | File name ends with this extension. Repeat the flag or comma-separate for multiple, e.g. `--ext .mp3,.wav` |
| `--name-contains <text>` | File name contains this text |
| `--folder <name-or-id>` | File lives directly in this folder |
| `--older-than <yyyy-MM-dd>` | Last modified before this date |
| `--newer-than <yyyy-MM-dd>` | Last modified after this date |
| `--min-size <bytes>` / `--max-size <bytes>` | File size range |
| `--query <raw Drive query>` | Escape hatch — any [Drive search query](https://developers.google.com/drive/api/guides/search-files) |
| `--limit <n>` | Only act on the first N matches |
| `--largest` | Sort matches by size, largest first. Must be paired with `--limit` |

At least one filter is required — the tool refuses to run against "every file in Drive" by accident.

### Other commands

| Command | Does |
|---|---|
| `usage` | Shows total storage usage, split into active Drive files vs. Trash (Trash still counts against your quota until emptied) |
| `duplicates` | Scans Drive by content checksum (not just name) and reports duplicate sets sorted by wasted space |
| `delete-duplicates` | Keeps one copy per duplicate set (`--keep oldest`\|`newest`, default oldest) and trashes the rest, after preview + confirmation |

## Notes

- Deletes are batched in groups of 100 (mirroring Drive's own bulk-action size), with a progress bar and a final succeeded/failed count.
- Rate-limit retries are handled automatically by the underlying Google API client.
- Files without a size (native Google Docs/Sheets/Slides) are excluded whenever `--min-size`/`--max-size` is used, since they have no byte size to compare.
