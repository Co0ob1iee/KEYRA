using SshKeyManager.Models;

namespace SshKeyManager.Services.Ssh;

public sealed class SshSessionLaunchRequest
{
    public string? ProfileName { get; init; }

    public required string Host { get; init; }

    public int Port { get; init; } = 22;

    public required string Username { get; init; }

    public bool UsePasswordAuth { get; init; }

    public string Password { get; init; } = string.Empty;

    public SshKeyRecord? SelectedKey { get; init; }

    public string KeyPassphrase { get; init; } = string.Empty;

    public string BuildDisplayTitle()
    {
        string endpoint;
        if (!string.IsNullOrWhiteSpace(ProfileName))
        {
            endpoint = ProfileName.Trim();
        }
        else
        {
            var user = Username?.Trim() ?? string.Empty;
            var host = Host?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(user))
            {
                endpoint = host;
            }
            else
            {
                endpoint = string.IsNullOrEmpty(host) ? user : $"{user}@{host}";
            }
        }

        return string.IsNullOrWhiteSpace(endpoint) ? "KEYRA" : $"KEYRA — {endpoint}";
    }
}
