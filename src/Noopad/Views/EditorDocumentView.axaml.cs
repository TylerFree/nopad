using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using CommunityToolkit.Mvvm.Input;
using Noopad.Models;
using Noopad.Services;
using Noopad.ViewModels;

namespace Noopad.Views;

public partial class EditorDocumentView : UserControl
{
    private EditorTabViewModel? _viewModel;
    private bool _updatingFromVm;
    private bool _updatingFromEditor;

    public EditorDocumentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnVmPropertyChanged;

        _viewModel = DataContext as EditorTabViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnVmPropertyChanged;
            SyncFromViewModel();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null) return;

        editor.TextChanged += OnEditorTextChanged;
        editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        editor.TextArea.TextView.ScrollOffsetChanged += OnScrollOffsetChanged;
        editor.TextArea.KeyDown += OnEditorKeyDown;

        // Disable AvaloniaEdit's built-in search panel so Ctrl+F focuses our find box instead.
        try { editor.SearchPanel?.Uninstall(); } catch { /* ignore if not installed */ }

        // Open markdown hyperlinks safely in the default browser instead of crashing the app.
        MarkdownPreview.Plugins.HyperlinkCommand = new RelayCommand<object?>(OpenHyperlink);

        ApplyStoredFontSettings(editor);
        SyncFromViewModel();
    }

    private void ApplyStoredFontSettings(TextEditor editor)
    {
        var mainVm = GetMainViewModel();
        if (mainVm?.Settings?.Settings is UserSettings s)
        {
            editor.FontSize = s.FontSize;
            editor.FontFamily = new FontFamily(s.FontFamily);
        }
    }

    public void ApplyFontSettings(UserSettings settings)
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null) return;
        editor.FontSize = settings.FontSize;
        editor.FontFamily = new FontFamily(settings.FontFamily);
    }

    private void SyncFromViewModel()
    {
        if (_viewModel == null) return;
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null) return;

        _updatingFromVm = true;
        try
        {
            if (editor.Text != _viewModel.Content)
                editor.Text = _viewModel.Content;
            ApplySyntaxHighlighting(editor, _viewModel.Syntax);
            UpdatePreviewColumnWidth(_viewModel.ShowMarkdownPreview);
        }
        finally
        {
            _updatingFromVm = false;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_updatingFromEditor) return;

        Dispatcher.UIThread.Post(() =>
        {
            var editor = this.FindControl<TextEditor>("TextEditor");
            if (editor == null) return;

            switch (e.PropertyName)
            {
                case nameof(EditorTabViewModel.Content):
                    if (_viewModel != null && editor.Text != _viewModel.Content)
                    {
                        _updatingFromVm = true;
                        editor.Text = _viewModel.Content;
                        _updatingFromVm = false;
                    }
                    break;
                case nameof(EditorTabViewModel.Syntax):
                    if (_viewModel != null)
                        ApplySyntaxHighlighting(editor, _viewModel.Syntax);
                    break;
                case nameof(EditorTabViewModel.ShowMarkdownPreview):
                    if (_viewModel != null)
                        UpdatePreviewColumnWidth(_viewModel.ShowMarkdownPreview);
                    break;
            }
        });
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_updatingFromVm || _viewModel == null) return;
        _updatingFromEditor = true;
        try
        {
            var editor = (TextEditor)sender!;
            if (DataContext is EditorTabViewModel vm)
            {
                var mainVm = GetMainViewModel();
                if (mainVm != null)
                    mainVm.OnContentChanged(vm, editor.Text);
                else
                {
                    vm.Content = editor.Text;
                    vm.IsDirty = true;
                }
            }
        }
        finally
        {
            _updatingFromEditor = false;
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_viewModel == null) return;
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null) return;
        var caret = editor.TextArea.Caret;
        var mainVm = GetMainViewModel();
        mainVm?.OnCursorChanged(_viewModel, caret.Line, caret.Column);
        _viewModel.CursorLine = caret.Line;
        _viewModel.CursorColumn = caret.Column;
    }

    private void OnScrollOffsetChanged(object? sender, EventArgs e)
    {
        if (_viewModel == null) return;
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor != null)
            _viewModel.VerticalOffset = editor.VerticalOffset;
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null) return;

        if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.None)
        {
            InsertIndent(editor);
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.Shift)
        {
            RemoveIndent(editor);
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            var mainVm = GetMainViewModel();
            mainVm?.ShowFindPanelCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.H && e.KeyModifiers == KeyModifiers.Control)
        {
            var mainVm = GetMainViewModel();
            mainVm?.ShowReplacePanelCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.G && e.KeyModifiers == KeyModifiers.Control)
        {
            var mainVm = GetMainViewModel();
            mainVm?.ShowGoToLineCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static void InsertIndent(TextEditor editor)
    {
        var doc = editor.Document;
        var caret = editor.TextArea.Caret;
        doc.Insert(caret.Offset, "   ");
    }

    private static void RemoveIndent(TextEditor editor)
    {
        var doc = editor.Document;
        var caret = editor.TextArea.Caret;
        var line = doc.GetLineByOffset(caret.Offset);
        var lineText = doc.GetText(line.Offset, line.Length);
        int spaces = 0;
        foreach (var ch in lineText)
        {
            if (ch == ' ' && spaces < 3) spaces++;
            else break;
        }
        if (spaces > 0)
            doc.Remove(line.Offset, spaces);
    }

    private void ApplySyntaxHighlighting(TextEditor editor, SyntaxLanguage syntax)
    {
        var name = syntax switch
        {
            SyntaxLanguage.Json => "Json",
            SyntaxLanguage.Xml => "XML",
            SyntaxLanguage.Yaml => "YAML",
            SyntaxLanguage.Markdown => "MarkDown",
            SyntaxLanguage.CSharp => "C#",
            SyntaxLanguage.Python => "Python",
            SyntaxLanguage.JavaScript => "JavaScript",
            _ => null
        };

        try
        {
            if (name != null)
            {
                var definition = HighlightingManager.Instance.GetDefinition(name);
                if (definition != null && name == "Json")
                    definition = BuildDarkJsonHighlighting(definition);
                editor.SyntaxHighlighting = definition;
            }
            else
            {
                editor.SyntaxHighlighting = null;
            }
        }
        catch { editor.SyntaxHighlighting = null; }
    }

    // Remap JSON highlighting colors for visibility on dark backgrounds
    private static IHighlightingDefinition BuildDarkJsonHighlighting(IHighlightingDefinition def)
    {
        // Map of rule names to dark-mode friendly colors
        var colorMap = new Dictionary<string, Color>
        {
            { "String", Color.FromRgb(0xCE, 0x91, 0x78) },      // warm orange
            { "NumberLiteral", Color.FromRgb(0xB5, 0xCE, 0xA8) },// soft green
            { "Keyword", Color.FromRgb(0x56, 0x9C, 0xD6) },      // blue
            { "Punctuation", Color.FromRgb(0xCC, 0xCC, 0xCC) },  // light gray
        };

        foreach (var rule in def.MainRuleSet.Rules)
        {
            foreach (var (ruleName, color) in colorMap)
            {
                if (rule.Color?.Name?.Contains(ruleName, StringComparison.OrdinalIgnoreCase) == true ||
                    (rule.Color != null && def.GetNamedColor(rule.Color.Name)?.Name?.Contains(ruleName, StringComparison.OrdinalIgnoreCase) == true))
                {
                    rule.Color.Foreground = new SimpleHighlightingBrush(color);
                }
            }
        }

        // Also update named colors in the definition
        foreach (var (ruleName, color) in colorMap)
        {
            var namedColor = def.NamedHighlightingColors.FirstOrDefault(c =>
                c.Name?.Contains(ruleName, StringComparison.OrdinalIgnoreCase) == true);
            if (namedColor != null)
                namedColor.Foreground = new SimpleHighlightingBrush(color);
        }

        return def;
    }

    private void UpdatePreviewColumnWidth(bool show)
    {
        var grid = this.FindControl<Grid>("EditorGrid");
        if (grid?.ColumnDefinitions.Count >= 3)
        {
            grid.ColumnDefinitions[2].Width = show
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);
        }
    }

    private void OpenHyperlink(object? parameter)
    {
        var url = parameter?.ToString();
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp ||
                 uri.Scheme == Uri.UriSchemeHttps ||
                 uri.Scheme == Uri.UriSchemeMailto ||
                 uri.Scheme == Uri.UriSchemeFile))
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            // Relative links or in-document anchors are ignored safely.
        }
        catch (Exception ex)
        {
            var mainVm = GetMainViewModel();
            if (mainVm != null)
                mainVm.StatusMessage = $"Could not open link: {ex.Message}";
        }
    }

    public void SelectText(int start, int length)
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null) return;
        editor.Focus();
        ScrollOffsetIntoView(editor, start);
        editor.Select(start, length);
        editor.TextArea.Caret.Offset = start + length;
    }

    public void ReplaceText(int start, int length, string replacement)
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor == null) return;
        editor.Focus();
        ScrollOffsetIntoView(editor, start);
        editor.Document.Replace(start, length, replacement);
        editor.Select(start, replacement.Length);
        editor.TextArea.Caret.Offset = start + replacement.Length;
    }

    public int LineCount
    {
        get
        {
            var editor = this.FindControl<TextEditor>("TextEditor");
            return editor?.Document?.LineCount ?? 1;
        }
    }

    public void GoToLine(int lineNumber)
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        if (editor?.Document == null) return;

        var clampedLine = Math.Clamp(lineNumber, 1, editor.Document.LineCount);
        var line = editor.Document.GetLineByNumber(clampedLine);
        editor.Focus();
        editor.Select(line.Offset, 0);
        editor.TextArea.Caret.Offset = line.Offset;
        editor.ScrollToLine(clampedLine);
    }

    private static void ScrollOffsetIntoView(TextEditor editor, int offset)
    {
        if (editor.Document == null)
            return;

        var clampedOffset = Math.Clamp(offset, 0, editor.Document.TextLength);
        var line = editor.Document.GetLineByOffset(clampedOffset);
        editor.ScrollToLine(line.LineNumber);
    }

    public void Undo()
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        editor?.Undo();
    }

    public void Redo()
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        editor?.Redo();
    }

    public void SelectAll()
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        editor?.SelectAll();
    }

    public void Cut()
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        editor?.Cut();
    }

    public void Copy()
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        editor?.Copy();
    }

    public void Paste()
    {
        var editor = this.FindControl<TextEditor>("TextEditor");
        editor?.Paste();
    }

    private void UndoClick(object? sender, RoutedEventArgs e) => Undo();
    private void RedoClick(object? sender, RoutedEventArgs e) => Redo();
    private void CutClick(object? sender, RoutedEventArgs e) => Cut();
    private void CopyClick(object? sender, RoutedEventArgs e) => Copy();
    private void PasteClick(object? sender, RoutedEventArgs e) => Paste();
    private void SelectAllClick(object? sender, RoutedEventArgs e) => SelectAll();

    private MainWindowViewModel? GetMainViewModel()
    {
        var window = TopLevel.GetTopLevel(this) as Avalonia.Controls.Window;
        return window?.DataContext as MainWindowViewModel;
    }
}