namespace JoyZoning.Cli.Tui;

/// <summary>TTY line editor with history, tab completion, multiline continuation, and $EDITOR.</summary>
public sealed class TerminalLineReader
{
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    private readonly Func<string, IReadOnlyList<string>>? _complete;
    private readonly string _historyPath;

    public TerminalLineReader(Func<string, IReadOnlyList<string>>? complete = null)
    {
        _complete = complete;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".joyzoning");
        Directory.CreateDirectory(dir);
        _historyPath = Path.Combine(dir, "cli_history");
        LoadHistory();
    }

    public string? ReadLine(string prompt, CancellationToken cancellationToken = default)
    {
        if (!Console.IsInputRedirected)
            return ReadLineInteractive(prompt, cancellationToken);

        var line = Console.ReadLine();
        if (line is not null)
            AppendHistory(line);
        return line;
    }

    private string? ReadLineInteractive(string prompt, CancellationToken cancellationToken)
    {
        var buffer = new List<char>();
        var multiline = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!multiline)
                WritePrompt(prompt, buffer);

            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                if (multiline && buffer.Count == 0)
                {
                    multiline = false;
                    continue;
                }

                var endsWithContinue = buffer.Count > 0 && buffer[^1] == '\\';
                if (endsWithContinue)
                {
                    buffer.RemoveAt(buffer.Count - 1);
                    buffer.Add('\n');
                    multiline = true;
                    Console.WriteLine();
                    continue;
                }

                Console.WriteLine();
                var text = new string(buffer.ToArray()).TrimEnd();
                if (text.Length > 0)
                    AppendHistory(text);
                return text.Length == 0 ? "" : text;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Count > 0)
                    buffer.RemoveAt(buffer.Count - 1);
                continue;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                ApplyTabCompletion(buffer, prompt);
                continue;
            }

            if (key.Key == ConsoleKey.UpArrow)
            {
                RecallHistory(buffer, direction: -1);
                continue;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                RecallHistory(buffer, direction: 1);
                continue;
            }

            if (key is { Modifiers: ConsoleModifiers.Control, Key: ConsoleKey.G })
            {
                var edited = ReadFromEditor(new string(buffer.ToArray()));
                if (edited is not null)
                {
                    buffer.Clear();
                    buffer.AddRange(edited);
                }

                continue;
            }

            if (key.Key == ConsoleKey.Escape)
            {
                buffer.Clear();
                _historyIndex = _history.Count;
                Console.Write("\r" + new string(' ', prompt.Length + 40) + "\r");
                continue;
            }

            if (!char.IsControl(key.KeyChar))
                buffer.Add(key.KeyChar);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return null;
    }

    private void WritePrompt(string prompt, List<char> buffer)
    {
        Console.Write($"\r{prompt}{new string(buffer.ToArray())}");
        if (buffer.Count < Console.WindowWidth - prompt.Length - 1)
            Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - prompt.Length - buffer.Count - 2)));
        Console.Write($"\r{prompt}{new string(buffer.ToArray())}");
    }

    private void ApplyTabCompletion(List<char> buffer, string prompt)
    {
        if (_complete is null)
            return;

        var current = new string(buffer.ToArray());
        var candidates = _complete(current);
        if (candidates.Count == 0)
            return;

        if (candidates.Count == 1)
        {
            buffer.Clear();
            buffer.AddRange(candidates[0]);
            WritePrompt(prompt, buffer);
            return;
        }

        Console.WriteLine();
        foreach (var c in candidates.Take(12))
            Console.WriteLine($"  {c}");
        if (candidates.Count > 12)
            Console.WriteLine($"  … {candidates.Count - 12} more");
    }

    private void RecallHistory(List<char> buffer, int direction)
    {
        if (_history.Count == 0)
            return;

        if (_historyIndex < 0)
            _historyIndex = _history.Count;

        _historyIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count);
        buffer.Clear();
        if (_historyIndex < _history.Count)
            buffer.AddRange(_history[_historyIndex]);
    }

    private void AppendHistory(string line)
    {
        if (_history.Count == 0 || !_history[^1].Equals(line, StringComparison.Ordinal))
            _history.Add(line);
        _historyIndex = _history.Count;
        try
        {
            File.AppendAllLines(_historyPath, [line]);
        }
        catch
        {
            // best-effort
        }
    }

    private void LoadHistory()
    {
        if (!File.Exists(_historyPath))
            return;

        try
        {
            foreach (var line in File.ReadLines(_historyPath).TakeLast(500))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    _history.Add(line);
            }
        }
        catch
        {
            // ignore
        }
    }

    public static string? ReadFromEditor(string initial)
    {
        var editor = Environment.GetEnvironmentVariable("VISUAL")
            ?? Environment.GetEnvironmentVariable("EDITOR")
            ?? (OperatingSystem.IsWindows() ? "notepad" : "nano");
        var tmp = Path.Combine(Path.GetTempPath(), $"jz-edit-{Guid.NewGuid():N}.md");
        File.WriteAllText(tmp, initial);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(editor, tmp)
            {
                UseShellExecute = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit();
            return File.Exists(tmp) ? File.ReadAllText(tmp).TrimEnd() : null;
        }
        catch
        {
            Console.WriteLine("  Could not open $EDITOR. Set EDITOR or VISUAL.");
            return null;
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* ignore */ }
        }
    }
}
