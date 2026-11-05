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
        private const string Repository = "https://github.com/RTantrumR/HS_DeckInspector";

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
        internal static ConfigEntry<bool> LayDecks;
        internal static ConfigEntry<float> LayTiltX;
        internal static ConfigEntry<float> LayTiltY;
        internal static ConfigEntry<float> LayTiltZ;
        internal static ConfigEntry<float> LayRight;
        internal static ConfigEntry<float> LayUp;
        internal static ConfigEntry<float> LayLift;
        internal static ConfigEntry<float> LayScale;
        internal static ConfigEntry<float> DrawSlide;
        internal static ConfigEntry<float> DrawRise;
        internal static ConfigEntry<float> DrawRevealDelay;
        internal static ConfigEntry<float> DrawRevealTime;

        private void Awake()
        {
            Log = Logger;
            BindModInfo();
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
            LayDecks = Config.Bind("Board", "LayDecks", false,
                "Lay both decks on the board with their card backs up, and draw from them there.");
            LayTiltX = Config.Bind("Board", "LayTiltX", 0f, "Degrees a laid deck leans around the screen's horizontal.");
            LayTiltY = Config.Bind("Board", "LayTiltY", 16f, "Degrees a laid deck turns around the screen's vertical, to show its thickness.");
            LayTiltZ = Config.Bind("Board", "LayTiltZ", 0f, "Degrees a laid deck turns in the screen's plane.");
            LayRight = Config.Bind("Board", "LayRight", 0.2f, "How far right of its slot a laid deck sits, in board units.");
            LayUp = Config.Bind("Board", "LayUp", 0f, "How far up the screen from its slot a laid deck sits, in board units.");
            LayLift = Config.Bind("Board", "LayLift", 1.2f, "How far towards the camera a laid deck sits, in board units.");
            LayScale = Config.Bind("Board", "LayScale", 0.75f, "A laid deck's size against the game's deck: lifted towards the camera it looks bigger, so it needs less than 1 to look the same.");
            DrawSlide = Config.Bind("Board", "DrawSlide", 2f, "How far a drawn card slides off a laid deck, along the deck, before it heads for the hand.");
            DrawRise = Config.Bind("Board", "DrawRise", 1f, "How far a drawn card lifts off a laid deck while it slides.");
            DrawRevealDelay = Config.Bind("Board", "DrawRevealDelay", 0.45f, "Seconds your drawn card stays face down after it leaves a laid deck.");
            DrawRevealTime = Config.Bind("Board", "DrawRevealTime", 0.4f, "Seconds your drawn card takes to turn over.");
            _config = Config;
            InspectInputBlock.Install();
            new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo("HS Deck Inspector loaded");
        }

        internal static void ReloadConfig() => _config.Reload();

        internal static string ConfigPath => _config.ConfigFilePath;

        /// <summary>
        /// What Firestone's mods page reads from a plugin's config file (mods-manager.service.ts): it lists an installed
        /// mod and checks for updates from these, so they have to be there however the addon got installed. Version is
        /// set every start: an update through the setup would otherwise leave the old one for Firestone to compare.
        /// No = in the descriptions: Firestone reads every line holding one as a setting.
        /// </summary>
        private void BindModInfo()
        {
            Config.Bind("General", "Name", MyPluginInfo.PLUGIN_NAME, "For Firestone's mods page.");
            Config.Bind("General", "Guid", MyPluginInfo.PLUGIN_GUID, "For Firestone's mods page.");
            Config.Bind("General", "Version", MyPluginInfo.PLUGIN_VERSION, "For Firestone's mods page.").Value = MyPluginInfo.PLUGIN_VERSION;
            Config.Bind("General", "DownloadLink", Repository, "For Firestone's mods page.");
            Config.Bind("General", "Description", "Pull a deck off the board and spin it.", "For Firestone's mods page.");
        }

        private void Update()
        {
            try
            {
                KeyPanel.Update();
                BoardDeck.Update();
                DeckInspector.Update();
            }
            catch (System.Exception e)
            {
                Log.LogError($"Deck inspector: {e}");
                DeckInspector.Abort();
                BoardDeck.Disable();
            }
        }

        private void OnDestroy()
        {
            DeckInspector.Abort();
            BoardDeck.Disable();
        }
    }
}
