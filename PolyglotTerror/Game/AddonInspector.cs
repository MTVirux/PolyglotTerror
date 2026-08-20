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

        var count = unit->UldManager.NodeListCount;
        var root = unit->RootNode;
        var rootSize = root == null ? "none" : $"{root->Width}x{root->Height}";
        Report(
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
                Report(
                    $"  [{i}] id={node->NodeId} Text visible={visible} {box} flags={text->TextFlags} " +
                    $"lineSpacing={text->LineSpacing} fontSize={text->FontSize} \"{text->NodeText}\"");
            }
            else
            {
                Report(
                    $"  [{i}] id={node->NodeId} {node->Type} visible={visible} {box}{Texture(node)}");
            }
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
                offsets = $" offsets={grid->TopOffset},{grid->RightOffset},{grid->BottomOffset},{grid->LeftOffset}";
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
        var rect = $" part={part->U},{part->V} {part->Width}x{part->Height}";
        var asset = part->UldAsset;
        if (asset == null)
            return offsets + rect;

        var resource = asset->AtkTexture.Resource;
        if (resource == null || resource->TexFileResourceHandle == null)
            return offsets + rect;

        var path = resource->TexFileResourceHandle->ResourceHandle.FileName.ToString();
        return $"{offsets}{rect} tex=\"{path}\"";
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
