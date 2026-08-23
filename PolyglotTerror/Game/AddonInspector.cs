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

        var root = unit->RootNode;
        var rootSize = root == null ? "none" : $"{root->Width}x{root->Height}";
        Plugin.Log.Information(
            $"Addon {addonName}: {unit->UldManager.NodeListCount} nodes, visible={unit->IsVisible}, root={rootSize}, " +
            $"scale={unit->Scale}, build={BuildStamp()}");

        DumpList(&unit->UldManager, 0);
    }

    /// <summary>
    /// Dumps one node list, stepping into component nodes. A component keeps its own list, so the
    /// pieces that actually draw a window's frame never appear in the addon's.
    /// </summary>
    private void DumpList(AtkUldManager* manager, int depth)
    {
        if (manager == null || manager->NodeList == null || depth > 3)
            return;

        var indent = new string(' ', (depth + 1) * 2);

        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (node == null)
                continue;

            var visible = node->NodeFlags.HasFlag(NodeFlags.Visible);
            var parent = node->ParentNode;
            var box = $"parent={(parent == null ? 0 : parent->NodeId)} x={node->X} y={node->Y} {node->Width}x{node->Height}";

            if (node->Type == NodeType.Text)
            {
                var text = (AtkTextNode*)node;
                Plugin.Log.Information(
                    $"{indent}[{i}] id={node->NodeId} Text visible={visible} {box} flags={text->TextFlags} " +
                    $"lineSpacing={text->LineSpacing} fontSize={text->FontSize} \"{text->NodeText}\"");
            }
            else
            {
                Plugin.Log.Information($"{indent}[{i}] id={node->NodeId} {node->Type} visible={visible} {box}");
            }

            if ((ushort)node->Type < 1000)
                continue;

            var component = ((AtkComponentNode*)node)->Component;
            if (component != null)
                DumpList(&component->UldManager, depth + 1);
        }
    }

    /// <summary>
    /// When the loaded plugin is older than the source, every other number here is misleading.
    /// </summary>
    private static string BuildStamp()
    {
        try
        {
            // Read the file fresh - FileInfo caches its timestamps, which reported a stale build.
            var path = Plugin.PluginInterface.AssemblyLocation.FullName;
            var written = System.IO.File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss");
            return $"{written} ({path})";
        }
        catch (System.Exception)
        {
            return "unknown";
        }
    }
}
