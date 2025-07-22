using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using MapRuleLibrary.State;

namespace BugFixes;

[BepInPlugin(pluginGUID, pluginName, pluginVersion)]
public class BugFixPlugin : BaseUnityPlugin
{
    const string pluginGUID = "com.gummyboars.gloomhaven.bugfixes";
    const string pluginName = "Bug Fixes";
    const string pluginVersion = "0.0.1";

    private readonly Harmony HarmonyInstance = new Harmony(pluginGUID);

    public static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(pluginName);
        
    private void Awake()
    {
        // Plugin startup logic
        BugFixPlugin.logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        Assembly assembly = Assembly.GetExecutingAssembly();
        HarmonyInstance.PatchAll(assembly);
    }
}

[HarmonyPatch(typeof(CMapState), nameof(CMapState.OnMapLoaded))]
public static class Patch_Map_OnMapLoaded
{
    private static void Prefix(bool isJoiningMPClient)
    {
        BugFixPlugin.logger.LogInfo($"OnMapLoaded called with {isJoiningMPClient}");
    }

    private static void Postfix()
    {
        BugFixPlugin.logger.LogInfo("OnMapLoaded done");
    }
}
