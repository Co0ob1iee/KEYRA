using System.Windows.Data;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;
using SshKeyManager.Services;

namespace SshKeyManager.Localization;

[MarkupExtensionReturnType(typeof(BindingExpression))]
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            return string.Empty;
        }

        var localization = App.Services.GetRequiredService<ILocalizationService>();
        var binding = new Binding($"[{Key}]")
        {
            Source = localization,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
