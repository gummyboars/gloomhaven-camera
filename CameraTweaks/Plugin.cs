using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

namespace CameraTweaks;

[BepInPlugin(pluginGUID, pluginName, pluginVersion)]
public class CameraTweaksPlugin : BaseUnityPlugin
{
    const string pluginGUID = "com.gummyboars.gloomhaven.cameratweaks";
    const string pluginName = "Camera Tweaks";
    const string pluginVersion = "1.0.0";

    private Harmony HarmonyInstance = null;

    public static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(pluginName);

    private void Awake()
    {
        CameraTweaksPlugin.logger.LogInfo($"Loading plugin {pluginName}.");
        try
        {
            HarmonyInstance = new Harmony(pluginGUID);
            Assembly assembly = Assembly.GetExecutingAssembly();
            HarmonyInstance.PatchAll(assembly);
            CameraTweaksPlugin.logger.LogInfo($"Plugin {pluginName} loaded.");
        }
        catch (Exception e)
        {
            CameraTweaksPlugin.logger.LogError($"Could not load plugin {pluginName}: {e}");
        }
    }
}

// Prevents the camera from rotating whenever a character attacks by completely bypassing
// SetOptimalViewPoint, which is not used for any other functionality.
[HarmonyPatch(typeof(CameraController), nameof(CameraController.SetOptimalViewPoint))]
public static class Disable_Camera_SetOptimalViewPoint
{
    private static bool Prefix()
    {
        return false;
    }
}

// Activates the toggle speed button in the list of hotkey buttons if and only if
// the receive damage button is active. The toggle speed button is a preexisting
// hotkey that for unknown reasons is not displayed by default in the hotkey list.
[HarmonyPatch]
public static class Patch_ToggleSpeed_Hotkey_Button
{
    public static GameObject receiveDamageAction;
    public static GameObject toggleSpeedAction;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ControlsSettings), nameof(ControlsSettings.Initialize))]
    private static void Postfix(KeyActionControlButton[] ___keyBindingButtons)
    {
        for (int i = 0; i < ___keyBindingButtons.Length; i++)
        {
            KeyActionControlButton kacb = ___keyBindingButtons[i];
            KeyAction ka = (KeyAction) typeof(KeyActionControlButton).GetField("keyAction", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(kacb);
            ExtendedButton btn = (ExtendedButton) typeof(KeyActionControlButton).GetField("button", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(kacb);
            if (ka == KeyAction.TOGGLE_SPEED)
            {
                toggleSpeedAction = btn.gameObject.transform.parent.parent.gameObject;
                TextLocalizedListener tll = toggleSpeedAction.GetComponentInChildren<TextLocalizedListener>();
                tll?.SetTextKey("GUI_OPT_CONTROL_SPEED_UP");
            }
            if (ka == KeyAction.RECEIVE_DAMAGE)
            {
                receiveDamageAction = btn.gameObject.transform.parent.parent.gameObject;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UISubmenuGOWindow), nameof(UISubmenuGOWindow.Show))]
    private static void Postfix()
    {
        if (receiveDamageAction != null && toggleSpeedAction != null)
        {
            toggleSpeedAction.SetActive(receiveDamageAction.activeSelf);
        }
    }
}

// Makes sure that for every scenario, the hotkey for toggling animation speed is registered.
// Note that this does not save the global save data (it may later be saved by something else).
[HarmonyPatch(typeof(Choreographer))]
public static class Patch_ToggleSpeed_Hotkey
{
    [HarmonyPostfix]
    [HarmonyPatch("Awake")]
    private static void PostfixAwake()
    {
        KeyActionHandler kah = new KeyActionHandler(KeyAction.TOGGLE_SPEED, ToggleSpeed);
        Singleton<KeyActionHandlerController>.Instance.AddHandler(kah);
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnDestroy")]
    private static void PrefixOnDestroy()
    {
        if (!CoreApplication.IsQuitting)
        {
            Singleton<KeyActionHandlerController>.Instance.RemoveHandler(KeyAction.TOGGLE_SPEED, ToggleSpeed);
        }
    }

    public static void ToggleSpeed()
    {
        SaveData.Instance.Global.SpeedUpToggle = !SaveData.Instance.Global.SpeedUpToggle;
    }
}

// Stores camera settings and writes them into the global save data. To be compatible
// with other saves that do not use this mod, we use CompletedTutorialIDs as storage.
// This list of strings is not used for anything other than a Contains() check in the
// original code, and can therefore have extra strings without consequence.
[HarmonyPatch]
public static class CameraSettings
{
    public static bool CameraFollowEnabled { get; private set; } = true;
    public static float TargetZoom { get; private set; } = 0;
    private static string settingsName = "com.gummyboars.gloomhaven.camera.";

    // This class is the one serialized to JSON.
    [Serializable]
    public class InternalSettings
    {
        public bool nofollow;
        public float zoom;
    }

    public static void ToggleCameraFollow(bool autoCameraEnabled)
    {
        if (SaveData.Instance.Global == null)
        {
            return;
        }
        if (CameraFollowEnabled == autoCameraEnabled)
        {
            return;
        }

        CameraFollowEnabled = autoCameraEnabled;
        SaveSettings();
        if (!CameraFollowEnabled)
        {
            if (CameraController.s_CameraController != null)
            {
                // The follow controller needs to be reset; otherwise the camera will be
                // locked and continue to follow the object until the scenario is over.
                CameraTargetFocalFollowController ctffc = (CameraTargetFocalFollowController) typeof(CameraController).GetField("_targetFocalFollowController", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(CameraController.s_CameraController);
                if (ctffc != null && ctffc.IsFollowingTarget)
                {
                    ctffc.OnArrivedToPoint();
                }
                CameraController.s_CameraController.ResetFocalPointGameObject();
            }
        }
    }

    public static void SetZoom(float zoom)
    {
        TargetZoom = zoom;
        if (SaveData.Instance.Global == null)
        {
            return;
        }
        SaveSettings();
    }

    private static void SaveSettings()
    {
        InternalSettings settings = new InternalSettings{nofollow = !CameraFollowEnabled, zoom = TargetZoom};
        string serialized = JsonUtility.ToJson(settings);
        int index = -1;
        for (int i = 0; i < SaveData.Instance.Global.CompletedTutorialIDs.Count; i++)
        {
            string entry = SaveData.Instance.Global.CompletedTutorialIDs[i];
            if (entry.StartsWith(settingsName))
            {
                index = i;
                SaveData.Instance.Global.CompletedTutorialIDs[i] = settingsName + serialized;
                break;
            }
        }
        if (index < 0)
        {
            SaveData.Instance.Global.CompletedTutorialIDs.Add(settingsName + serialized);
        }
        SaveData.Instance.SaveGlobalData();
    }

    private static void ParseSettings(string serialized)
    {
        InternalSettings settings = JsonUtility.FromJson<InternalSettings>(serialized);
        CameraFollowEnabled = !settings.nofollow;
        TargetZoom = settings.zoom;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GlobalData), "OnDeserialized")]
    private static void PostfixGlobalDataOnDeserialized(GlobalData __instance)
    {
        foreach (string entry in __instance.CompletedTutorialIDs)
        {
            if (entry.StartsWith(settingsName))
            {
                try
                {
                    ParseSettings(entry.Substring(settingsName.Length));
                }
                catch (Exception e)
                {
                    CameraTweaksPlugin.logger.LogInfo($"{e}");
                }
                break;
            }
        }
    }
}

// Bypasses SetPoint on the follow controller if camera follow is disabled.
[HarmonyPatch(typeof(CameraTargetFocalFollowController))]
public static class Patch_CameraTargetFocalFollowController
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(CameraTargetFocalFollowController.SetPoint), new[] {typeof(GameObject)})]
    private static bool PrefixSetPointGameObject()
    {
        return CameraSettings.CameraFollowEnabled;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(CameraTargetFocalFollowController.SetPoint), new[] {typeof(Vector3), typeof(bool)})]
    private static bool PrefixSetPointVector3()
    {
        return CameraSettings.CameraFollowEnabled;
    }
}

// Creates a button in general settings for enabling/disabling camera follow behavior.
[HarmonyPatch(typeof(GeneralSettings))]
public static class Patch_CameraFollowButton
{
    public static GameObject cameraFollowObject = null;

    [HarmonyPostfix]
    [HarmonyPatch("OnEnable")]
    private static void PostfixOnEnable(ButtonSwitch ___crossPlayToggle, ButtonSwitch ___scenarioSpeedUpToggle)
    {
        if (cameraFollowObject == null)
        {
            CloneCrossPlayObject(___crossPlayToggle.transform.parent.parent.gameObject, ___scenarioSpeedUpToggle.transform.parent.parent.gameObject);
        }
        cameraFollowObject.SetActive(true);
        ButtonSwitch cameraFollowButton = cameraFollowObject.GetComponentInChildren<ButtonSwitch>();
        cameraFollowButton.IsOn = CameraSettings.CameraFollowEnabled;
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnDestroy")]
    private static void Prefix()
    {
        if (cameraFollowObject != null)
        {
            GameObject.Destroy(cameraFollowObject);
            cameraFollowObject = null;
        }
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
        btns.OnValueChanged.AddListener(CameraSettings.ToggleCameraFollow);
        Vector3 difference = speedUp.transform.position - crossPlay.transform.position;
        cameraFollowObject.transform.Translate(difference);
    }
}

// Remembers camera FOV on scenario end and restores it on scenario start.
// If the round is being restarted, it will also remember the camera horizontal angle.
[HarmonyPatch]
public static class Patch_DefaultAngleAndFOV
{
    public static float cameraGameHorizontalAngle = 0;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ScenarioRuleClient), nameof(ScenarioRuleClient.Stop), new Type[] {})]
    private static void PrefixScenarioStop()
    {
        if (CameraController.s_CameraController == null)
        {
            return;
        }

        float targetZoom = (float) typeof(CameraController).GetField("m_TargetZoom", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(CameraController.s_CameraController);
        CameraSettings.SetZoom(targetZoom);
        if (Choreographer.s_Choreographer != null && Choreographer.s_Choreographer.IsRestarting)
        {
            cameraGameHorizontalAngle = (float) typeof(CameraController).GetField("m_CameraGameHorizontalAngle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(CameraController.s_CameraController);
        }
        else
        {
            cameraGameHorizontalAngle = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Choreographer), nameof(Choreographer.ScenarioImportProcessedCallback))]
    private static void PostfixScenarioStart()
    {
        if (CameraController.s_CameraController == null)
        {
            return;
        }

        if (cameraGameHorizontalAngle != 0)
        {
            typeof(CameraController).GetField("m_CameraGameHorizontalAngle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(CameraController.s_CameraController, cameraGameHorizontalAngle);
            CameraController.s_CameraController.ResetCameraRotation();
        }
        if (CameraSettings.TargetZoom != 0)
        {
            CameraController.s_CameraController.ZoomToFOV(CameraSettings.TargetZoom, 0);
        }
    }
}

public static class PrintHelper
{
    public static void PrintRecursive(Transform t, int depth)
    {
        string s = "";
        for (int i = 0; i < depth; i++)
        {
            s += "  ";
        }
        CameraTweaksPlugin.logger.LogInfo($"{s}{t.gameObject}");
        if (t.gameObject.name == "Title")
        {
            MonoBehaviour[] lst = t.gameObject.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour b in lst)
            {
                CameraTweaksPlugin.logger.LogInfo($"  {s}{b} {b.GetType()}");
            }
        }
        foreach(Transform child in t)
        {
            PrintRecursive(child, depth+1);
        }
    }
}
