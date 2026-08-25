namespace SshKeyManager.Models;

public sealed class AppLogEntry
{
    public AppLogEntry(DateTime timestampUtc, string level, string message)
    {
        TimestampUtc = timestampUtc;
        Level = level ?? throw new ArgumentNullException(nameof(level));
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }

    public DateTime TimestampUtc { get; }

    public string Level { get; }

    public string Message { get; }

    public string DisplayText => $"{TimestampUtc.ToLocalTime():HH:mm:ss} [{Level}] {Message}";
}
