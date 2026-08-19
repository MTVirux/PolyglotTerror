using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PolyglotTerror.Game;

/// <summary>
/// Writes text into game text nodes. The game keeps the pointer handed to SetText,
/// so each node's buffer stays alive until its replacement is installed.
/// </summary>
public sealed unsafe class NodeTextWriter : IDisposable
{
    private readonly Dictionary<nint, nint> buffers = new();

    public void Write(AtkTextNode* node, string text)
    {
        if (node == null)
            return;

        var key = (nint)node;
        var bytes = Encoding.UTF8.GetBytes(text + "\0");
        var buffer = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, buffer, bytes.Length);

        node->SetText((byte*)buffer);

        if (buffers.TryGetValue(key, out var previous))
            Marshal.FreeHGlobal(previous);

        buffers[key] = buffer;
    }

    public void Forget(nint node)
    {
        if (buffers.Remove(node, out var buffer))
            Marshal.FreeHGlobal(buffer);
    }

    public void Dispose()
    {
        foreach (var buffer in buffers.Values)
            Marshal.FreeHGlobal(buffer);

        buffers.Clear();
    }
}
