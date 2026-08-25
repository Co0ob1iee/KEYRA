namespace SshKeyManager.Models;

public sealed class LanguageOption
{
    public required string CultureName { get; init; }

    public required string NativeName { get; init; }

    public static IReadOnlyList<LanguageOption> All { get; } =
    [
        new LanguageOption { CultureName = "pl-PL", NativeName = "Polski" },
        new LanguageOption { CultureName = "en-US", NativeName = "English" },
        new LanguageOption { CultureName = "ru-RU", NativeName = "Русский" },
        new LanguageOption { CultureName = "zh-CN", NativeName = "中文" },
        new LanguageOption { CultureName = "fr-FR", NativeName = "Français" },
        new LanguageOption { CultureName = "de-DE", NativeName = "Deutsch" }
    ];

    public override string ToString() => NativeName;
}
