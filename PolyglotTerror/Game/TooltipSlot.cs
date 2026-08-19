using System;
using System.Text;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;

namespace PolyglotTerror.Game;

/// <summary>
/// One entry inside a tooltip's backing string array. The entry is found by its value rather than
/// by a fixed index, so a patch that reshuffles the array leaves the tooltip undecorated instead
/// of corrupted.
/// </summary>
internal sealed unsafe class TooltipSlot
{
    private int index = -1;
    private uint subjectId;
    private byte[] original = [];
    private string originalText = string.Empty;
    private byte[] written = [];

    /// <summary>
    /// Appends the extra lines <paramref name="compose"/> builds from the entry's own text.
    /// </summary>
    public void Decorate(StringArrayData* data, uint subject, string clientText, Func<string, string?> compose)
    {
        var expected = Strip(clientText);
        if (expected.Length == 0)
            return;

        var slot = Locate(data, subject, expected);
        if (slot < 0)
            return;

        var head = Strip(originalText);
        var composed = compose(head);
        if (composed is null || !composed.StartsWith(head, StringComparison.Ordinal))
            return;

        // Only the tail is built from text; the game's own bytes are copied verbatim so any
        // SeString payloads inside them survive the rewrite.
        var tail = Encoding.UTF8.GetBytes(composed[head.Length..]);
        var value = new byte[original.Length + tail.Length + 1];
        original.CopyTo(value, 0);
        tail.CopyTo(value, original.Length);

        data->SetValue(slot, value.AsSpan(), readBeforeWrite: false, managed: true, suppressUpdates: true);
        written = value[..^1];
    }

    private int Locate(StringArrayData* data, uint subject, string expected)
    {
        if (index >= 0 && index < data->Size && Claim(data, index, subject, expected))
            return index;

        for (var i = 0; i < data->Size; i++)
        {
            if (!Claim(data, i, subject, expected))
                continue;

            index = i;
            return i;
        }

        index = -1;
        return -1;
    }

    private bool Claim(StringArrayData* data, int candidate, uint subject, string expected)
    {
        var pointer = data->StringArray[candidate];
        if (!pointer.HasValue)
            return false;

        var raw = pointer.AsSpan();

        // An entry we already rewrote no longer holds the plain text, so recognise our own output.
        if (subjectId == subject && written.Length > 0 && raw.SequenceEqual(written))
            return true;

        var text = new ReadOnlySeStringSpan(raw).ExtractText();
        if (!string.Equals(Strip(text), expected, StringComparison.Ordinal))
            return false;

        subjectId = subject;
        original = raw.ToArray();
        originalText = text;
        written = [];
        return true;
    }

    // High quality and collectable tooltips append an icon glyph after the name.
    private static string Strip(string text)
    {
        var end = text.Length;
        while (end > 0 && IsTrailingDecoration(text[end - 1]))
            end--;

        var start = 0;
        while (start < end && char.IsWhiteSpace(text[start]))
            start++;

        return text[start..end];
    }

    private static bool IsTrailingDecoration(char value)
        => char.IsWhiteSpace(value) || value is >= '\ue000' and <= '\uf8ff';
}
