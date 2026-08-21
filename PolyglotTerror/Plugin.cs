using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using PolyglotTerror.Game;
using PolyglotTerror.Windows;

namespace PolyglotTerror;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/polyglot";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("PolyglotTerror");
    private readonly AddonInspector inspector = new();
    private readonly TooltipForensics forensics;
    private readonly ConfigWindow configWindow;
    private readonly NamePanelWindow namePanel = new();
    private readonly CastBarDecorator castBars;
    private readonly ActionTooltipDecorator actionTooltips;
    private readonly ItemTooltipNames itemNames;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Migrate();

        forensics = new TooltipForensics();
        inspector.AlsoWriteTo(forensics);

        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(configWindow);
        windowSystem.AddWindow(namePanel);

        castBars = new CastBarDecorator(Configuration, Names);
        RegisterCastBars();

        actionTooltips = new ActionTooltipDecorator(Configuration, Names, forensics);

        itemNames = new ItemTooltipNames(Configuration, Names, forensics, inspector, namePanel);

        itemNames.SetEnabled(true);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open settings. \"/polyglot nodes <AddonName>\" logs an addon's node tree, \"/polyglot dump item\" logs the next item tooltip's, \"/polyglot panel\" toggles the tooltip name panel.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
    }

    public Configuration Configuration { get; }

    public NameCatalog Names { get; } = new();

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        CommandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
        configWindow.Dispose();
        castBars.Dispose();
        actionTooltips.Dispose();
        itemNames.Dispose();
        forensics.Dispose();
    }

    private void RegisterCastBars()
    {
        if (Configuration.DecorateOwnCastBar)
            castBars.Register(new CastBarSurface("_CastBar", 0, CastSource.Self, LanguagePolicy.FullStack));

        if (Configuration.DecorateTargetBars)
        {
            castBars.Register(new CastBarSurface("_TargetInfo", 12, CastSource.Target, LanguagePolicy.FullStack));
            castBars.Register(new CastBarSurface("_TargetInfoCastBar", 4, CastSource.Target, LanguagePolicy.FullStack));
            castBars.Register(new CastBarSurface("_FocusTargetInfo", 5, CastSource.FocusTarget, LanguagePolicy.FullStack));
        }

        if (Configuration.DecorateOverheadBars)
            castBars.RegisterOverheadBars();

        if (Configuration.DecoratePartyList)
            castBars.RegisterPartyList();
    }

    private void OpenConfig() => configWindow.IsOpen = true;

    private void OnCommand(string command, string arguments)
    {
        var parts = arguments.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && parts[0] == "nodes")
        {
            inspector.DumpNodes(parts[1]);
            return;
        }

        if (parts.Length == 2 && parts[0] == "dump" && parts[1] == "item")
        {
            itemNames.ArmDump();
            Chat.Print("PolyglotTerror: armed - hover an item to dump its tooltip.");
            return;
        }

        if (parts.Length >= 1 && (parts[0] == "panel" || parts[0] == "kami"))
        {
            itemNames.SetEnabled(!itemNames.Enabled);

            var state = itemNames.Enabled ? "enabled" : "disabled";
            Chat.Print($"PolyglotTerror: tooltip name panel {state}.");
            Log.Information($"Tooltip name panel {state}.");
            return;
        }

        OpenConfig();
    }
}
