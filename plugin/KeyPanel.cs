using System;
using System.Linq;
using BepInEx.Configuration;
using Blizzard.T5.AssetManager;
using HarmonyLib;
using UnityEngine;

namespace HsDeckInspector
{
    /// <summary>
    /// The panel that rebinds the addon's keys, opened by a button added to the game's Options menu. Its parts are copies of
    /// the Sound Options popup's children, never the prefab itself: its root's SoundOptionsMenu would tie them to the
    /// game's sound settings. Each row is a label over a button showing the bound key: click it, press a key.
    ///
    /// The popup is cut to its rows: the frame and its two insets are meshes of their own, each three stacked slices
    /// (Top, a stretched Middle, Bottom) of a left end, a stretched centre and a right end. The upper inset is narrowed
    /// and shortened with the frame, every other row gets a copy of it, and the frame's Middle takes up the difference.
    /// </summary>
    internal static class KeyPanel
    {
        private const string SoundMenuPrefab = "OptionsMenu_Sound_phone.prefab:67dc77661d359d84ea46fbe496ee81d7";
        private const string Title = "Deck Keys";
        private const string Listening = "Press a key";
        private const float RefusalSeconds = 3f;
        private const float WidthShare = 0.62f; // of the prefab's width: still clears the title plate and the buttons
        private const float InsetShare = 0.86f; // of the upper inset's height: a label and a button, with some air
        private const float LabelShare = 0.85f; // of an inset's width
        private const float PopSeconds = 0.25f;
        private const float ShownScale = 1.1f; // of the prefab's scale: the size the panel was tuned at

        private sealed class Row
        {
            public ConfigEntry<KeyCode> Key;
            public string Label;
            public UberText Title;
            public UIBButton Button;
            public Color TitleColor;
            public float RefusedUntil;
        }

        private static readonly Color RefusedColor = new Color(0.85f, 0.15f, 0.1f);

        private static object _prefabHandle; // the asset handle, kept: a dropped one frees its asset
        private static GameObject _root;
        private static Vector3 _normalScale;
        private static Row[] _rows;
        private static Row _listening;
        private static bool _failed;
        private static bool _open;

        public static bool Shown => _root != null && _root.activeSelf;

        public static void Show()
        {
            if (_root == null && (_failed || !Build())) return;
            foreach (var row in _rows) ShowRow(row);
            UniversalInputManager.Get().SetGameDialogActive(true);
            InspectInputBlock.HoldEscape(true);
            _open = true;
            _root.SetActive(true);
            // Not AnimationUtil.ShowWithPunch: it runs a coroutine on the object's own MonoBehaviour, and this has none.
            _root.transform.localScale = 0.01f * _normalScale;
            iTween.ScaleTo(_root, ShownScale * _normalScale, PopSeconds);
        }

        private static void Hide()
        {
            if (!Shown) return;
            Release();
            _root.SetActive(false);
        }

        private static void Release()
        {
            _open = false;
            _listening = null;
            UniversalInputManager.Get()?.SetGameDialogActive(false);
            InspectInputBlock.HoldEscape(false);
        }

        private static void Back()
        {
            Hide();
            var menu = OptionsMenu.Get();
            if (menu != null && !menu.IsShown()) menu.Show();
        }

        public static void Update()
        {
            if (!Shown)
            {
                if (_open) Release(); // something took the panel away: Esc and the game's input have to come back
                return;
            }
            foreach (var row in _rows)
            {
                if (row.RefusedUntil > 0f && Time.realtimeSinceStartup >= row.RefusedUntil) ShowLabel(row);
            }
            if (Input.GetKeyUp(KeyCode.Escape))
            {
                if (_listening != null) StopListening();
                else Hide();
                return;
            }
            if (_listening == null || !Input.anyKeyDown) return;
            foreach (var key in KeyRules.Keyboard)
            {
                if (key != KeyCode.Escape && Input.GetKeyDown(key))
                {
                    Pressed(key);
                    return;
                }
            }
        }

        private static void Pressed(KeyCode key)
        {
            var row = _listening;
            string refusal = KeyRules.Refusal(key, _rows.Where(r => r != row).Select(r => r.Key.Value));
            if (refusal != null)
            {
                row.Title.Text = refusal;
                row.Title.TextColor = RefusedColor;
                row.RefusedUntil = Time.realtimeSinceStartup + RefusalSeconds;
                return; // still listening
            }
            row.Key.Value = key;
            Plugin.Log.LogInfo($"{row.Label}: {key}");
            StopListening();
        }

        private static void Click(Row row)
        {
            bool same = _listening == row;
            StopListening();
            if (same) return;
            _listening = row;
            row.Button.SetText(Listening);
        }

        private static void StopListening()
        {
            var row = _listening;
            _listening = null;
            if (row != null) ShowRow(row);
        }

        private static void ShowRow(Row row)
        {
            row.Button.SetText(KeyRules.Name(row.Key.Value));
            ShowLabel(row);
        }

        private static void ShowLabel(Row row)
        {
            row.RefusedUntil = 0f;
            row.Title.Text = row.Label;
            row.Title.TextColor = row.TitleColor;
        }

        private static bool Build()
        {
            GameObject prefab = null;
            try
            {
                var handle = AssetLoader.Get().LoadAsset<GameObject>(SoundMenuPrefab);
                _prefabHandle = handle;
                prefab = handle?.Asset;
            }
            catch (Exception e) { Plugin.Log.LogWarning("Key panel: " + e.Message); }
            if (prefab == null)
            {
                _failed = true;
                Plugin.Log.LogWarning("Key panel: no Sound Options popup to build from");
                return false;
            }

            int layer = prefab.layer;
            _root = new GameObject("DeckKeyPanel") { layer = layer };
            _root.transform.localPosition = prefab.transform.localPosition;
            _root.transform.localRotation = prefab.transform.localRotation;
            _root.transform.localScale = prefab.transform.localScale;
            OverlayUI.Get().AddGameObject(_root);
            _normalScale = _root.transform.localScale;
            foreach (Transform child in prefab.transform)
                UnityEngine.Object.Instantiate(child.gameObject, _root.transform, false).name = child.name;
            LayerUtils.SetLayer(_root, layer);

            var blocker = CameraUtils.CreateInputBlocker(CameraUtils.FindFirstByLayer(layer), "DeckKeyPanelInputBlocker",
                                                         _root.transform, _root.transform, 10f);
            blocker.layer = layer;
            blocker.AddComponent<PegUIElement>().AddEventListener(UIEventType.RELEASE, e => Hide());

            var t = _root.transform;
            var header = Find(t, "Header");
            var left = Find(t, "Button_left");
            var right = Find(t, "Button_right");
            var restore = Find(t, "RestoreDefaultButton");
            var label = Find(t, "MasterVolumeLabel")?.GetComponent<UberText>();
            var keyButton = restore != null ? restore.GetComponentInChildren<UIBButton>(true) : null;
            if (header == null || left == null || right == null || keyButton == null || label == null)
            {
                Plugin.Log.LogWarning("Key panel: the Sound Options popup has changed");
                UnityEngine.Object.Destroy(_root);
                _root = null;
                _failed = true;
                return false;
            }

            var labels = new[] { CopyText(label, "FirstKeyLabel"), CopyText(label, "SecondKeyLabel"), CopyText(label, "ThirdKeyLabel") };
            var bright = Find(t, "MonoSoundCheckbox")?.GetComponent<CheckBox>()?.m_uberText;
            if (bright != null)
                foreach (var text in labels) text.TextColor = bright.TextColor;
            var second = CopyButton(restore, "SecondKeyButton");
            var third = CopyButton(restore, "ThirdKeyButton");

            // Everything the sound settings put here goes; what stays is the frame and these parts.
            foreach (var bar in _root.GetComponentsInChildren<ScrollbarControl>(true)) bar.gameObject.SetActive(false);
            foreach (var box in _root.GetComponentsInChildren<CheckBox>(true)) box.gameObject.SetActive(false);
            foreach (var text in _root.GetComponentsInChildren<UberText>(true))
            {
                var at = text.transform;
                if (at.IsChildOf(header) || at.IsChildOf(left) || at.IsChildOf(right) || at.IsChildOf(restore) ||
                    at.IsChildOf(second) || at.IsChildOf(third) || labels.Contains(text)) continue;
                text.gameObject.SetActive(false);
            }

            var title = Find(header, "UberText")?.GetComponent<UberText>();
            if (title != null) title.Text = Title;

            _rows = new[]
            {
                NewRow(Plugin.InspectKey, "Show your deck", labels[0], keyButton),
                NewRow(Plugin.OpponentKey, "Show your opponent's deck", labels[1], second.GetComponentInChildren<UIBButton>(true)),
                NewRow(Plugin.LayKey, "Lay decks on the board", labels[2], third.GetComponentInChildren<UIBButton>(true)),
            };

            if (!Shrink(t, _rows.Length, out var insets, out float labelWidth))
            {
                Plugin.Log.LogWarning("Key panel: can't measure the frame; the rows stay where the prefab has them");
            }
            else
            {
                float centre = Bounds(t.Find("OptionFrame")).center.x;
                for (int i = 0; i < _rows.Length; i++) PlaceRow(_rows[i], centre, insets[i], labelWidth);
            }

            left.GetComponentInChildren<UIBButton>(true)?.AddEventListener(UIEventType.RELEASE, e => Back());
            right.GetComponentInChildren<UIBButton>(true)?.AddEventListener(UIEventType.RELEASE, e => Hide());

            var frame = Bounds(t.Find("OptionFrame"));
            Plugin.Log.LogInfo($"Key panel built; frame {frame.center} size {frame.size}");
            _root.SetActive(false);
            return true;
        }

        private static Transform CopyButton(Transform source, string name)
        {
            var copy = UnityEngine.Object.Instantiate(source.gameObject, source.parent, false);
            copy.name = name;
            DropCopiedText(copy.transform);
            return copy.transform;
        }

        private static Row NewRow(ConfigEntry<KeyCode> key, string label, UberText title, UIBButton button)
        {
            var row = new Row { Key = key, Label = label, Title = title, Button = button, TitleColor = title.TextColor };
            button.AddEventListener(UIEventType.RELEASE, e => Click(row));
            return row;
        }

        /// <summary>
        /// Cuts the popup to one small inset per row, keeping the spacing it has, and returns their centres and how wide
        /// a label can be. Everything is measured: the frame's slices sit at large opposite offsets, so their numbers say
        /// little. The prefab has two insets, so a further row grows the frame back by one inset and gap: a negative cut.
        /// </summary>
        private static bool Shrink(Transform root, int rows, out float[] insets, out float labelWidth)
        {
            insets = null;
            labelWidth = 0f;
            var frame = root.Find("OptionFrame");
            var upper = root.Find("OptionSection1");
            var lower = root.Find("OptionSection2");
            var middle = frame != null ? frame.Find("Middle") : null;
            var bottom = frame != null ? frame.Find("Bottom") : null;
            var buttons = root.Find("Button_Framed_Double (1)");
            if (upper == null || lower == null || middle == null || bottom == null || buttons == null) return false;

            var whole = Bounds(frame);
            var top = Bounds(upper);
            var low = Bounds(lower);
            if (whole.size.z <= 0f || top.size.z <= 0f || low.size.z <= top.size.z) return false;
            float gap = top.min.z - low.max.z;

            float narrower = whole.size.x * (1f - WidthShare);
            foreach (Transform slice in frame) Narrow(slice, narrower);
            foreach (Transform slice in upper) Narrow(slice, narrower);

            float insetCut = top.size.z * (1f - InsetShare);
            foreach (Transform slice in upper.Find("Middle")) Trim(slice, insetCut);
            upper.Find("Bottom").position += new Vector3(0f, 0f, insetCut);
            top = Bounds(upper);

            float shift = -(top.size.z + gap);
            float cut = low.size.z + insetCut - top.size.z + shift * (rows - 2);

            for (int i = 1; i < rows; i++)
            {
                var copy = UnityEngine.Object.Instantiate(upper.gameObject, root, false);
                copy.name = "OptionSectionSmall" + i;
                copy.transform.position = upper.position + new Vector3(0f, 0f, shift * i);
            }
            lower.gameObject.SetActive(false);

            foreach (Transform slice in middle) Trim(slice, cut);
            bottom.position += new Vector3(0f, 0f, cut);
            buttons.position += new Vector3(0f, 0f, cut);

            var clicks = root.Find("MenuClickArea");
            if (clicks != null)
            {
                var size = clicks.localScale;
                clicks.localScale = new Vector3(size.x * WidthShare, size.y, size.z * (whole.size.z - cut) / whole.size.z);
                clicks.position += new Vector3(0f, 0f, cut / 2f);
            }

            root.position += new Vector3(0f, 0f, -cut / 2f); // back to the middle of the screen
            insets = new float[rows];
            for (int i = 0; i < rows; i++) insets[i] = top.center.z - cut / 2f + shift * i;
            labelWidth = top.size.x * LabelShare;
            Plugin.Log.LogInfo($"Key panel cut by {narrower:0.##} of {whole.size.x:0.##} and {cut:0.##} of {whole.size.z:0.##}; inset {top.size}, gap {gap:0.##}");
            return true;
        }

        /// <summary>Takes a width off a row of a left end, an optional stretched centre and a right end, which close in.</summary>
        private static void Narrow(Transform row, float cut)
        {
            Transform left = null, right = null, centre = null;
            float leftX = float.MaxValue, rightX = float.MinValue;
            foreach (Transform piece in row)
            {
                float x = Bounds(piece).center.x;
                if (x < leftX) { leftX = x; left = piece; }
                if (x > rightX) { rightX = x; right = piece; }
            }
            foreach (Transform piece in row)
                if (piece != left && piece != right) centre = piece;
            if (left == null || right == left) return;
            left.position += new Vector3(cut / 2f, 0f, 0f);
            right.position -= new Vector3(cut / 2f, 0f, 0f);
            if (centre == null) return;
            var before = Bounds(centre);
            if (before.size.x <= cut) return;
            var scale = centre.localScale;
            centre.localScale = new Vector3(scale.x * (before.size.x - cut) / before.size.x, scale.y, scale.z);
            centre.position += new Vector3(before.center.x - Bounds(centre).center.x, 0f, 0f);
        }

        /// <summary>Takes a height off a slice, its top edge staying put.</summary>
        private static void Trim(Transform slice, float cut)
        {
            var before = Bounds(slice);
            if (before.size.z <= cut) return;
            var scale = slice.localScale;
            slice.localScale = new Vector3(scale.x, scale.y, scale.z * (before.size.z - cut) / before.size.z);
            slice.position += new Vector3(0f, 0f, before.max.z - Bounds(slice).max.z);
        }

        /// <summary>Label over button, centred across the frame, around a height.</summary>
        private static void PlaceRow(Row row, float centre, float z, float labelWidth)
        {
            var button = row.Button.transform;
            var size = Bounds(button);
            float offset = button.position.x - size.center.x;
            button.position = new Vector3(centre + offset, button.position.y, z - size.size.z * 0.35f - (size.center.z - button.position.z));

            var text = row.Title;
            float scale = Mathf.Abs(text.transform.lossyScale.x);
            if (scale > 0f) text.Width = labelWidth / scale;
            text.Alignment = UberText.AlignmentOptions.Center;
            text.Anchor = UberText.AnchorOptions.Middle;
            text.transform.position = new Vector3(centre, text.transform.position.y, z + size.size.z * 0.55f);
            Plugin.Log.LogInfo($"Key panel row: button {size.size}, row at {z}");
        }

        /// <summary>A copy of a text, on the panel's root: the original sits under a slider that gets hidden. The copy
        /// brings the original's rendered text object along; UberText makes its own.</summary>
        private static UberText CopyText(UberText text, string name)
        {
            var value = UnityEngine.Object.Instantiate(text.gameObject, _root.transform, true);
            value.name = name;
            DropCopiedText(value.transform);
            var copy = value.GetComponent<UberText>();
            copy.WordWrap = false;
            copy.ResizeToFit = true;
            return copy;
        }

        /// <summary>Drops what a copied text brought of its original's rendering, which would draw the original's words.</summary>
        internal static void DropCopiedText(Transform copy)
        {
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("UberText_RenderObject")) UnityEngine.Object.Destroy(t.gameObject);
        }

        internal static Bounds Bounds(Transform t)
        {
            var b = new Bounds();
            bool any = false;
            if (t == null) return b;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }

    /// <summary>The button that opens the panel, a copy of Sound Options under the Signature Card Appearance dropdown.</summary>
    [HarmonyPatch(typeof(OptionsMenu), "ShowOrHide")]
    internal static class OptionsMenuEntry
    {
        private const string Label = "Deck Keys";
        private const float Gap = 0.25f; // of the button's height

        private static UIBButton _button;

        private static void Postfix(OptionsMenu __instance, bool showOrHide)
        {
            if (!showOrHide || (bool)UniversalInputManager.UsePhoneUI) return;
            try
            {
                if (_button == null) Build(__instance);
                if (_button != null) Place(__instance);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Options entry: " + e); }
        }

        private static void Build(OptionsMenu menu)
        {
            var source = menu.m_soundOptionsButton;
            var anchor = menu.m_signatureTextDisplayDropdown;
            if (source == null || anchor == null) return;
            // On the menu's root, never in a pane: the game lays its panes out by measuring what is in them, so a
            // button there would move the whole column.
            var go = UnityEngine.Object.Instantiate(source.gameObject, menu.transform, true);
            go.name = "DeckKeysButton";
            go.SetActive(true);
            KeyPanel.DropCopiedText(go.transform);
            _button = go.GetComponent<UIBButton>() ?? go.GetComponentInChildren<UIBButton>(true);
            _button.SetText(Label);
            _button.AddEventListener(UIEventType.RELEASE, e =>
            {
                OptionsMenu.Get()?.Hide(false);
                KeyPanel.Show();
            });
        }

        /// <summary>Done at every opening: the game lays its panes out as it shows them.</summary>
        private static void Place(OptionsMenu menu)
        {
            var anchor = menu.m_signatureTextDisplayDropdown;
            var under = KeyPanel.Bounds(anchor.transform);
            var size = KeyPanel.Bounds(_button.transform);
            if (under.size.z <= 0f || size.size.z <= 0f)
            {
                Plugin.Log.LogWarning("Options entry: can't measure the dropdown or the button");
                return;
            }
            float z = under.min.z - size.size.z * (0.5f + Gap);
            var move = new Vector3(under.center.x - size.center.x, 0f, z - size.center.z);
            _button.transform.position += move;
            Plugin.Log.LogInfo($"Options entry: under {under.center} size {under.size}, button {size.size}");
        }
    }
}
