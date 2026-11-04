using Blizzard.T5.AssetManager;
using UnityEngine;

namespace HsDeckInspector
{
    /// <summary>
    /// The unknown card on the deck's face: a real hand card with its stats and name turned into question marks and
    /// the mystery reward's art as its portrait, so the deck's order stays a secret.
    /// </summary>
    internal static class FaceCard
    {
        public enum Style { Minion, Spell }

        // Keys from the bundle's container: GUIDs stay the same across patches, the bundle names don't.
        private const string MysteryArt = "Icon_MysteryReward_Art.png:bc27baf19aee0fe43aa5de910eb701d1";
        private const string MinionCard = "CS2_231"; // Wisp: neutral, no text
        private const string SpellCard = "GAME_005"; // The Coin: neutral
        private const int PortraitSize = 512;

        private static readonly Color BackdropCentre = new Color(0.20f, 0.10f, 0.26f);
        private static readonly Color BackdropEdge = new Color(0.05f, 0.03f, 0.08f);

        private static Texture2D _portrait;
        private static float _portraitArtScale;
        private static bool _holdLogged;

        /// <summary>The card as a child of nothing, lying flat (+Y up, +Z its top); null if it can't be built.</summary>
        public static Actor Build(Style style)
        {
            var def = DefLoader.Get()?.GetEntityDef(style == Style.Minion ? MinionCard : SpellCard);
            if (def == null)
            {
                Plugin.Log.LogWarning("Face card: entity def not found");
                return null;
            }
            var go = AssetLoader.Get().InstantiatePrefab(ActorNames.GetHandActor(def, TAG_PREMIUM.NORMAL), AssetLoadingOptions.IgnorePrefabPosition);
            var actor = go != null ? go.GetComponent<Actor>() : null;
            if (actor == null)
            {
                Plugin.Log.LogWarning("Face card: hand actor did not load");
                if (go != null) Object.Destroy(go);
                return null;
            }
            actor.SetIgnoreGameEntity(true);
            var shown = def.Clone();
            shown.SetTag(GAME_TAG.RARITY, (int)TAG_RARITY.FREE); // the plain frame, with no rarity gem to read
            actor.SetEntityDef(shown); // loads the base card's art, and re-applies it later in the background
            actor.SetPremium(TAG_PREMIUM.NORMAL);

            var portrait = Portrait();
            if (portrait != null) actor.SetPortraitTextureOverride(portrait); // the one texture that update respects
            else Plugin.Log.LogWarning("Face card: the mystery art did not load");
            actor.UpdateAllComponents();

            Hide(actor.m_watermarkMesh);
            Hide(actor.m_racePlateObject);
            SetTexts(actor);
            actor.SetBlockTextComponentUpdate(true);

            actor.SetUnlit();
            actor.Show();
            go.name = "DeckInspectorFaceCard";
            return actor;
        }

        /// <summary>Puts back anything an update of the base card changed; called every frame the card exists.</summary>
        public static void Hold(Actor actor)
        {
            bool changed = SetTexts(actor);
            if (_portrait != null && actor.GetPortraitTexture() != _portrait)
            {
                actor.SetPortraitTextureOverride(_portrait);
                changed = true;
            }
            if (changed && !_holdLogged)
            {
                _holdLogged = true;
                Plugin.Log.LogInfo("Face card: an update of the base card was undone");
            }
        }

        private static void Hide(GameObject go)
        {
            if (go != null) go.SetActive(false);
        }

        private static bool SetTexts(Actor actor)
        {
            bool changed = SetText(actor.m_nameTextMesh, "???");
            changed |= SetText(actor.m_powersTextMesh, "");
            changed |= SetText(actor.m_costTextMesh, "?");
            changed |= SetText(actor.m_attackTextMesh, "?");
            changed |= SetText(actor.m_healthTextMesh, "?");
            return changed;
        }

        private static bool SetText(UberText text, string value)
        {
            if (text == null || text.Text == value) return false;
            text.Text = value;
            return true;
        }

        /// <summary>
        /// The mystery art over a dark backdrop. The game's own material for it is opaque, which would draw the art's
        /// transparent surround as a gray square, so the alpha is applied here, once.
        /// </summary>
        private static Texture2D Portrait()
        {
            float scale = Mathf.Clamp(Plugin.ArtScale.Value, 0.1f, 1f);
            if (_portrait != null && _portraitArtScale == scale) return _portrait;
            var art = AssetLoader.Get().LoadTexture(MysteryArt);
            if (art == null) return null;

            int inner = Mathf.RoundToInt(PortraitSize * scale);
            var rt = RenderTexture.GetTemporary(inner, inner, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Graphics.Blit(art, rt, Vector2.one, Vector2.zero);
            RenderTexture.active = rt;
            var read = new Texture2D(inner, inner, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, inner, inner), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            var source = read.GetPixels32();
            Object.Destroy(read);

            var pixels = new Color32[PortraitSize * PortraitSize];
            int margin = (PortraitSize - inner) / 2;
            float mid = (PortraitSize - 1) * 0.5f;
            for (int y = 0; y < PortraitSize; y++)
            {
                for (int x = 0; x < PortraitSize; x++)
                {
                    float dx = (x - mid) / mid, dy = (y - mid) / mid;
                    Color c = Color.Lerp(BackdropCentre, BackdropEdge, Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy)));
                    int sx = x - margin, sy = y - margin;
                    if (sx >= 0 && sy >= 0 && sx < inner && sy < inner)
                    {
                        Color32 a = source[sy * inner + sx];
                        c = Color.Lerp(c, new Color(a.r / 255f, a.g / 255f, a.b / 255f), a.a / 255f);
                    }
                    pixels[y * PortraitSize + x] = c;
                }
            }

            if (_portrait != null) Object.Destroy(_portrait);
            _portrait = new Texture2D(PortraitSize, PortraitSize, TextureFormat.RGBA32, false) { name = "DeckInspectorPortrait" };
            _portrait.SetPixels32(pixels);
            _portrait.Apply(false, true);
            _portrait.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _portraitArtScale = scale;
            return _portrait;
        }
    }
}
