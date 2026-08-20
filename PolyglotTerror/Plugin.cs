using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiToolKit;
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
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("PolyglotTerror");
    private readonly AddonInspector inspector = new();
    private readonly TooltipForensics forensics;
    private readonly ConfigWindow configWindow;
    private readonly CastBarDecorator castBars;
    private readonly ItemTooltipDecorator itemTooltips;
    private readonly ActionTooltipDecorator actionTooltips;
    private readonly KamiTooltipProbe kamiProbe;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Migrate();

        forensics = new TooltipForensics();

        configWindow = new ConfigWindow(this);
        windowSystem.AddWindow(configWindow);

        castBars = new CastBarDecorator(Configuration, Names);
        RegisterCastBars();

        itemTooltips = new ItemTooltipDecorator(Configuration, Names, inspector, forensics);
        actionTooltips = new ActionTooltipDecorator(Configuration, Names);

        // THROWAWAY SPIKE - "/polyglot kami" toggles it. Off by default.
        KamiToolKitLibrary.Initialize(PluginInterface);
        kamiProbe = new KamiTooltipProbe(Configuration, Names);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open settings. \"/polyglot nodes <AddonName>\" logs an addon's node tree, \"/polyglot dump item\" logs the next item tooltip, \"/polyglot kami\" toggles the KamiToolKit tooltip spike.",
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
        itemTooltips.Dispose();
        actionTooltips.Dispose();
        kamiProbe.Dispose();
        KamiToolKitLibrary.Dispose();
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

        if (parts.Length >= 1 && parts[0] == "kami")
        {
            var wanted = !kamiProbe.Enabled;
            Framework.RunOnFrameworkThread(() =>
            {
                kamiProbe.SetEnabled(wanted);
                kamiProbe.ArmLog();

                // Both would add the same names, so only one owns the tooltip at a time.
                itemTooltips.Suspended = wanted;
                Log.Information($"Kami tooltip spike: {(wanted ? "on" : "off")}");
            });
            return;
        }

        if (parts.Length == 2 && parts[0] == "dump" && parts[1] == "item")
        {
            itemTooltips.ArmDump();
            Log.Information("Armed - hover an item to dump its tooltip.");
            return;
        }

        OpenConfig();
    }
}
