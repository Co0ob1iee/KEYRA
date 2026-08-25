namespace SshKeyManager.Models;

public sealed class NavigationItem
{
    public NavigationItem(AppSection section, string title, string iconGlyph, bool isActive = false)
    {
        Section = section;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        IconGlyph = iconGlyph ?? throw new ArgumentNullException(nameof(iconGlyph));
        IsActive = isActive;
    }

    public AppSection Section { get; }

    public string Title { get; }

    public string IconGlyph { get; }

    public bool IsActive { get; }
}
