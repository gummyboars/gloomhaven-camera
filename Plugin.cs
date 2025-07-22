using BepInEx;
using BepInEx.Logging;

namespace BugFixes;

[BepInPlugin("com.gummyboars.gloomhaven.bugfixes", "Bug Fixes", "0.0.1")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
        
    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }
}
