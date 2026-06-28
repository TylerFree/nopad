using Avalonia.Controls;

namespace Noopad.Views;

public partial class ReloadFileDialog : Window
{
    public ReloadFileDialog() : this("Untitled", "Untitled", isDirty: false) { }

    public ReloadFileDialog(string fileName, string filePath, bool isDirty)
    {
        InitializeComponent();

        var message = this.FindControl<TextBlock>("MessageText");
        if (message != null)
        {
            var dirtyText = isDirty
                ? " Reloading will discard your unsaved changes."
                : string.Empty;
            message.Text = $"\"{fileName}\" changed on disk.\n\nDo you want to reload it from \"{filePath}\"?{dirtyText}";
        }

        var reloadBtn = this.FindControl<Button>("ReloadBtn");
        var keepBtn = this.FindControl<Button>("KeepBtn");

        if (reloadBtn != null) reloadBtn.Click += (_, _) => Close(true);
        if (keepBtn != null) keepBtn.Click += (_, _) => Close(false);
    }
}
