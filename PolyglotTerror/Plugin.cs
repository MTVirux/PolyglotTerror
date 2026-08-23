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
    [PluginService] internal static ISeStringEvaluator SeStringEvaluator { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("PolyglotTerror");
    private readonly AddonInspector inspector = new();
    private readonly ConfigWindow configWindow;
    private readonly NamePanelWindow itemPanel = new("PolyglotItemNames");
    private readonly NamePanelWindow actionPanel = new("PolyglotActionNames");
    private readonly CastBarDecorator castBars;
    private readonly ActionTooltipNames actionNames;
    private readonly ItemTooltipNames itemNames;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Migrate(NameCatalog.FromClientLanguage(ClientState.ClientLanguage));

        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(configWindow);
        windowSystem.AddWindow(itemPanel);
        windowSystem.AddWindow(actionPanel);

        castBars = new CastBarDecorator(Configuration, Names);
        RegisterCastBars();

        itemNames = new ItemTooltipNames(Configuration, Names, inspector, itemPanel);
        actionNames = new ActionTooltipNames(Configuration, Names, actionPanel);

        itemNames.SetEnabled(true);
        actionNames.SetEnabled(true);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open settings. \"/polyglot nodes <AddonName>\" logs an addon's node tree, \"/polyglot dump item\" logs the next item tooltip's, \"/polyglot panel\" toggles the tooltip name panels.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;

        ClientState.Login += Names.Clear;
        ClientState.ClassJobChanged += OnClassJobChanged;
        ClientState.LevelChanged += OnLevelChanged;
    }

    public Configuration Configuration { get; }

    public NameCatalog Names { get; } = new();

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        ClientState.Login -= Names.Clear;
        ClientState.ClassJobChanged -= OnClassJobChanged;
        ClientState.LevelChanged -= OnLevelChanged;
        CommandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
        castBars.Dispose();
        actionNames.Dispose();
        itemNames.Dispose();
    }

    // Descriptions are resolved against the player's job and level, so they stop being true the
    // moment either changes.
    private void OnClassJobChanged(uint classJobId) => Names.Clear();

    private void OnLevelChanged(uint classJobId, uint level) => Names.Clear();

    private void RegisterCastBars()
    {
        if (Configuration.DecorateOwnCastBar)
            castBars.Register(new CastBarSurface("_CastBar", 0, CastSource.Self));

        if (Configuration.DecorateTargetBars)
        {
            castBars.Register(new CastBarSurface("_TargetInfo", 12, CastSource.Target));
            castBars.Register(new CastBarSurface("_TargetInfoCastBar", 4, CastSource.Target));
            castBars.Register(new CastBarSurface("_FocusTargetInfo", 5, CastSource.FocusTarget));
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
            var wanted = !itemNames.Enabled;
            itemNames.SetEnabled(wanted);
            actionNames.SetEnabled(wanted);

            var state = wanted ? "enabled" : "disabled";
            Chat.Print($"PolyglotTerror: tooltip name panels {state}.");
            Log.Information($"Tooltip name panels {state}.");
            return;
        }

        OpenConfig();
    }
}
