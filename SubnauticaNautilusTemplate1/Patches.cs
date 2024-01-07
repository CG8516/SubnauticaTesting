using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HarmonyLib;
using Nautilus.Utility;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UWE;
using Debug = UnityEngine.Debug;
using Math = System.Math;

namespace SubnauticaNautilusTemplate1;

class Utils
{
    public static string CleanFormattedString(string original, bool skipLast = false)
    {
        string[] split = original.Split('>');
        string[] newSplit = new string[split.Length];
        int lineCount = 0;
        for(int i = 0; i < split.Length; i++)
        {
            //Skip formatting tags and empty lines
            if (split[i].Length < 1 || split[i][0] == '<' || split[i][0] == '\r' || split[i][0] == '\n')
                continue;

            string[] firstHalf = split[i].Split('<');
            if (firstHalf.Length > 0)
                newSplit[lineCount++] = firstHalf[0];
        }

        StringBuilder sb = new StringBuilder();
        // Skip the last line, which is the item source info (eg; "Subnautica" or "SexyModNumber3")
        int count = lineCount;
        if (skipLast)
            count--;
        for (int i = 0; i < count; i++)
            sb.AppendLine(newSplit[i]);
        return sb.ToString();
    }

    public static bool ReadLoadText(Button button)
    {
        if (button.name.Equals("LoadButton"))
        {
            try
            {
                var parent = button.transform.parent;
                var gameMode = parent.Find("SaveGameMode");
                string gameModeStr = gameMode.GetComponent<TextMeshProUGUI>().text;
                var saveTime = parent.Find("SaveGameTime");
                string saveTimeStr = saveTime.GetComponent<TextMeshProUGUI>().text;
                var playTime = parent.Find("SaveGameLength");
                string playTimeStr = playTime.GetComponent<TextMeshProUGUI>().text;
                string totalStr = string.Format($"{saveTimeStr}. {gameModeStr}. {playTimeStr}");
                Utils.Say(totalStr);
                return true;
            }
            catch
            {
                
            }
        }

        return false;
    }

    public static void ReadButtonText(Button button)
    {
        try
        {
            var textComponent = button.gameObject.GetComponentInChildren<TextMeshProUGUI>();
            string text = textComponent.text;
            Utils.Say(text);
        }
        catch
        {
            Utils.Say(button.name);
        }
    }

    public static void Say(string text)
    {
        Debug.Log(text);
    }
}


[HarmonyPatch(typeof(HandReticle))]
class HandReticlePatches
{
    private static string oldText;
    private static string oldTranslatedText;
    private static string oldTMPText;

    [HarmonyPatch("UpdateText")]
    [HarmonyPostfix]
    public static void UpdateText(HandReticle __instance, TextMeshProUGUI comp, string text)
    {
        if (comp == __instance.compTextHand)
        {
            if (text != "" && text != oldTMPText)
            {
                string cleanedText = text;

                //Remove control indicator icon
                int bracketIndex = text.IndexOf('(');
                if (bracketIndex != -1)
                    cleanedText = text.Substring(0, bracketIndex);

                string subStr = __instance.textHandSubscript;
                if (subStr != "")
                    Utils.Say(cleanedText + ".\n" + subStr);
                else
                    Utils.Say(cleanedText);
            }

            oldTMPText = text;
        }
    }

    [HarmonyPatch("SetText")]
    [HarmonyPostfix]
    public static void SetText(HandReticle.TextType type, string text, bool translate)
    {

        return;
        if ((type == HandReticle.TextType.Hand) && oldText != text && !translate)
        {
            string logText;
            //if (translate)
            //  logText = Language.main.Get(text);
            //else
            logText = text;
            Utils.Say(logText);
            oldText = text;
        }
        else if ((type == HandReticle.TextType.Hand) && oldText != text && oldTranslatedText != text)
        {
            string translatedText = Language.main.Get(text);
            if (oldText == translatedText || translatedText == oldTranslatedText)
                return;
            Debug.Log(translatedText);
            oldTranslatedText = translatedText;
        }
    }
}


[HarmonyPatch(typeof(uGUI_RadioMessageIndicator))]
class uGUI_RadioMessageIndicatorPatches
{
    [HarmonyPatch("NewRadioMessage")]
    [HarmonyPostfix]
    private static void NewRadioMessage(bool newMessages)
    {
        if (newMessages && (bool)Story.StoryGoalManager.main &&
            Story.StoryGoalManager.main.IsGoalComplete("OnPlayRadioBounceBack"))
        {
            Utils.Say("New radio message received!");
        }
    }
}

[HarmonyPatch(typeof(uGUI_Tooltip))]
class uGUI_TooltipPatches
{
    private static int lastHash = 0;

    [HarmonyPatch("Set")]
    [HarmonyPostfix]
    public static void Set(ITooltip tooltip)
    {
        if (tooltip != null && tooltip.GetHashCode() != lastHash)
        {
            if (uGUI_Tooltip.prefix == "")
                return;
            Utils.Say(Utils.CleanFormattedString(uGUI_Tooltip.prefix));
            lastHash = tooltip.GetHashCode();
        }
    }
}

[HarmonyPatch(typeof(uGUI_ScannerIcon))]
class uGUI_ScannerIconPatches
{
    private static bool wasScannable = false;
    private static ScannerTool scanner;
    
    [HarmonyPatch("LateUpdate")]
    [HarmonyPostfix]
    public static void LateUpdate(uGUI_ScannerIcon __instance)
    {
        if (__instance.sequence.active && __instance.sequence.t > 0.1f)
        {
            if (!wasScannable)
            {
                Debug.Log("SCANNABLE");
                scanner = Inventory.main.container._items[TechType.Scanner].items[0].item.gameObject
                    .GetComponent<ScannerTool>();
                scanner.scanSound.Play();
            }

            if (__instance.sequence.t > 0.12f && __instance.sequence.t < 0.4f && !(scanner.stateCurrent == ScannerTool.ScanState.Scan || scanner.stateCurrent == ScannerTool.ScanState.SelfScan))
                scanner.scanSound.Stop();

            wasScannable = true;
        }
        else
            wasScannable = false;
    }
}

[HarmonyPatch(typeof(uGUI_Pings))]
class uGUI_PingsPatches
{
    private static List<string> currentPings = new List<string>();
    private static uGUI_Pings instance;
    private static Atlas.Sprite sprite_lifepod;
    private static Atlas.Sprite sprite_seamoth;
    private static Atlas.Sprite sprite_cyclops;
    private static Atlas.Sprite sprite_exosuit;
    private static Atlas.Sprite sprite_rocket;
    private static Atlas.Sprite sprite_beacon;
    private static Atlas.Sprite sprite_signal;
    private static Atlas.Sprite sprite_camera;
    private static Atlas.Sprite sprite_sunbeam;
    private static Atlas.Sprite sprite_base;
    private static Atlas.Sprite sprite_controlRoom;

    public static void UpdatePings2()
    {
        Dictionary<string, string> newPings = new Dictionary<string, string>();

        //Debug.Log("UpdatePings!");

        if (instance.pings.Count < 1)
            return;
        //Debug.Log("PingCount: " + instance.pings.Count.ToString());
        foreach (var ping in instance.pings)
        {
            float scale = ping.Value.GetScale();
            if (scale > 0.8f)
            {
                string pingType = "";
                Atlas.Sprite sprite = ping.Value.icon.sprite;
                if (sprite == sprite_lifepod)
                    pingType = Language.main.Get("PingLifepod");
                if (sprite == sprite_seamoth)
                    pingType = Language.main.Get("PingSeamoth");
                if (sprite == sprite_cyclops)
                    pingType = Language.main.Get("PingCyclops");
                if (sprite == sprite_exosuit)
                    pingType = Language.main.Get("PingExosuit");
                if (sprite == sprite_rocket)
                    pingType = Language.main.Get("PingRocket");
                if (sprite == sprite_beacon)
                    pingType = Language.main.Get("PingBeacon");
                if (sprite == sprite_signal)
                    pingType = Language.main.Get("PingSignal");
                if (sprite == sprite_camera)
                    pingType = Language.main.Get("PingMapRoomCamera");
                if (sprite == sprite_sunbeam)
                    pingType = Language.main.Get("SunbeamRendezvousLocation");

                newPings.Add(pingType + " " + ping.Value.infoText.text, ping.Value.distanceText.text);
            }
        }

        StringBuilder sb = new StringBuilder();
        foreach (var ping in newPings)
        {
            if (!currentPings.Contains(ping.Key))
                sb.AppendLine(ping.Key + " " + ping.Value);
        }

        string logLine = sb.ToString();
        if (logLine != "")
            Utils.Say(logLine);
        currentPings.Clear();
        foreach (var ping in newPings)
            currentPings.Add(ping.Key);

    }

    [HarmonyPatch("OnEnable")]
    [HarmonyPostfix]
    public static void OnEnable(uGUI_Pings __instance)
    {
        ManagedUpdate.Subscribe(ManagedUpdate.Queue.PreCanvasPing, UpdatePings2);
        instance = __instance;
        sprite_lifepod = SpriteManager.Get(SpriteManager.Group.Pings,
            PingManager.sCachedPingTypeStrings.Get(PingType.Lifepod));
        sprite_seamoth = SpriteManager.Get(SpriteManager.Group.Pings,
            PingManager.sCachedPingTypeStrings.Get(PingType.Seamoth));
        sprite_cyclops = SpriteManager.Get(SpriteManager.Group.Pings,
            PingManager.sCachedPingTypeStrings.Get(PingType.Cyclops));
        sprite_exosuit = SpriteManager.Get(SpriteManager.Group.Pings,
            PingManager.sCachedPingTypeStrings.Get(PingType.Exosuit));
        sprite_rocket = SpriteManager.Get(SpriteManager.Group.Pings,
            PingManager.sCachedPingTypeStrings.Get(PingType.Rocket));
        sprite_beacon = SpriteManager.Get(SpriteManager.Group.Pings,
            PingManager.sCachedPingTypeStrings.Get(PingType.Beacon));
        sprite_signal = SpriteManager.Get(SpriteManager.Group.Pings,
            PingManager.sCachedPingTypeStrings.Get(PingType.Signal));
        sprite_camera = SpriteManager.Get(SpriteManager.Group.Pings,
            PingManager.sCachedPingTypeStrings.Get(PingType.MapRoomCamera));
        sprite_sunbeam = SpriteManager.Get(SpriteManager.Group.Pings,
            PingManager.sCachedPingTypeStrings.Get(PingType.Sunbeam));
        sprite_base = SpriteManager.Get(SpriteManager.Group.Pings,
            PingManager.sCachedPingTypeStrings.Get(PingType.Base));
        sprite_controlRoom = SpriteManager.Get(SpriteManager.Group.Pings,
            PingManager.sCachedPingTypeStrings.Get(PingType.ControlRoom));
    }

    [HarmonyPatch("OnDisable")]
    [HarmonyPostfix]
    public static void OnDisable()
    {
        ManagedUpdate.Unsubscribe(ManagedUpdate.Queue.PreCanvasPing, UpdatePings2);
    }
}

[HarmonyPatch(typeof(uGUI_DepthCompass))]
class uGUI_DepthCompassPatches
{
    private static int prevDepth = 0;
    private static int prevLogDepth = 0;

    [HarmonyPatch("UpdateDepth")]
    [HarmonyPostfix]
    public static void UpdateDepth(uGUI_DepthCompass __instance)
    {
        int depth;
        int crushDepth;
        uGUI_DepthCompass.DepthMode depthInfo = __instance.GetDepthInfo(out depth, out crushDepth);
        int newDepth = Mathf.FloorToInt(depth / 10.0f);
        if (newDepth != prevDepth)
        {
            int logDepth;
            if (prevDepth > newDepth)
                logDepth = Mathf.CeilToInt((depth) / 10.0f);
            else
                logDepth = newDepth;
            if (prevLogDepth != logDepth)
            {
                Utils.Say("Depth: " + logDepth * 10);
                prevLogDepth = logDepth;
            }

            prevDepth = newDepth;
        }
    }
}

[HarmonyPatch(typeof(uGUI_ListEntry))]
class uGUI_ListEntryPatches
{
    private static int lastHash = 0;

    [HarmonyPatch("OnPointerEnter")]
    [HarmonyPostfix]
    public static void OnPointerEnter(uGUI_ListEntry __instance)
    {
        int newHash = __instance.GetHashCode();
        if (newHash != lastHash)
        {
            Utils.Say(__instance.text.text);
            lastHash = newHash;
        }
    }

}

/*
[HarmonyPatch(typeof(MainMenuPrimaryOption))]
class MainMenuPrimaryOptionPatches
{
    private static bool wasOpening = false;

    [HarmonyPatch("AnimatePanel")]
    [HarmonyPostfix]
    public static void AnimatePanel(MainMenuPrimaryOption __instance)
    {
        Utils.Say("MENU UPDATE!");
        if (__instance.isOpening && !wasOpening)
        {
            wasOpening = true;
            Utils.Say(__instance.optionPanel.name);
            Utils.Say(__instance.optionText.name);
        }
    }
}
*/


/*
[HarmonyPatch(typeof(Button))]
class ButtonPatches
{
    private static bool wasHighlighted = false;


}
*/


[HarmonyPatch(typeof(Selectable))]
class SelectablePatches
{
    private static bool wasHighlighted = false;

    [HarmonyPatch("DoStateTransition")]
    [HarmonyPostfix]
    public static void DoStateTransition(Selectable __instance, object[] __args)
    {
        if ((int)(__args[0]) == 1)
        {
            if (!wasHighlighted && __instance is Button)
            {
                if (!Utils.ReadLoadText((Button)__instance))
                    Utils.ReadButtonText((Button)__instance);
            }

            wasHighlighted = true;
        }
        else
            wasHighlighted = false;
    }
}


/*
[HarmonyPatch(typeof(uGUI_ListItem))]
class uGUI_ListItemPatches
{
    private static int lastHash = 0;

    [HarmonyPatch("OnPointerEnter")]
    [HarmonyPostfix]
    public static void OnPointerEnter(uGUI_ListItem __instance)
    {
        int newHash = __instance.GetHashCode();
        if (newHash != lastHash)
        {
            //Debug.Log(__instance.text.text);
            lastHash = newHash;
        }
    }

}
*/

/*
[HarmonyPatch(typeof(uGUI_NavigableControlGrid))]
class uGUI_NavigableControlGridPatches
{
    private static int lastHash = 0;

    [HarmonyPatch("SelectItem")]
    [HarmonyPostfix]
    public static void SelectItem(uGUI_NavigableControlGrid __instance, object item)
    {
        int newHash = item.GetHashCode();
        if (newHash != lastHash)
        {
            if (item is uGUI_ButtonSound)
            {
                Debug.Log("SOUND BUTTON!");
            }

            if (item is UnityEngine.UI.Button)
            {
                UnityEngine.UI.Button button = (UnityEngine.UI.Button)item;
                try
                {
                    var textComponent = button.gameObject.GetComponentInChildren<TextMeshProUGUI>();
                    string text = textComponent.text;
                    Debug.Log(text);
                }
                catch
                {
                    if (button.name.Equals("LoadButton"))
                    {
                        try
                        {
                            var parent = button.transform.parent;
                            var gameMode = parent.Find("SaveGameMode");
                            string gameModeStr = gameMode.GetComponent<TextMeshProUGUI>().text;
                            var saveTime = parent.Find("SaveGameTime");
                            string saveTimeStr = saveTime.GetComponent<TextMeshProUGUI>().text;
                            var playTime = parent.Find("SaveGameLength");
                            string playTimeStr = playTime.GetComponent<TextMeshProUGUI>().text;
                            string totalStr = string.Format($"{saveTimeStr}. {gameModeStr}. {playTimeStr}");
                            Debug.Log(totalStr);
                        }
                        catch
                        {
                            Debug.Log(button.name);
                        }
                    }
                    else
                        Debug.Log(button.name);
                }
            }

            lastHash = newHash;

        }
    }
}
*/

[HarmonyPatch(typeof(GamepadInputModule))]
class GamepadInputModulePatches
{
    private static object lastItem;

    [HarmonyPatch("OnUpdate")]
    [HarmonyPostfix]
    public static void OnUpdate(GamepadInputModule __instance)
    {
        if (__instance.currentNavigableGrid == null)
            return;

        object selectedItem = __instance.currentNavigableGrid.GetSelectedItem();
        if (selectedItem == null)
            return;

        if (selectedItem != lastItem)
        {
            try
            {
                //Debug.Log(selectedItem.GetType());
                //Debug.Log(selectedItem.GetType());
                Button button = null;

                if (selectedItem is Button)
                    button = (Button)selectedItem;
                else
                {
                    try
                    {
                        button = ((GameObject)selectedItem).GetComponentInChildren<Button>();
                    }
                    catch
                    {
                    }
                }

                if (button != null)
                {
                    if (!Utils.ReadLoadText(button))
                        Utils.ReadButtonText(button);
                }

                if (selectedItem is ToggleButton)
                {
                    try
                    {
                        var textMesh = ((ToggleButton)selectedItem).gameObject
                            .GetComponentInChildren<TextMeshProUGUI>();
                        string text = textMesh.text;
                        Utils.Say(text);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            lastItem = selectedItem;
        }


    }
}



#if DEBUG
    [HarmonyPatch(typeof(FlashingLightsDisclaimer))]
    class FlashingLightsDisclaimerPatches
    {
        [HarmonyPatch("OnEnable")]
        [HarmonyPostfix]
        public static void OnEnable(FlashingLightsDisclaimer __instance)
        {
            FlashingLightsDisclaimer.isFirstRun = false;
            __instance.gameObject.SetActive(false);
        }

    }
#endif
