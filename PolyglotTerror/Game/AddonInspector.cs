using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PolyglotTerror.Game;

/// <summary>
/// Logs what a live addon actually contains, so node ids and string array slots can be
/// found from the game itself instead of being guessed.
/// </summary>
public sealed unsafe class AddonInspector
{
    public void DumpNodes(string addonName)
    {
        var unit = (AtkUnitBase*)(nint)Plugin.GameGui.GetAddonByName(addonName, 1);
        if (unit == null)
        {
            Plugin.Log.Information($"Addon {addonName} is not open.");
            return;
        }

        DumpNodes(addonName, unit);
    }

    public void DumpNodes(string addonName, AtkUnitBase* unit)
    {
        if (unit == null)
            return;

        var count = unit->UldManager.NodeListCount;
        var root = unit->RootNode;
        var rootSize = root == null ? "none" : $"{root->Width}x{root->Height}";
        Plugin.Log.Information(
            $"Addon {addonName}: {count} nodes, visible={unit->IsVisible}, root={rootSize}, " +
            $"scale={unit->Scale}, build={BuildStamp()}");

        for (var i = 0; i < count; i++)
        {
            var node = unit->UldManager.NodeList[i];
            if (node == null)
                continue;

            var visible = node->NodeFlags.HasFlag(NodeFlags.Visible);
            var parent = node->ParentNode;
            var box = $"parent={(parent == null ? 0 : parent->NodeId)} x={node->X} y={node->Y} {node->Width}x{node->Height}";

            if (node->Type == NodeType.Text)
            {
                var text = (AtkTextNode*)node;
                Plugin.Log.Information(
                    $"  [{i}] id={node->NodeId} Text visible={visible} {box} flags={text->TextFlags} " +
                    $"lineSpacing={text->LineSpacing} fontSize={text->FontSize} \"{text->NodeText}\"");
            }
            else
            {
                Plugin.Log.Information($"  [{i}] id={node->NodeId} {node->Type} visible={visible} {box}");
            }
        }
    }

    /// <summary>
    /// When the loaded plugin is older than the source, every other number here is misleading.
    /// </summary>
    private static string BuildStamp()
    {
        try
        {
            return Plugin.PluginInterface.AssemblyLocation.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (System.Exception)
        {
            return "unknown";
        }
    }

    public void DumpTooltipStrings(StringArrayData* strings)
    {
        if (strings == null)
        {
            Plugin.Log.Information("String array is null.");
            return;
        }

        Plugin.Log.Information($"String array: {strings->Size} entries");

        for (var i = 0; i < strings->Size; i++)
        {
            var entry = strings->StringArray[i];
            if (!entry.HasValue)
                continue;

            var text = entry.ToString();
            if (string.IsNullOrEmpty(text))
                continue;

            Plugin.Log.Information($"  [{i}] \"{text}\"");
        }
    }
}
