using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Noopad.Services;

public class FileDialogService : IFileDialogService
{
    private readonly Window _owner;

    public FileDialogService(Window owner)
    {
        _owner = owner;
    }

    public async Task<string?> OpenFileAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            AllowMultiple = false
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveFileAsync(string? currentPath, string? suggestedName)
    {
        var options = new FilePickerSaveOptions
        {
            Title = "Save File",
            SuggestedFileName = suggestedName ?? Path.GetFileName(currentPath) ?? "untitled.txt"
        };

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var directory = Path.GetDirectoryName(currentPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                options.SuggestedStartLocation = await _owner.StorageProvider.TryGetFolderFromPathAsync(directory);
        }

        var file = await _owner.StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }
}
