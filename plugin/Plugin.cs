using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HsDeckInspector
{
    // Name and version come from HsDeckInspector.csproj (BepInEx.PluginInfoProps).
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyCode> InspectKey;
        internal static ConfigEntry<KeyCode> OpponentKey;
        internal static ConfigEntry<FaceCard.Style> FaceStyle;
        internal static ConfigEntry<float> ArtScale;
        internal static ConfigEntry<bool> AnimatedBack;
        internal static ConfigEntry<float> FaceDepth;
        internal static ConfigEntry<float> BackDepth;
        private static ConfigFile _config;
        internal static ConfigEntry<float> DeckHeight;
        internal static ConfigEntry<float> DragSpeed;

        private void Awake()
        {
            Log = Logger;
            InspectKey = Config.Bind("Controls", "InspectKey", KeyCode.D,
                "Pulls your deck out, and sends it back.");
            OpponentKey = Config.Bind("Controls", "OpponentKey", KeyCode.F,
                "Pulls your opponent's deck out, and sends it back. Each key swaps to its deck if the other one is out.");
            DragSpeed = Config.Bind("Controls", "DragSpeed", 0.45f,
                "Degrees the deck turns per pixel dragged, at 1080p.");
            DeckHeight = Config.Bind("Look", "DeckHeight", 0.62f,
                "The pulled-out deck's height as a share of the screen's.");
            FaceStyle = Config.Bind("Look", "FaceStyle", FaceCard.Style.Minion,
                "The unknown card on the deck's face: Minion (question marks on every stat) or Spell (only the cost).");
            ArtScale = Config.Bind("Look", "ArtScale", 0.62f,
                "Size of the question mark inside the unknown card's portrait.");
            AnimatedBack = Config.Bind("Look", "AnimatedBack", true,
                "Show your card back as the collection does, with its animation and effects, not the deck's flat picture of it.");
            FaceDepth = Config.Bind("Look", "FaceDepth", 0.3f,
                "How far the unknown card's stats and text stand off the card: 1 is a normal hand card.");
            BackDepth = Config.Bind("Look", "BackDepth", 0.6f,
                "How deep the animated card back's model is: 1 is as the collection shows it.");
            _config = Config;
            InspectInputBlock.Install();
            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo("HS Deck Inspector loaded");
        }

        internal static void ReloadConfig() => _config.Reload();

        private void Update()
        {
            try
            {
                KeyPanel.Update();
                DeckInspector.Update();
            }
            catch (System.Exception e)
            {
                Log.LogError($"Deck inspector: {e}");
                DeckInspector.Abort();
            }
        }

        private void OnDestroy() => DeckInspector.Abort();
    }
}
