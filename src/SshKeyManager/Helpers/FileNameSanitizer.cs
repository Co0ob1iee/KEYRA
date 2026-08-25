using System.Text.RegularExpressions;

namespace SshKeyManager.Helpers;

public static class FileNameSanitizer
{
    private static readonly Regex Invalid = new($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", RegexOptions.Compiled);

    public static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var cleaned = Invalid.Replace(name.Trim(), "_");
        cleaned = cleaned.Replace(' ', '_');
        while (cleaned.Contains("__", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("__", "_", StringComparison.Ordinal);
        }

        return cleaned.Trim('_');
    }
}
