namespace SshKeyManager.Helpers;

/// <summary>
/// Prefix matching and ghost-text suggestion logic for terminal command input.
/// </summary>
public static class TerminalSuggestionEngine
{
    public const int MaxPopupItems = 8;

    /// <summary>
    /// Returns the suffix to display as ghost text for the most recent history match.
    /// </summary>
    public static string? FindGhostSuffix(string input, IReadOnlyList<string> history)
    {
        if (string.IsNullOrEmpty(input) || history.Count == 0)
        {
            return null;
        }

        for (var i = history.Count - 1; i >= 0; i--)
        {
            var entry = history[i];
            if (entry.StartsWith(input, StringComparison.Ordinal)
                && entry.Length > input.Length)
            {
                return entry[input.Length..];
            }
        }

        return null;
    }

    /// <summary>
    /// Returns filtered history entries for the suggestion popup (newest first).
    /// </summary>
    public static IReadOnlyList<string> FindMatches(string input, IReadOnlyList<string> history, int maxItems = MaxPopupItems)
    {
        if (history.Count == 0)
        {
            return Array.Empty<string>();
        }

        IEnumerable<string> candidates;
        if (string.IsNullOrWhiteSpace(input))
        {
            candidates = history.TakeLast(maxItems);
        }
        else
        {
            candidates = history
                .Where(entry =>
                    entry.StartsWith(input, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(entry, input, StringComparison.Ordinal));
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .TakeLast(maxItems)
            .Reverse()
            .ToList();
    }

    /// <summary>
    /// Splits the line before the cursor into a stable prefix and the partial word being completed.
    /// </summary>
    public static (string Prefix, string PartialWord) GetPartialWord(string lineBeforeCursor)
    {
        if (string.IsNullOrEmpty(lineBeforeCursor))
        {
            return (string.Empty, string.Empty);
        }

        var lastSpace = lineBeforeCursor.LastIndexOf(' ');
        if (lastSpace < 0)
        {
            return (string.Empty, lineBeforeCursor);
        }

        return (lineBeforeCursor[..(lastSpace + 1)], lineBeforeCursor[(lastSpace + 1)..]);
    }

    /// <summary>
    /// Builds the completed command line by replacing the partial word with the chosen match.
    /// </summary>
    public static string ApplyRemoteMatch(string prefix, string match) =>
        prefix + match;

    /// <summary>
    /// Returns the suffix to append when a single remote match extends the partial word.
    /// </summary>
    public static string GetRemoteCompletionSuffix(string partialWord, string match)
    {
        if (string.IsNullOrEmpty(match))
        {
            return string.Empty;
        }

        if (match.StartsWith(partialWord, StringComparison.Ordinal))
        {
            return match[partialWord.Length..];
        }

        return match;
    }

    /// <summary>
    /// Adds a command to history, deduplicating and enforcing max count (most recent last).
    /// </summary>
    public static List<string> AppendHistory(
        IReadOnlyList<string> history,
        string command,
        int maxCount)
    {
        var trimmed = command.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return history.ToList();
        }

        var list = history.ToList();
        list.RemoveAll(entry => string.Equals(entry, trimmed, StringComparison.Ordinal));
        list.Add(trimmed);

        while (list.Count > maxCount)
        {
            list.RemoveAt(0);
        }

        return list;
    }
}
