using System.Windows;
using System.Windows.Controls;

namespace SshKeyManager.Helpers;

public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundPasswordChanged));

    public static readonly DependencyProperty BindPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BindPassword",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(false, OnBindPasswordChanged));

    private static readonly DependencyProperty UpdatingPasswordProperty =
        DependencyProperty.RegisterAttached(
            "UpdatingPassword",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject obj) =>
        (string)obj.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject obj, string value) =>
        obj.SetValue(BoundPasswordProperty, value);

    public static bool GetBindPassword(DependencyObject obj) =>
        (bool)obj.GetValue(BindPasswordProperty);

    public static void SetBindPassword(DependencyObject obj, bool value) =>
        obj.SetValue(BindPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
        {
            return;
        }

        box.PasswordChanged -= HandlePasswordChanged;
        if (!(bool)box.GetValue(UpdatingPasswordProperty))
        {
            box.Password = e.NewValue as string ?? string.Empty;
        }

        box.PasswordChanged += HandlePasswordChanged;
    }

    private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
        {
            return;
        }

        if ((bool)e.OldValue)
        {
            box.PasswordChanged -= HandlePasswordChanged;
        }

        if ((bool)e.NewValue)
        {
            box.PasswordChanged += HandlePasswordChanged;
        }
    }

    private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox box)
        {
            return;
        }

        box.SetValue(UpdatingPasswordProperty, true);
        SetBoundPassword(box, box.Password);
        box.SetValue(UpdatingPasswordProperty, false);
    }
}
