using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Noopad.Views;

public partial class GoToLineDialog : Window
{
    private readonly int _maxLine;

    public GoToLineDialog() : this(1, 1) { }

    public GoToLineDialog(int maxLine, int currentLine)
    {
        InitializeComponent();
        _maxLine = Math.Max(1, maxLine);

        var prompt = this.FindControl<TextBlock>("PromptText");
        if (prompt != null)
            prompt.Text = $"Enter a line number (1 - {_maxLine}):";

        var lineBox = this.FindControl<TextBox>("LineBox");
        if (lineBox != null)
        {
            lineBox.Text = Math.Clamp(currentLine, 1, _maxLine).ToString();
            lineBox.KeyDown += OnLineBoxKeyDown;
            Opened += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                lineBox.Focus();
                lineBox.SelectAll();
            });
        }

        var goBtn = this.FindControl<Button>("GoBtn");
        var cancelBtn = this.FindControl<Button>("CancelBtn");

        if (goBtn != null) goBtn.Click += (_, _) => Submit();
        if (cancelBtn != null) cancelBtn.Click += (_, _) => Close(null);
    }

    private void OnLineBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Submit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }

    private void Submit()
    {
        var lineBox = this.FindControl<TextBox>("LineBox");
        var error = this.FindControl<TextBlock>("ErrorText");
        if (lineBox == null)
            return;

        if (int.TryParse(lineBox.Text, out var lineNumber) && lineNumber >= 1 && lineNumber <= _maxLine)
        {
            Close(lineNumber);
            return;
        }

        if (error != null)
            error.Text = $"Enter a number from 1 to {_maxLine}.";
    }
}
