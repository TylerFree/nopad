using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Noopad.Services;
using Noopad.ViewModels;

namespace Noopad.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;
    private bool _singleInstanceServerStarted;
    private DispatcherTimer? _externalChangeTimer;
    private bool _checkingExternalChanges;
    private SearchReplaceDialog? _searchDialog;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _vm = DataContext as MainWindowViewModel;
        if (_vm == null) return;

        _vm.FileDialog = new FileDialogService(this);

        _vm.SaveDialogHandler = async (tab) =>
        {
            var dialog = new SaveConfirmDialog(tab.Title);
            return await dialog.ShowDialog<bool?>(this);
        };

        _vm.CreateMissingFileHandler = async (path) =>
        {
            var dialog = new CreateFileDialog(path);
            return await dialog.ShowDialog<bool>(this);
        };

        _vm.ReloadFileHandler = async (tab) =>
        {
            BringToFront();
            var dialog = new ReloadFileDialog(tab.Title, tab.FilePath ?? tab.Title, tab.IsDirty);
            return await dialog.ShowDialog<bool>(this);
        };

        _vm.ShowSettingsHandler = async () =>
        {
            if (_vm?.Settings is IUserSettingsService svc)
            {
                var dialog = new SettingsDialog(svc, _vm.RecoveryDirectory);
                var result = await dialog.ShowDialog<bool?>(this);
                if (result == true)
                {
                    _vm.ApplySettingsToAllTabs();
                    FindEditorView()?.ApplyFontSettings(svc.Settings);
                }
            }
        };

        _vm.FileLoaded += () => Dispatcher.UIThread.Post(BringToFront);
        _vm.GoToLineRequested += () => _ = ShowGoToLineDialogAsync();
        _vm.SearchDialogRequested += ShowSearchReplaceDialog;

        _vm.SelectTextRequested += (start, length) =>
        {
            FindEditorView()?.SelectText(start, length);
        };

        _vm.ReplaceTextRequested += (start, length, replacement) =>
        {
            FindEditorView()?.ReplaceText(start, length, replacement);
        };

        WireTabStrip();
        WireMenuItems();

        await _vm.InitializeAsync();
        StartExternalChangeTimer();
        StartSingleInstanceServer();
    }

    private void StartExternalChangeTimer()
    {
        if (_externalChangeTimer != null)
            return;

        _externalChangeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _externalChangeTimer.Tick += async (_, _) =>
        {
            if (_vm == null || _checkingExternalChanges)
                return;

            _checkingExternalChanges = true;
            try
            {
                await _vm.CheckForExternalFileChangesAsync();
            }
            finally
            {
                _checkingExternalChanges = false;
            }
        };
        _externalChangeTimer.Start();
    }

    private void StartSingleInstanceServer()
    {
        if (_singleInstanceServerStarted || App.SingleInstanceCoordinator == null)
            return;

        _singleInstanceServerStarted = true;
        App.SingleInstanceCoordinator.StartServer(async args =>
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                BringToFront();
                if (_vm != null)
                    await _vm.OpenStartupFilePathsAsync(args, promptToCreate: true);
            });
        });
    }

    private void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
        Focus();
    }

    private void WireTabStrip()
    {
        var tabStrip = this.FindControl<ItemsControl>("TabStrip");
        if (tabStrip == null) return;

        tabStrip.AddHandler(Button.ClickEvent, (object? sender, RoutedEventArgs e) =>
        {
            if (e.Source is Button btn)
            {
                if (btn.Name == "CloseTabBtn")
                {
                    var tabVm = btn.DataContext as EditorTabViewModel;
                    if (_vm != null) _ = _vm.CloseTabCommand.ExecuteAsync(tabVm);
                    e.Handled = true;
                }
                else if (btn.Name == "TabButton")
                {
                    if (_vm != null && btn.DataContext is EditorTabViewModel tabVm)
                        _vm.ActiveTab = tabVm;
                    e.Handled = true;
                }
            }
        });
    }

    private void WireMenuItems()
    {
        if (_vm == null) return;

        var menuExit = this.FindControl<MenuItem>("MenuExit");
        if (menuExit != null) menuExit.Click += (_, _) => Close();

        var menuAbout = this.FindControl<MenuItem>("MenuAbout");
        if (menuAbout != null) menuAbout.Click += async (_, _) =>
        {
            var dlg = new AboutDialog();
            await dlg.ShowDialog(this);
        };

        var menuFindNext = this.FindControl<MenuItem>("MenuFindNext");
        if (menuFindNext != null) menuFindNext.Click += (_, _) => _vm.SearchPanel.FindNextCommand.Execute(null);

        var menuFindPrev = this.FindControl<MenuItem>("MenuFindPrev");
        if (menuFindPrev != null) menuFindPrev.Click += (_, _) => _vm.SearchPanel.FindPreviousCommand.Execute(null);

        var menuUndo = this.FindControl<MenuItem>("MenuUndo");
        if (menuUndo != null) menuUndo.Click += (_, _) => FindEditorView()?.Undo();

        var menuRedo = this.FindControl<MenuItem>("MenuRedo");
        if (menuRedo != null) menuRedo.Click += (_, _) => FindEditorView()?.Redo();

        var menuSelectAll = this.FindControl<MenuItem>("MenuSelectAll");
        if (menuSelectAll != null) menuSelectAll.Click += (_, _) => FindEditorView()?.SelectAll();

        var menuCut = this.FindControl<MenuItem>("MenuCut");
        if (menuCut != null) menuCut.Click += (_, _) => FindEditorView()?.Cut();

        var menuCopy = this.FindControl<MenuItem>("MenuCopy");
        if (menuCopy != null) menuCopy.Click += (_, _) => FindEditorView()?.Copy();

        var menuPaste = this.FindControl<MenuItem>("MenuPaste");
        if (menuPaste != null) menuPaste.Click += (_, _) => FindEditorView()?.Paste();
    }

    private async void CopyTabPathClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || TryGetContextMenuTab(menuItem) is not { } tab)
            return;

        if (string.IsNullOrWhiteSpace(tab.FilePath))
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
            await clipboard.SetTextAsync(tab.FilePath);

        if (_vm != null)
            _vm.StatusMessage = $"Copied path for {tab.Title}";

        e.Handled = true;
    }

    private void CloseTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && TryGetContextMenuTab(menuItem) is { } tab && _vm != null)
        {
            _ = _vm.CloseTabCommand.ExecuteAsync(tab);
            e.Handled = true;
        }
    }

    private static EditorTabViewModel? TryGetContextMenuTab(MenuItem menuItem)
    {
        if (menuItem.DataContext is EditorTabViewModel tab)
            return tab;

        return (menuItem.Parent as ContextMenu)?.PlacementTarget?.DataContext as EditorTabViewModel;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_vm == null) return;

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.Tab)
        {
            _vm.NextTab(); e.Handled = true;
        }
        else if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Tab)
        {
            _vm.PreviousTab(); e.Handled = true;
        }
        else if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.F3)
        {
            _vm.SearchPanel.FindNextCommand.Execute(null); e.Handled = true;
        }
        else if (e.KeyModifiers == KeyModifiers.Shift && e.Key == Key.F3)
        {
            _vm.SearchPanel.FindPreviousCommand.Execute(null); e.Handled = true;
        }
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.G)
        {
            _ = ShowGoToLineDialogAsync(); e.Handled = true;
        }
    }

    private void ShowSearchReplaceDialog()
    {
        if (_vm == null)
            return;

        if (_searchDialog == null)
        {
            _searchDialog = new SearchReplaceDialog { DataContext = _vm.SearchPanel };
            _searchDialog.Closed += (_, _) =>
            {
                if (_vm != null)
                    _vm.SearchPanel.IsVisible = false;
                _searchDialog = null;
            };
            _searchDialog.Show(this);
        }
        else
        {
            _searchDialog.Activate();
            _vm.SearchPanel.RequestFocus();
        }
    }

    public async Task ShowGoToLineDialogAsync()
    {
        var editor = FindEditorView();
        if (editor == null)
            return;

        var dialog = new GoToLineDialog(editor.LineCount, _vm?.ActiveTab?.CursorLine ?? 1);
        var line = await dialog.ShowDialog<int?>(this);
        if (line != null)
            editor.GoToLine(line.Value);
    }

    private EditorDocumentView? FindEditorView()
    {
        return FindDescendant<EditorDocumentView>(this);
    }

    private static T? FindDescendant<T>(Avalonia.Visual root) where T : Avalonia.Visual
    {
        if (root is T match) return match;
        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(root))
        {
            var found = FindDescendant<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        _externalChangeTimer?.Stop();
        if (_vm != null)
            await _vm.SaveRecoveryAsync();
        base.OnClosing(e);
    }
}