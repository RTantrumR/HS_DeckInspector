using System;
using BepInEx.Configuration;
using Blizzard.T5.AssetManager;
using HarmonyLib;
using UnityEngine;

namespace HsDeckInspector
{
    /// <summary>
    /// The panel that rebinds the deck keys, opened by a button added to the game's Options menu. Its parts are copies of
    /// the Sound Options popup's children, never the prefab itself: its root's SoundOptionsMenu would tie them to the
    /// game's sound settings. Each row is a label over a button showing the bound key: click it, press a key.
    /// </summary>
    internal static class KeyPanel
    {
        private const string SoundMenuPrefab = "OptionsMenu_Sound_phone.prefab:67dc77661d359d84ea46fbe496ee81d7";
        private const string Title = "Deck Keys";
        private const string Listening = "Press a key";
        private const float RefusalSeconds = 3f;

        // The rows' centres, from the frame's top, as shares of its height: one in each of the frame's dark insets.
        private const float FirstRow = 0.235f, SecondRow = 0.67f;

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

        public static bool Shown => _root != null && _root.activeSelf;

        public static void Show()
        {
            if (_root == null && (_failed || !Build())) return;
            foreach (var row in _rows) ShowRow(row);
            UniversalInputManager.Get().SetGameDialogActive(true);
            InspectInputBlock.HoldEscape(true);
            _root.SetActive(true);
            AnimationUtil.ShowWithPunch(_root, 0.01f * _normalScale, 1.1f * _normalScale, _normalScale, null, noFade: true);
        }

        private static void Hide()
        {
            if (!Shown) return;
            _listening = null;
            UniversalInputManager.Get().SetGameDialogActive(false);
            InspectInputBlock.HoldEscape(false);
            _root.SetActive(false);
        }

        private static void Back()
        {
            Hide();
            var menu = OptionsMenu.Get();
            if (menu != null && !menu.IsShown()) menu.Show();
        }

        public static void Update()
        {
            if (!Shown) return;
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
            var other = _rows[0] == row ? _rows[1] : _rows[0];
            string refusal = KeyRules.Refusal(key, other.Key.Value);
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

            var firstLabel = CopyText(label, "FirstKeyLabel");
            var secondLabel = CopyText(label, "SecondKeyLabel");
            var bright = Find(t, "MonoSoundCheckbox")?.GetComponent<CheckBox>()?.m_uberText;
            if (bright != null) firstLabel.TextColor = secondLabel.TextColor = bright.TextColor;
            var copy = UnityEngine.Object.Instantiate(restore.gameObject, restore.parent, false);
            copy.name = "SecondKeyButton";
            DropCopiedText(copy.transform);

            // Everything the sound settings put here goes; what stays is the frame and these parts.
            foreach (var bar in _root.GetComponentsInChildren<ScrollbarControl>(true)) bar.gameObject.SetActive(false);
            foreach (var box in _root.GetComponentsInChildren<CheckBox>(true)) box.gameObject.SetActive(false);
            foreach (var text in _root.GetComponentsInChildren<UberText>(true))
            {
                var at = text.transform;
                if (at.IsChildOf(header) || at.IsChildOf(left) || at.IsChildOf(right) || at.IsChildOf(restore) ||
                    at.IsChildOf(copy.transform) || text == firstLabel || text == secondLabel) continue;
                text.gameObject.SetActive(false);
            }

            var title = Find(header, "UberText")?.GetComponent<UberText>();
            if (title != null) title.Text = Title;

            _rows = new[]
            {
                NewRow(Plugin.InspectKey, "Show your deck", firstLabel, keyButton),
                NewRow(Plugin.OpponentKey, "Show your opponent's deck", secondLabel, copy.GetComponentInChildren<UIBButton>(true)),
            };

            var frame = Bounds(Find(t, "OptionFrame"));
            if (frame.size.z > 0f)
            {
                PlaceRow(_rows[0], frame, FirstRow);
                PlaceRow(_rows[1], frame, SecondRow);
            }
            else
            {
                Plugin.Log.LogWarning("Key panel: can't measure the frame; the rows stay where the prefab has them");
            }

            left.GetComponentInChildren<UIBButton>(true)?.AddEventListener(UIEventType.RELEASE, e => Back());
            right.GetComponentInChildren<UIBButton>(true)?.AddEventListener(UIEventType.RELEASE, e => Hide());

            string parts = "";
            foreach (Transform child in t) parts += " " + child.name;
            Plugin.Log.LogInfo($"Key panel built; frame {frame.center} size {frame.size}; parts:{parts}");
            _root.SetActive(false);
            return true;
        }

        private static Row NewRow(ConfigEntry<KeyCode> key, string label, UberText title, UIBButton button)
        {
            var row = new Row { Key = key, Label = label, Title = title, Button = button, TitleColor = title.TextColor };
            button.AddEventListener(UIEventType.RELEASE, e => Click(row));
            return row;
        }

        /// <summary>Label over button, centred across the frame, around a height given from its top.</summary>
        private static void PlaceRow(Row row, Bounds frame, float fromTop)
        {
            float z = frame.max.z - frame.size.z * fromTop;
            var button = row.Button.transform;
            var size = Bounds(button);
            float offset = button.position.x - size.center.x;
            button.position = new Vector3(frame.center.x + offset, button.position.y, z - size.size.z * 0.55f - (size.center.z - button.position.z));

            var text = row.Title;
            float scale = Mathf.Abs(text.transform.lossyScale.x);
            if (scale > 0f) text.Width = frame.size.x * 0.8f / scale;
            text.Alignment = UberText.AlignmentOptions.Center;
            text.Anchor = UberText.AnchorOptions.Middle;
            text.transform.position = new Vector3(frame.center.x, text.transform.position.y, z + size.size.z * 0.45f);
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
