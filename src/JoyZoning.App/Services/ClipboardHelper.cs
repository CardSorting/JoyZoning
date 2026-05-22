using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;

namespace JoyZoning.App.Services;

public static class ClipboardHelper
{
    public static string ReportFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JoyZoning",
        "health-report.txt");

    public static async Task SetTextAsync(TopLevel? topLevel, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportFilePath)!);
        await File.WriteAllTextAsync(ReportFilePath, text);

        if (topLevel?.Clipboard is { } clipboard)
        {
            var setText = clipboard.GetType().GetMethod(
                "SetTextAsync",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                new[] { typeof(string) },
                modifiers: null);

            if (setText is not null)
            {
                await (Task)setText.Invoke(clipboard, new object?[] { text })!;
                return;
            }

            try
            {
                var format = DataFormat.CreateStringApplicationFormat("text/plain");
                var item = new DataTransferItem();
                item.Set(format, text);
                var transfer = new DataTransfer();
                transfer.Add(item);
                await clipboard.SetDataAsync(transfer);
                return;
            }
            catch
            {
                // fall through to OS helper
            }
        }

        if (OperatingSystem.IsMacOS())
            await MacPasteboardCopyAsync(text);
    }

    private static async Task MacPasteboardCopyAsync(string text)
    {
        var psi = new ProcessStartInfo("/usr/bin/pbcopy")
        {
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi);
        if (proc is null) return;

        await proc.StandardInput.WriteAsync(text);
        await proc.StandardInput.FlushAsync();
        proc.StandardInput.Close();
        await proc.WaitForExitAsync();
    }
}
