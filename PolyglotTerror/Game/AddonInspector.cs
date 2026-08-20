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
