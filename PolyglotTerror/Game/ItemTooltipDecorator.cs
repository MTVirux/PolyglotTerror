using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;
using PolyglotTerror.Core;

namespace PolyglotTerror.Game;

/// <summary>
/// Appends extra language lines to the item tooltip's backing string array before the game
/// lays the window out, so the game grows the tooltip around the taller text by itself.
/// </summary>
public sealed unsafe class ItemTooltipDecorator : IDisposable
{
    private const string AddonName = "ItemDetail";

    private readonly Configuration config;
    private readonly NameCatalog names;
    private readonly Slot nameSlot = new();
    private readonly Slot categorySlot = new();
    private readonly Slot descriptionSlot = new();

    public ItemTooltipDecorator(Configuration config, NameCatalog names)
    {
        this.config = config;
        this.names = names;

        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);
    }

    public void Dispose()
        => Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, AddonName, OnPreRequestedUpdate);

    private void OnPreRequestedUpdate(AddonEvent type, AddonArgs args)
    {
        if (!config.DecorateTooltip || args is not AddonRequestedUpdateArgs update)
            return;

        var itemId = (uint)Plugin.GameGui.HoveredItem;
        if (itemId == 0)
            return;

        var arrays = (StringArrayData**)update.StringArrayData;
        if (arrays == null)
            return;

        var data = arrays[(int)StringArrayType.ItemDetail];
        if (data == null || data->StringArray == null)
            return;

        var client = names.GetItem(NameCatalog.FromClientLanguage(Plugin.ClientState.ClientLanguage), itemId);
        if (client.Name is null)
            return;

        if (config.ShowItemName)
            Decorate(data, nameSlot, itemId, client.Name, static item => item.Name);

        if (config.ShowItemCategory && client.Category is not null)
            Decorate(data, categorySlot, itemId, client.Category, static item => item.Category);

        if (config.ShowItemDescription && client.Description is not null)
            Decorate(data, descriptionSlot, itemId, client.Description, static item => item.Description);
    }

    private void Decorate(StringArrayData* data, Slot slot, uint itemId, string clientText, Func<ItemNames, string?> pick)
    {
        var expected = Strip(clientText);
        if (expected.Length == 0)
            return;

        var index = Locate(data, slot, itemId, expected);
        if (index < 0)
            return;

        var candidates = new Dictionary<GameLanguage, string?>();
        foreach (var entry in config.Languages)
        {
            if (entry.Enabled)
                candidates[entry.Language] = pick(names.GetItem(entry.Language, itemId));
        }

        // Compose against the stripped text so an HQ glyph cannot stop a language matching
        // the line that is already there, then splice onto the untouched original bytes.
        var head = Strip(slot.OriginalText);
        var composed = LineComposer.Compose(head, candidates, config.Languages, config.HideDuplicates);
        if (composed is null || !composed.StartsWith(head, StringComparison.Ordinal))
            return;

        // Only the tail is built from text; the game's own bytes are copied verbatim so any
        // SeString payloads inside them survive the rewrite.
        var tail = Encoding.UTF8.GetBytes(composed[head.Length..]);
        var value = new byte[slot.Original.Length + tail.Length + 1];
        slot.Original.CopyTo(value, 0);
        tail.CopyTo(value, slot.Original.Length);

        data->SetValue(index, value.AsSpan(), readBeforeWrite: false, managed: true, suppressUpdates: true);
        slot.Written = value[..^1];
    }

    /// <summary>
    /// Finds the entry by its value rather than by a fixed index, so a patch that reshuffles
    /// the array leaves the tooltip undecorated instead of corrupted.
    /// </summary>
    private static int Locate(StringArrayData* data, Slot slot, uint itemId, string expected)
    {
        if (slot.Index >= 0 && slot.Index < data->Size && Claim(data, slot, slot.Index, itemId, expected))
            return slot.Index;

        for (var i = 0; i < data->Size; i++)
        {
            if (!Claim(data, slot, i, itemId, expected))
                continue;

            slot.Index = i;
            return i;
        }

        slot.Index = -1;
        return -1;
    }

    private static bool Claim(StringArrayData* data, Slot slot, int index, uint itemId, string expected)
    {
        var pointer = data->StringArray[index];
        if (!pointer.HasValue)
            return false;

        var raw = pointer.AsSpan();

        // An entry we already rewrote no longer holds the plain text, so recognise our own output.
        if (slot.ItemId == itemId && slot.Written.Length > 0 && raw.SequenceEqual(slot.Written))
            return true;

        var text = new ReadOnlySeStringSpan(raw).ExtractText();
        if (!string.Equals(Strip(text), expected, StringComparison.Ordinal))
            return false;

        slot.ItemId = itemId;
        slot.Original = raw.ToArray();
        slot.OriginalText = text;
        slot.Written = [];
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

    private sealed class Slot
    {
        public int Index = -1;

        public uint ItemId;

        public byte[] Original = [];

        public string OriginalText = string.Empty;

        public byte[] Written = [];
    }
}
