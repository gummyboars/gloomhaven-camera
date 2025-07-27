using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

using MapRuleLibrary.State;
using Gloomhaven;
using GLOOM.MainMenu;
using Script.GUI.SMNavigation.States.MainMenuStates;
using SM.Gamepad;

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

[HarmonyPatch(typeof(CameraController), nameof(CameraController.SetOptimalViewPoint))]
public static class Patch_Camera_SetOptimalViewPoint
{
    private static bool Prefix()
    {
        return false;
    }
}

[HarmonyPatch(typeof(UISubmenuGOWindow), nameof(UISubmenuGOWindow.Show))]
public static class Patch_UISubmenuGOWindow_Show
{
    private static void Postfix()
    {
        BugFixPlugin.logger.LogInfo("************ submenu GO window Show");
        if (Patch_ControlsSettings_Initialize.receiveDamageAction != null && Patch_ControlsSettings_Initialize.toggleSpeedAction != null)
        {
            bool active = Patch_ControlsSettings_Initialize.receiveDamageAction.activeSelf;
            Patch_ControlsSettings_Initialize.toggleSpeedAction.SetActive(active);
            PrintRecursive(Patch_ControlsSettings_Initialize.toggleSpeedAction.transform, 0);
        }
        GameObject rd = GameObject.Find(" - ReceiveDamage Action");
        GameObject ts = GameObject.Find(" - ToggleSpeed Action");
        BugFixPlugin.logger.LogInfo($"  {rd?.name} {rd?.activeSelf}");
        BugFixPlugin.logger.LogInfo($"  {ts?.name} {ts?.activeSelf}");
    }

    private static void PrintRecursive(Transform t, int depth)
    {
        string s = "";
        for (int i = 0; i < depth; i++)
        {
            s += "  ";
        }
        BugFixPlugin.logger.LogInfo($"{s}{t.gameObject}");
        if (t.gameObject.name == "Title")
        {
            MonoBehaviour[] lst = t.gameObject.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour b in lst)
            {
                BugFixPlugin.logger.LogInfo($"  {s}{b} {b.GetType()}");
            }
        }
        foreach(Transform child in t)
        {
            PrintRecursive(child, depth+1);
        }
    }
}

/*
[HarmonyPatch(typeof(MainMenuState), "SetNavigationRootWithoutStateData")]
public static class Patch_MainMenuState_SNR
{
    private static void Prefix()
    {
        BugFixPlugin.logger.LogInfo("************ MainMenuState SNR before");
        GameObject rd = GameObject.Find(" - ReceiveDamage Action");
        GameObject ts = GameObject.Find(" - ToggleSpeed Action");
        BugFixPlugin.logger.LogInfo($"  {rd?.name} {rd?.activeSelf}");
        BugFixPlugin.logger.LogInfo($"  {ts?.name} {ts?.activeSelf}");
    }
    private static void Postfix()
    {
        BugFixPlugin.logger.LogInfo("************ MainMenuState SNR after");
        GameObject rd = GameObject.Find(" - ReceiveDamage Action");
        GameObject ts = GameObject.Find(" - ToggleSpeed Action");
        BugFixPlugin.logger.LogInfo($"  {rd?.name} {rd?.activeSelf}");
        BugFixPlugin.logger.LogInfo($"  {ts?.name} {ts?.activeSelf}");
    }
}

[HarmonyPatch(typeof(MainMenuState), nameof(MainMenuState.EnterWithMainStateData))]
public static class Patch_MainMenuState_EMS
{
    private static void Prefix()
    {
        BugFixPlugin.logger.LogInfo("************ MainMenuState EMS before");
        GameObject rd = GameObject.Find(" - ReceiveDamage Action");
        GameObject ts = GameObject.Find(" - ToggleSpeed Action");
        BugFixPlugin.logger.LogInfo($"  {rd?.name} {rd?.activeSelf}");
        BugFixPlugin.logger.LogInfo($"  {ts?.name} {ts?.activeSelf}");
    }
    private static void Postfix()
    {
        BugFixPlugin.logger.LogInfo("************ MainMenuState EMS after");
        GameObject rd = GameObject.Find(" - ReceiveDamage Action");
        GameObject ts = GameObject.Find(" - ToggleSpeed Action");
        BugFixPlugin.logger.LogInfo($"  {rd?.name} {rd?.activeSelf}");
        BugFixPlugin.logger.LogInfo($"  {ts?.name} {ts?.activeSelf}");
    }
}

[HarmonyPatch(typeof(UiNavigationManager), nameof(UiNavigationManager.SetCurrentRoot))]
public static class Patch_UiNavigationManager_SetCurrentRoot
{
    private static void Prefix()
    {
        BugFixPlugin.logger.LogInfo("########### SetCurrentRoot enter");
        GameObject rd = GameObject.Find(" - ReceiveDamage Action");
        GameObject ts = GameObject.Find(" - ToggleSpeed Action");
        BugFixPlugin.logger.LogInfo($"  {rd?.name} {rd?.activeSelf}");
        BugFixPlugin.logger.LogInfo($"  {ts?.name} {ts?.activeSelf}");
    }

    private static void Postfix()
    {
        BugFixPlugin.logger.LogInfo("########### SetCurrentRoot exit");
        GameObject rd = GameObject.Find(" - ReceiveDamage Action");
        GameObject ts = GameObject.Find(" - ToggleSpeed Action");
        BugFixPlugin.logger.LogInfo($"  {rd?.name} {rd?.activeSelf}");
        BugFixPlugin.logger.LogInfo($"  {ts?.name} {ts?.activeSelf}");
    }
}


[HarmonyPatch(typeof(HotkeyContainer), "UpdateHotkeys")]
public static class Patch_HotkeyContainer_UpdateHotkeys
{
    private static void Prefix(List<Hotkey> ____hotkeys)
    {
        BugFixPlugin.logger.LogInfo("############ UpdateHotkeys called");
        for (int i = 0; i < ____hotkeys.Count; i++)
        {
            BugFixPlugin.logger.LogInfo($"  {____hotkeys[i].gameObject.name} {____hotkeys[i].gameObject.GetInstanceID()}");
        }
    }
}
*/

[HarmonyPatch(typeof(ControlsSettings), nameof(ControlsSettings.Initialize))]
public static class Patch_ControlsSettings_Initialize
{
    public static GameObject receiveDamageAction;
    public static GameObject toggleSpeedAction;
    private static void Postfix(KeyActionControlButton[] ___keyBindingButtons, ExtendedScrollRect ___scrollRect)
    {
        BugFixPlugin.logger.LogInfo("===========init===============");
        RectTransform trn = (RectTransform) ___scrollRect.content;
        BugFixPlugin.logger.LogInfo($"{trn} {trn.rect} {trn.position}");
        BugFixPlugin.logger.LogInfo("============init==============");
        for (int i = 0; i < ___keyBindingButtons.Length; i++)
        {
            Gloomhaven.KeyActionControlButton tmp = ___keyBindingButtons[i];
            KeyAction ka = (KeyAction) typeof(KeyActionControlButton).GetField("keyAction", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tmp);
            BugFixPlugin.logger.LogInfo($"{ka}");
            TextMeshProUGUI kt = (TextMeshProUGUI) typeof(KeyActionControlButton).GetField("keyText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tmp);
            BugFixPlugin.logger.LogInfo($"  {kt}");
            ExtendedButton btn = (ExtendedButton) typeof(KeyActionControlButton).GetField("button", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tmp);
            BugFixPlugin.logger.LogInfo($"  {btn}");
            if (ka == KeyAction.TOGGLE_SPEED)
            {
                toggleSpeedAction = btn.gameObject.transform.parent.gameObject.transform.parent.gameObject;
                TextLocalizedListener tll = toggleSpeedAction.GetComponentInChildren<TextLocalizedListener>();
                if (tll != null)
                {
                    tll.SetTextKey("GUI_OPT_CONTROL_SPEED_UP");
                }
                else
                {
                    BugFixPlugin.logger.LogInfo($"########it's null>>>>>>");
                }
            }
            if (ka == KeyAction.RECEIVE_DAMAGE)
            {
                receiveDamageAction = btn.gameObject.transform.parent.gameObject.transform.parent.gameObject;
            }
            RectTransform trnsf = (RectTransform) typeof(KeyActionControlButton).GetField("rowRectTransform", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tmp);
            BugFixPlugin.logger.LogInfo($"  {trnsf.position}");
            BugFixPlugin.logger.LogInfo($"  Active? {btn.GetType()} {btn.IsActive()} {btn.gameObject.activeInHierarchy} {btn.gameObject.activeSelf}");
            GameObject prnt = btn.gameObject.transform.parent.gameObject;
            BugFixPlugin.logger.LogInfo($"  {prnt.GetType()} {prnt.name} {prnt.tag} {prnt.GetInstanceID()} {prnt.activeSelf}");
            GameObject gpt = btn.gameObject.transform.parent.gameObject.transform.parent.gameObject;
            BugFixPlugin.logger.LogInfo($"  {gpt.GetType()} {gpt} {gpt.name} {gpt.tag} {gpt.GetInstanceID()} {gpt.activeSelf}");
            GameObject ggpt = gpt.transform.parent.gameObject;
            BugFixPlugin.logger.LogInfo($"  {ggpt.GetType()} {ggpt} {ggpt.name} {ggpt.tag} {ggpt.GetInstanceID()} {ggpt.activeSelf}");
            GameObject gggpt = ggpt.transform.parent.gameObject;
            BugFixPlugin.logger.LogInfo($"  {gggpt.GetType()} {gggpt} {gggpt.name} {gggpt.tag} {gggpt.GetInstanceID()} {gggpt.activeSelf}");
        }
    }
}

[HarmonyPatch(typeof(ControlsSettings), nameof(ControlsSettings.ToggleSwitchSkipAndUndoButtons))]
public static class Patch_ControlsSettings_ToggleSwitchSkipAndUndoButtons
{
    private static void Postfix(KeyActionControlButton[] ___keyBindingButtons, ExtendedScrollRect ___scrollRect)
    {
        BugFixPlugin.logger.LogInfo("==========toggle==============");
        RectTransform trn = (RectTransform) ___scrollRect.content;
        BugFixPlugin.logger.LogInfo($"{trn} {trn.rect} {trn.position}");
        BugFixPlugin.logger.LogInfo("===========toggle=============");
        for (int i = 0; i < ___keyBindingButtons.Length; i++)
        {
            Gloomhaven.KeyActionControlButton tmp = ___keyBindingButtons[i];
            KeyAction ka = (KeyAction) typeof(KeyActionControlButton).GetField("keyAction", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tmp);
            BugFixPlugin.logger.LogInfo($"{ka}");
            var kt = typeof(KeyActionControlButton).GetField("keyText", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tmp);
            BugFixPlugin.logger.LogInfo($"  {kt}");
            ExtendedButton btn = (ExtendedButton) typeof(KeyActionControlButton).GetField("button", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tmp);
            BugFixPlugin.logger.LogInfo($"  {btn}");
            RectTransform trnsf = (RectTransform) typeof(KeyActionControlButton).GetField("rowRectTransform", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tmp);
            BugFixPlugin.logger.LogInfo($"  {trnsf.position}");
            BugFixPlugin.logger.LogInfo($"  Active? {btn.IsActive()} {btn.gameObject.activeInHierarchy} {btn.gameObject.activeSelf}");
            GameObject prnt = btn.gameObject.transform.parent.gameObject;
            BugFixPlugin.logger.LogInfo($"  {prnt.GetType()} {prnt.name} {prnt.tag} {prnt.GetInstanceID()} {prnt.activeSelf}");
            GameObject gpt = btn.gameObject.transform.parent.gameObject.transform.parent.gameObject;
            BugFixPlugin.logger.LogInfo($"  {gpt.GetType()} {gpt} {gpt.name} {gpt.tag} {gpt.GetInstanceID()} {gpt.activeSelf}");
            GameObject ggpt = gpt.transform.parent.gameObject;
            BugFixPlugin.logger.LogInfo($"  {ggpt.GetType()} {ggpt} {ggpt.name} {ggpt.tag} {ggpt.GetInstanceID()} {ggpt.activeSelf}");
            GameObject gggpt = ggpt.transform.parent.gameObject;
            BugFixPlugin.logger.LogInfo($"  {gggpt.GetType()} {gggpt} {gggpt.name} {gggpt.tag} {gggpt.GetInstanceID()} {gggpt.activeSelf}");
        }
    }
}


/*
[Gloomhaven]
ControlsSettings is a GameObject with a list of KeyActionControlButton
KeyActionControlButton gets its Init() called when ControlsSettings has its Initialize() called
the list of KeyActionControlButtons is built from all child objects of that type
unclear where these buttons get created

[Script.GUI.SMNavigation.States.MainMenuStates]
ControlsSettingsState is a subclass of MainMenuState
MainMenuState is a subclass of NavigationState<MainStateTag>
it looks like these don't do anything other than set the nav root

[none]
UIOptionsWindow is a singleton class
- contains a GLOOM.MainMenu.UISubmenuGOWindow
- contains a list of OptionTab
- contains a number of UIMainMenuOption (e.g. difficulty, display, house rules)
- the OnShow() determines which submenus are active
UIMainMenuOption is a subclass of UIMenuOptionToggle
UIMenuOptionToggle is a subclass of UIMenuOption. it has listeners for mouseenter/ontoggle
UIMenuOption is a subclass of MonoBehavior. it has some animation stuff.

[GLOOM.MainMenu]
UISubmenuGOWindow is a MonoBehavior and IShowActivity
- Show() will set the base GameObject to active
MainOptionOptions is a subclass of MainOption that touches UIOptionsWindow
MainOption is a MonoBehavior
UIMainOptionsMenu is a MonoBehavior
- contains a UIMainMenuSubptionsPanel
- contains buttons of type MainOptionOpenSuboptions and MainOption
- contains a list of UIMainMenuOption
- when Start is called, it calls InitializeOption on each of its buttons
- InitializeOption calls Show with MainOptionOpenSuboptions.BuildOptions()
UIMainMenuSubptionsPanel is a MonoBehavior
- contains a UIMainMenuSuboption prefab
- contains a UIMainMenuSuboption pool optionsPool
- contains a scrollRect optionsScroll
- calling Show with a list of MenuSuboption
--- collects MenuSuboption and UIMainMenuSuboption entries
--- calls Init on the UIMainMenuSuboption items
UIMainMenuSuboption is a subclass of UIMainMenuOption
MainOptionOpenSuboptions is an abstract Monobehavior (subclasses: extras, gameMode, sandbox)

[Script.GUI.SMNavigation]
UIStateSwitcher sets the OnShow of its activityElement to Combine(original, UIStateSwitcher.OnShow)
UIStateSwitcher.OnShow calls StateMachine.Enter(enteringTag)
MainMenuStateSwitcher is a UIStateSwitcher<MainStateTag, UIWindow>

[Script.GUI.SMNavigation.States.MainMenuStates]
ControlsSettingsState has
- StateTag == MainStateTag.ControlsSettings
- RootName == ControlsSettings
- Enter just calls base.Enter
MainMenuState
- Enter will call SetCurrentRoot on the UiNavigationManager

[SM.Gamepad]
UiNavigationManager is a class
SetCurrentRoot(rootname) will
- find the navigation root by name
- set _currentRoot
HotkeyContainer is a MonoBehavior - but unrelated to what we're doing


 */


/*
[HarmonyPatch(typeof(KeyActionControlButton), "KeyActionControlButton")]
public static class Patch_KeyActionControlButton
{
    public static bool once = false;
    private static void Postfix()
    {
        if (!once)
        {
            BugFixPlugin.logger.LogInfo($"{Environment.StackTrace}");
            once = true;
        }
    }
}
*/

[HarmonyPatch(typeof(GlobalData), "OnDeserialized")]
public static class Patch_GlobalData_OnDeserialized
{
    private static void Postfix(GlobalData __instance)
    {
        BugFixPlugin.logger.LogInfo("==============================");
        foreach (string str in __instance.CompletedTutorialIDs)
        {
            BugFixPlugin.logger.LogInfo($"Completed tutorial ID: {str}");
        }
        foreach (GlobalData.KeyBinding kb in __instance.KeyBindings)
        {
            BugFixPlugin.logger.LogInfo($"Binding {kb.Code} => {kb.Action}");
        }
    }
}

// FIXME: Note to self: add an option for follow camera disabled?
// FIXME: put this preference in CompletedTutorialIDs

/*
[HarmonyPatch(typeof(CameraController), nameof(CameraController.CameraFollowOn), MethodType.Getter)]
public static class Patch_Camera_CameraFollowOn
{
    private static bool Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(CameraController), nameof(CameraController.SmartFocus))]
public static class Patch_Camera_SmartFocus
{
    private static bool Prefix()
    {
        return false;
    }
}
*/
