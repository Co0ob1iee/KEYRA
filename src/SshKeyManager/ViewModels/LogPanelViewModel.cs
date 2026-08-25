using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SshKeyManager.Models;
using SshKeyManager.Presentation;
using SshKeyManager.Services;

namespace SshKeyManager.ViewModels;

public partial class LogPanelViewModel : LocalizedViewModelBase
{
    private readonly IAppLogService _log;
    private readonly IShellLayoutService _layout;
    private readonly Action<string> _setStatus;
    private readonly ListCollectionView _filteredView;

    public LogPanelViewModel(
        IAppLogService log,
        ILocalizationService localization,
        IShellLayoutService layout,
        Action<string> setStatus)
        : base(localization)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));

        IsExpanded = _layout.IsLogExpanded;
        PanelHeight = _layout.LogPanelHeight;

        _filteredView = (ListCollectionView)CollectionViewSource.GetDefaultView(_log.Entries);
        _filteredView.Filter = FilterEntry;
        ((INotifyCollectionChanged)_log.Entries).CollectionChanged += (_, _) =>
        {
            _filteredView.Refresh();
            OnPropertyChanged(nameof(FilteredEntries));
        };

        FilterLevels = new ObservableCollection<string>
        {
            L("Log_FilterAll"),
            "INFO",
            "WARN",
            "ERROR"
        };
        SelectedFilterLevel = FilterLevels[0];
    }

    public ObservableCollection<AppLogEntry> Entries => _log.Entries;

    public ICollectionView FilteredEntries => _filteredView;

    public ObservableCollection<string> FilterLevels { get; }

    public string Title => L("Log_Title");

    public string ClearLabel => L("Log_Clear");

    public string ExpandLabel => L("Log_Expand");

    public string CollapseLabel => L("Log_Collapse");

    public string FilterLabel => L("Log_Filter");

    public string SearchLabel => L("Log_Search");

    public string ToggleLabel => IsExpanded ? CollapseLabel : ExpandLabel;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private double _panelHeight = 140;

    [ObservableProperty]
    private string _selectedFilterLevel = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    protected override void OnLocalizationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnLocalizationPropertyChanged(sender, e);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ClearLabel));
        OnPropertyChanged(nameof(ExpandLabel));
        OnPropertyChanged(nameof(CollapseLabel));
        OnPropertyChanged(nameof(FilterLabel));
        OnPropertyChanged(nameof(SearchLabel));
        OnPropertyChanged(nameof(ToggleLabel));

        var previous = SelectedFilterLevel;
        FilterLevels[0] = L("Log_FilterAll");
        if (previous.Equals("INFO", StringComparison.OrdinalIgnoreCase) ||
            previous.Equals("WARN", StringComparison.OrdinalIgnoreCase) ||
            previous.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            SelectedFilterLevel = previous;
        }
        else
        {
            SelectedFilterLevel = FilterLevels[0];
        }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        _layout.IsLogExpanded = value;
        OnPropertyChanged(nameof(ToggleLabel));
        _ = _layout.PersistAsync();
    }

    partial void OnPanelHeightChanged(double value)
    {
        _layout.LogPanelHeight = value;
        _ = _layout.PersistAsync();
    }

    partial void OnSelectedFilterLevelChanged(string value) => _filteredView?.Refresh();

    partial void OnSearchTextChanged(string value) => _filteredView?.Refresh();

    [RelayCommand]
    private void ClearLog()
    {
        try
        {
            _log.Clear();
            _setStatus(L("Status_LogCleared"));
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    private bool FilterEntry(object obj)
    {
        if (obj is not AppLogEntry entry)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SelectedFilterLevel) &&
            !SelectedFilterLevel.Equals(L("Log_FilterAll"), StringComparison.OrdinalIgnoreCase) &&
            !entry.Level.Equals(SelectedFilterLevel, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText) &&
            entry.DisplayText.IndexOf(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        return true;
    }
}
