using System.Text.RegularExpressions;

namespace JoyZoning.App.Services;

public static class AnsiStrip
{
    private static readonly Regex EscapeCodes = new(
        @"\x1b\[[0-9;?]*[a-zA-Z]|\x1b\][^\x07]*\x07|\x1b[@-Z\\-_]",
        RegexOptions.Compiled);

    public static string Strip(string text) =>
        string.IsNullOrEmpty(text) ? text : EscapeCodes.Replace(text, "");
}
