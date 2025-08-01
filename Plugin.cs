using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

using AsmodeeNet.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using HarmonyLib.Tools;

using MapRuleLibrary.State;
using Gloomhaven;
using GLOOM.MainMenu;
using Script.GUI.SMNavigation.States.MainMenuStates;
using ScenarioRuleLibrary;
using SM.Gamepad;

namespace BugFixes;

[BepInPlugin(pluginGUID, pluginName, pluginVersion)]
public class BugFixPlugin : BaseUnityPlugin
{
    const string pluginGUID = "com.gummyboars.gloomhaven.bugfixes";
    const string pluginName = "Bug Fixes";
    const string pluginVersion = "0.0.1";

    private Harmony HarmonyInstance = null;

    public static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(pluginName);
        
    private void Awake()
    {
        // Plugin startup logic
        BugFixPlugin.logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        HarmonyFileLog.Enabled = true;
        HarmonyLib.Tools.Logger.ChannelFilter = HarmonyLib.Tools.Logger.LogChannel.All;
        BugFixPlugin.logger.LogInfo($"Harmony log path is {HarmonyFileLog.FileWriterPath}");
        HarmonyInstance = new Harmony(pluginGUID);
        BugFixPlugin.logger.LogInfo("Harmony instance created");
        Assembly assembly = Assembly.GetExecutingAssembly();
        BugFixPlugin.logger.LogInfo("Harmony got assembly.");
        HarmonyInstance.PatchAll(assembly);
        BugFixPlugin.logger.LogInfo("Harmony patched all.");
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

[HarmonyPatch(typeof(Choreographer), "Awake")]
public static class Patch_Choreographer_Awake
{
    private static void Postfix()
    {
        KeyActionHandler kah = new KeyActionHandler(KeyAction.TOGGLE_SPEED, ToggleSpeed);
        Singleton<KeyActionHandlerController>.Instance.AddHandler(kah);
        BugFixPlugin.logger.LogInfo("Created new action handler for toggle speed");
    }

    public static void ToggleSpeed()
    {
        SaveData.Instance.Global.SpeedUpToggle = !SaveData.Instance.Global.SpeedUpToggle;
        BugFixPlugin.logger.LogInfo($"SpeedUpToggle set to {SaveData.Instance.Global.SpeedUpToggle}");
    }
}

[HarmonyPatch(typeof(Choreographer), "OnDestroy")]
public static class Patch_Choreographer_OnDestroy
{
    private static void Prefix()
    {
        if (!CoreApplication.IsQuitting)
        {
            Singleton<KeyActionHandlerController>.Instance.RemoveHandler(KeyAction.TOGGLE_SPEED, Patch_Choreographer_Awake.ToggleSpeed);
            BugFixPlugin.logger.LogInfo("Removed action handler for toggle speed");
        }
    }
}

public static class CameraFollowHolder
{
    private static bool cameraFollowEnabled = true;
    private static string featureName = "com.gummyboars.gloomhaven.cameraFollowDisabled";

    public static void ToggleCameraFollow(bool autoCameraEnabled)
    {
        if (SaveData.Instance.Global == null)
        {
            return;
        }
        cameraFollowEnabled = autoCameraEnabled;
        if (cameraFollowEnabled)
        {
            if (SaveData.Instance.Global.CompletedTutorialIDs.Contains(featureName))
            {
                SaveData.Instance.Global.CompletedTutorialIDs.Remove(featureName);
                SaveData.Instance.SaveGlobalData();
            }
        }
        else
        {
            if (!SaveData.Instance.Global.CompletedTutorialIDs.Contains(featureName))
            {
                SaveData.Instance.Global.CompletedTutorialIDs.Add(featureName);
                SaveData.Instance.SaveGlobalData();
            }
            if (CameraController.s_CameraController != null)
            {
                CameraTargetFocalFollowController ctffc = (CameraTargetFocalFollowController) typeof(CameraController).GetField("_targetFocalFollowController", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(CameraController.s_CameraController);
                if (ctffc != null && ctffc.IsFollowingTarget)
                {
                    ctffc.OnArrivedToPoint();
                }
                CameraController.s_CameraController.ResetFocalPointGameObject();
                // CameraController.s_CameraController.DisableCameraInput(false);
            }
        }
    }

    public static void InitializeCameraFollow(List<string> tutorialIDs)
    {
        cameraFollowEnabled = !tutorialIDs.Contains(featureName);
    }

    public static bool CameraFollowEnabled()
    {
        return cameraFollowEnabled;
    }
}
 
[HarmonyPatch(typeof(CameraTargetFocalFollowController), nameof(CameraTargetFocalFollowController.SetPoint), new[] {typeof(GameObject)})]
public static class Patch_SetPoint_GameObject
{
    private static bool Prefix()
    {
        return CameraFollowHolder.CameraFollowEnabled();
    }
}

[HarmonyPatch(typeof(CameraTargetFocalFollowController), nameof(CameraTargetFocalFollowController.SetPoint), new[] {typeof(Vector3), typeof(bool)})]
public static class Patch_SetPoint_Vector3
{
    private static bool Prefix()
    {
        return CameraFollowHolder.CameraFollowEnabled();
    }
}

[HarmonyPatch(typeof(ScenarioRuleClient), nameof(ScenarioRuleClient.Stop), new Type[] {})]
public static class Patch_ScenarioRuleClient_Stop
{
    public static float cameraGameHorizontalAngle = 0;
    public static float targetZoom = 0;

    private static void Prefix()
    {
        if (CameraController.s_CameraController == null)
        {
            return;
        }

        targetZoom = (float) typeof(CameraController).GetField("m_TargetZoom", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(CameraController.s_CameraController);
        BugFixPlugin.logger.LogInfo($"Saved camera FOV {targetZoom}");
        if (Choreographer.s_Choreographer != null && Choreographer.s_Choreographer.IsRestarting)
        {
            cameraGameHorizontalAngle = (float) typeof(CameraController).GetField("m_CameraGameHorizontalAngle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(CameraController.s_CameraController);
            BugFixPlugin.logger.LogInfo($"Saved camera horizontal angle {cameraGameHorizontalAngle}");
        }
        else
        {
            cameraGameHorizontalAngle = 0;
            BugFixPlugin.logger.LogInfo("Cleared camera horizontal angle");
        }
    }
}

[HarmonyPatch(typeof(Choreographer), nameof(Choreographer.ScenarioImportProcessedCallback))]
public static class Patch_Choreographer_ScenarioImportProcessedCallback
{
    private static void Postfix()
    {
        if (CameraController.s_CameraController == null)
        {
            return;
        }
        if (Patch_ScenarioRuleClient_Stop.cameraGameHorizontalAngle != 0)
        {
            typeof(CameraController).GetField("m_CameraGameHorizontalAngle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(CameraController.s_CameraController, Patch_ScenarioRuleClient_Stop.cameraGameHorizontalAngle);
            CameraController.s_CameraController.ResetCameraRotation();
            BugFixPlugin.logger.LogInfo($"Set camera horizontal angle to {Patch_ScenarioRuleClient_Stop.cameraGameHorizontalAngle}");
        }
        else
        {
            BugFixPlugin.logger.LogInfo("Did not set camera horizontal angle");
        }
        if (Patch_ScenarioRuleClient_Stop.targetZoom != 0)
        {
            CameraController.s_CameraController.ZoomToFOV(Patch_ScenarioRuleClient_Stop.targetZoom, 0);
            BugFixPlugin.logger.LogInfo($"Set camera FOV to {Patch_ScenarioRuleClient_Stop.targetZoom}");
        }
        else
        {
            BugFixPlugin.logger.LogInfo("Did not set camera FOV");
        }
    }
}

[HarmonyPatch(typeof(CameraController), nameof(CameraController.SetCameraWithMessageProfile))]
public static class Patch_CameraController_SetCameraWithMessageProfile
{
    private static void Prefix(ScenarioRuleLibrary.CustomLevels.CLevelCameraProfile profileToSetTo)
    {
        BugFixPlugin.logger.LogInfo($"SetCameraWithMessageProfile called with {profileToSetTo.CameraFieldOfView}");
    }

    private static void Postfix()
    {
        BugFixPlugin.logger.LogInfo("SetCameraWithMessageProfile done");
    }
}

[HarmonyPatch(typeof(CameraController), nameof(CameraController.SetCameraDirectionAndFocalPoint))]
public static class Patch_CameraController_SetCameraDirectionAndFocalPoint
{
    private static void Prefix(Transform pointOfView)
    {
        BugFixPlugin.logger.LogInfo($"SetCameraDirectionAndFocalPoint called with {pointOfView}");
    }

    private static void Postfix()
    {
        BugFixPlugin.logger.LogInfo("SetCameraDirectionAndFocalPoint  done");
    }
}

[HarmonyPatch(typeof(CameraController), nameof(CameraController.InitCamera))]
public static class Patch_CameraController_InitCamera
{
    private static void Prefix()
    {
        BugFixPlugin.logger.LogInfo($"SetCameraDirectionAndFocalPoint called with {CameraController.s_InitialHorizontalAngle}");
    }

    private static void Postfix()
    {
        BugFixPlugin.logger.LogInfo("SetCameraDirectionAndFocalPoint  done");
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

    public static void PrintRecursive(Transform t, int depth)
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

[HarmonyPatch(typeof(GeneralSettings), "OnEnable")]
public static class Patch_GeneralSettings_OnEnable
{
    public static GameObject cameraFollowObject = null;
    private static void Postfix(ButtonSwitch ___crossPlayToggle, ButtonSwitch ___scenarioSpeedUpToggle)
    {
        if (cameraFollowObject == null)
        {
            CloneCrossPlayObject(___crossPlayToggle.transform.parent.parent.gameObject, ___scenarioSpeedUpToggle.transform.parent.parent.gameObject);
        }
        cameraFollowObject.SetActive(true);
        ButtonSwitch cameraFollowButton = cameraFollowObject.GetComponentInChildren<ButtonSwitch>();
        cameraFollowButton.IsOn = CameraFollowHolder.CameraFollowEnabled();
        GameObject gpt = ___crossPlayToggle.transform.parent.parent.parent.gameObject;
        Patch_UISubmenuGOWindow_Show.PrintRecursive(gpt.transform, 0);
    }

    private static void CloneCrossPlayObject(GameObject crossPlay, GameObject speedUp)
    {
        cameraFollowObject = GameObject.Instantiate(speedUp, speedUp.transform.parent);
        cameraFollowObject.name = "Camera Follow";
        TextLocalizedListener tll = cameraFollowObject.GetComponentInChildren<TextLocalizedListener>();
        tll.SetTextKey(null);
        TextMeshProUGUI tmpu = cameraFollowObject.GetComponentInChildren<TextMeshProUGUI>();
        tmpu.text = "Auto-Camera";
        ButtonSwitch btns = cameraFollowObject.GetComponentInChildren<ButtonSwitch>();
        btns.OnValueChanged.RemoveAllListeners();
        btns.OnValueChanged.AddListener(btns.Refresh);
        btns.OnValueChanged.AddListener(CameraFollowHolder.ToggleCameraFollow);
        Vector3 difference = speedUp.transform.position - crossPlay.transform.position;
        cameraFollowObject.transform.Translate(difference);
    }
}

[HarmonyPatch(typeof(GeneralSettings), "OnDestroy")]
public static class Patch_GeneralSettings_OnDestroy
{
    private static void Prefix()
    {
        if (Patch_GeneralSettings_OnEnable.cameraFollowObject != null)
        {
            GameObject.Destroy(Patch_GeneralSettings_OnEnable.cameraFollowObject);
        }
    }
}
 
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

            /*
            if (i == 0)
            {
                Patch_UISubmenuGOWindow_Show.PrintRecursive(ggpt.transform, 0);
            }
            */
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

======================================================================

[none]
GHControls defines PlayerAction and GHAction types, which have a OnPressed and OnValueChanged callback
ControlBindings has a MapBindingsToPlayerControls. it calls AddBinding on the PlayerActions
InputManager is a singleton that contains a GHControls
- Awake() will call InitialiseInControlGHControls() to initialise it with defaults
- SetKey() will set a key to a specific action
- RegisterToOnPressed() will associate a callback with a specific KeyAction
KeyActionHandlerController is a singleton that contains a list of KeyActionHandler objects
- AddHandler will add a KeyActionHandler to the list
KeyActionHandler
- has a KeyAction and an Action callback
- has one or more blockers IKeyActionHandlerBlocker
- the Action is set by the constructor
- it will call RegisterToOnPressed() if all blockers are cleared (and unregister if blockers are added)

Choreographer is a 15000 line class that is a MonoBehavior
- this is the state machine for playing a scenario
- it is not a singleton, but does have a static s_Choreographer
- it contains all the game objects like the ReadyButton, UndoButton, etc
- it has a ScenarioRuleLibrary.ScenarioState m_CurrentState
- Awake()
  - sets s_Choreographer
  - calls ScenarioRuleClient.SetMessageHandler(this->MessageHandler)
  - adds the OnGameSpeedChanged callback to the GlobalData
  - adds the SwitchSkipAndUndoButtons callback to the GlobalData
- Play() will call UnityGameEditorRuntime.LoadScenario()
- when moving to the next round, disables input, updates state machine, then enables input
- OnSceneLoadedCallback will disable input
- OnSceneLoadedCallbackContinued will enable input
- EndGameCoroutine will enable input
- OnDestroy will enable input


AddHandler may be called from anywhere - e.g. from CardsHandManager.Awake()
SpeedUpButton is a MonoBehavior that calls InputManager.RegisterToOnPressed with an action and a callback

[Script.GUI.SMNavigation.States.ScenarioStates]
RoundStartScenarioState
- Enter will call InitializeInput
- InitializeInput will call SubscribeInput
- SubscribeInput will call AddHandler on the KeyActionHandlerController

[InControl]
PlayerAction objects are in a set, and can be saved/loaded from a stream

FIXME: use AddHandler in Awake(). remove it in EndGameCoroutine()
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
        CameraFollowHolder.InitializeCameraFollow(__instance.CompletedTutorialIDs);
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

[HarmonyPatch(typeof(KeyActionHandlerController), "AddHandler")]
public static class Patch_KeyActionHandlerController_AddHandler
{
    private static void Prefix(KeyActionHandler handler)
    {
        if (handler.KeyAction == KeyAction.HIGHLIGHT || handler.KeyAction == KeyAction.ROTATE_CAMERA_LEFT || handler.KeyAction == KeyAction.DISPLAY_CARDS_HERO_1)
        {
            BugFixPlugin.logger.LogInfo($"AddHandler called {handler.KeyAction}");
            BugFixPlugin.logger.LogInfo($"{Environment.StackTrace}");
        }
    }
}

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
