using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace HsDeckInspector
{
    // Name and version come from HsDeckInspector.csproj (BepInEx.PluginInfoProps).
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyCode> InspectKey;
        internal static ConfigEntry<FaceCard.Style> FaceStyle;
        internal static ConfigEntry<float> ArtScale;
        internal static ConfigEntry<float> DeckHeight;
        internal static ConfigEntry<float> DragSpeed;

        private void Awake()
        {
            Log = Logger;
            InspectKey = Config.Bind("Controls", "InspectKey", KeyCode.D,
                "Pulls your deck out, and sends it back.");
            DragSpeed = Config.Bind("Controls", "DragSpeed", 0.45f,
                "Degrees the deck turns per pixel dragged, at 1080p.");
            DeckHeight = Config.Bind("Look", "DeckHeight", 0.62f,
                "The pulled-out deck's height as a share of the screen's.");
            FaceStyle = Config.Bind("Look", "FaceStyle", FaceCard.Style.Minion,
                "The unknown card on the deck's face: Minion (question marks on every stat) or Spell (only the cost).");
            ArtScale = Config.Bind("Look", "ArtScale", 0.62f,
                "Size of the question mark inside the unknown card's portrait.");
            InspectInputBlock.Install();
            Log.LogInfo("HS Deck Inspector loaded");
        }

        private void Update()
        {
            try
            {
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
