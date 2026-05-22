using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace JoyZoning.App.Services;

public static class FolderPickerHelper
{
    public static async Task<string?> PickFolderAsync(Window owner, string title, string? suggestedPath = null)
    {
        var storage = owner.StorageProvider;
        if (!storage.CanPickFolder)
            return null;

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        };

        if (!string.IsNullOrWhiteSpace(suggestedPath) && Directory.Exists(suggestedPath))
            options.SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(suggestedPath);

        var folders = await storage.OpenFolderPickerAsync(options);
        if (folders.Count == 0)
            return null;

        return folders[0].TryGetLocalPath();
    }
}
