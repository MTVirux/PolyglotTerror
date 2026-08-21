using FFXIVClientStructs.FFXIV.Component.GUI;

namespace PolyglotTerror.Game;

/// <summary>
/// Logs what a live addon actually contains, so node ids and string array slots can be
/// found from the game itself instead of being guessed.
/// </summary>
public sealed unsafe class AddonInspector
{
    private TooltipForensics? forensics;

    /// <summary>
    /// Sends the dump to the plugin's own file as well. Dalamud's log stops recording once it hits
    /// its size cap, which a long session reaches easily.
    /// </summary>
    public void AlsoWriteTo(TooltipForensics writer) => forensics = writer;

    private void Report(string line)
    {
        Plugin.Log.Information(line);
        forensics?.Write(line);
    }

    public void DumpNodes(string addonName)
    {
        var unit = (AtkUnitBase*)(nint)Plugin.GameGui.GetAddonByName(addonName, 1);
        if (unit == null)
        {
            Report($"Addon {addonName} is not open.");
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
        Report(
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
                Report(
                    $"{indent}[{i}] id={node->NodeId} Text visible={visible} {box} flags={text->TextFlags} " +
                    $"lineSpacing={text->LineSpacing} fontSize={text->FontSize} \"{text->NodeText}\"");
            }
            else
            {
                Report($"{indent}[{i}] id={node->NodeId} {node->Type} visible={visible} {box}{Texture(node)}");
            }

            if ((ushort)node->Type < 1000)
                continue;

            var component = ((AtkComponentNode*)node)->Component;
            if (component != null)
                DumpList(&component->UldManager, depth + 1);
        }
    }

    /// <summary>
    /// The texture a nine grid or image draws, so its look can be reproduced on a node of our own
    /// rather than guessed at from a path that may not exist.
    /// </summary>
    private static string Texture(AtkResNode* node)
    {
        AtkUldPartsList* list;
        uint partId;
        var offsets = string.Empty;

        switch (node->Type)
        {
            case NodeType.NineGrid:
                var grid = (AtkNineGridNode*)node;
                list = grid->PartsList;
                partId = grid->PartId;
                offsets =
                    $" offsets={grid->TopOffset},{grid->RightOffset},{grid->BottomOffset},{grid->LeftOffset}" +
                    $" render={grid->PartsTypeRenderType} blend={grid->BlendMode}";
                break;

            case NodeType.Image:
                var image = (AtkImageNode*)node;
                list = image->PartsList;
                partId = image->PartId;
                break;

            default:
                return string.Empty;
        }

        if (list == null || partId >= list->PartCount)
            return offsets;

        var part = &list->Parts[partId];
        var rect = $" partId={partId} part={part->U},{part->V} {part->Width}x{part->Height}";

        // Every part in the list, not just the one this node points at - a nine grid either slices
        // a single part with its offsets or draws nine of them, and only the rects tell us which.
        var all = $" parts[{list->PartCount}]=";
        for (var i = 0; i < list->PartCount && i < 12; i++)
        {
            var entry = &list->Parts[i];
            all += $"({entry->U},{entry->V} {entry->Width}x{entry->Height})";
        }

        var asset = part->UldAsset;
        if (asset == null)
            return offsets + rect + all;

        var resource = asset->AtkTexture.Resource;
        if (resource == null || resource->TexFileResourceHandle == null)
            return offsets + rect + all;

        var path = resource->TexFileResourceHandle->ResourceHandle.FileName.ToString();
        return $"{offsets}{rect}{all} tex=\"{path}\"";
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
