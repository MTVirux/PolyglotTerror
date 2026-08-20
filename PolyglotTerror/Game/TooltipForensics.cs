using System;
using System.IO;

namespace PolyglotTerror.Game;

/// <summary>
/// Writes tooltip geometry to its own file, flushed per line. Dalamud's log is capped and stops
/// recording long before a session ends, so a crash leaves nothing behind there.
/// </summary>
public sealed class TooltipForensics : IDisposable
{
    private readonly StreamWriter? writer;

    public TooltipForensics()
    {
        try
        {
            var directory = Plugin.PluginInterface.ConfigDirectory;
            directory.Create();

            var path = Path.Combine(directory.FullName, "tooltip-forensics.log");
            writer = new StreamWriter(path, append: false) { AutoFlush = true };
            writer.WriteLine($"--- session started {DateTime.Now:HH:mm:ss} ---");
        }
        catch (Exception)
        {
            // Diagnostics must never be the reason the plugin fails to load.
        }
    }

    public void Write(string line)
    {
        try
        {
            writer?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {line}");
        }
        catch (Exception)
        {
            // A diagnostic that throws would be worse than one that misses a line.
        }
    }

    public void Dispose() => writer?.Dispose();
}
