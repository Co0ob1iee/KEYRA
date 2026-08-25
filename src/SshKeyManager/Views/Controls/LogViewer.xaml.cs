using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using SshKeyManager.ViewModels;

namespace SshKeyManager.Views.Controls;

public partial class LogViewer : UserControl
{
    private bool _autoScroll = true;
    private LogPanelViewModel? _subscribed;

    public LogViewer()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => Attach();

    private void Attach()
    {
        Detach();
        _subscribed = DataContext as LogPanelViewModel;
        if (_subscribed?.Entries is INotifyCollectionChanged notify)
        {
            notify.CollectionChanged += Entries_CollectionChanged;
        }
    }

    private void Detach()
    {
        if (_subscribed?.Entries is INotifyCollectionChanged notify)
        {
            notify.CollectionChanged -= Entries_CollectionChanged;
        }

        _subscribed = null;
    }

    private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_autoScroll || e.Action != NotifyCollectionChangedAction.Add)
        {
            return;
        }

        if (LogList.Items.Count > 0)
        {
            LogList.ScrollIntoView(LogList.Items[0]);
        }
    }

    private void LogList_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange == 0)
        {
            // User scrolled: disable auto-scroll when not at top (newest entries are inserted at index 0).
            _autoScroll = e.VerticalOffset <= 2;
        }
    }
}
