using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace GoogleDriveAgent.Services;

public static class DriveAuthService
{
    public static async Task<DriveService> AuthenticateAsync(string credentialsPath, string tokenStorePath, CancellationToken ct)
    {
        if (!File.Exists(credentialsPath))
        {
            throw new FileNotFoundException(
                $"Could not find OAuth client credentials at '{credentialsPath}'. Download it from Google Cloud " +
                "Console (APIs & Services > Credentials > your Desktop app client > Download JSON) and save it " +
                "there, or pass --credentials <path>. See README.md for full setup steps.");
        }

        UserCredential credential;
        await using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
        {
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                (await GoogleClientSecrets.FromStreamAsync(stream, ct)).Secrets,
                new[] { DriveService.Scope.Drive },
                "user",
                ct,
                new FileDataStore(tokenStorePath, true));
        }

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "GoogleDriveAgent",
        });
    }
}
