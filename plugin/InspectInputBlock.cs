using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HsDeckInspector
{
    /// <summary>
    /// Keeps the game's hands off the mouse while the deck is out, so dragging it can't pick up a card.
    ///
    /// InputManager and PegUI ask the static InputCollection, which takes the answer of the first IInput that
    /// reports a call as handled; this one goes first. The plugin itself reads UnityEngine.Input, past all of this.
    /// A button held when the deck goes back stays hidden until it is released, or the game would see a release
    /// with no press. Esc goes back on its key-up, which is when the game opens its menu, so the block outlives
    /// that frame.
    /// </summary>
    internal sealed class InspectInputBlock : IInput
    {
        /// <summary>Off screen: nothing hovers, so no card pops up as the mouse crosses the board.</summary>
        private static readonly Vector3 NoPointer = new Vector3(-200f, -200f, 0f);

        private static InspectInputBlock _instance;
        private static bool _active;
        private static bool _releasePending;
        private static int _escapeUntilFrame = -1;

        public static void Install()
        {
            if (_instance != null) return;
            var inputs = AccessTools.StaticFieldRefAccess<List<IInput>>(typeof(InputCollection), "m_Inputs");
            if (inputs == null)
            {
                Plugin.Log.LogWarning("InputCollection.m_Inputs not found; clicks on the deck reach the board");
                return;
            }
            _instance = new InspectInputBlock();
            inputs.Insert(0, _instance);
        }

        public static void Set(bool active)
        {
            if (active) _releasePending = true;
            else if (_active) _escapeUntilFrame = Time.frameCount + 1;
            _active = active;
        }

        private static bool AnyButton() =>
            Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) ||
            Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2);

        private static bool Blocking()
        {
            if (_active) return true;
            if (_releasePending && AnyButton()) return true;
            _releasePending = false;
            return false;
        }

        private static bool Escape(KeyCode key) =>
            key == KeyCode.Escape && (_active || Time.frameCount <= _escapeUntilFrame);

        public bool GetMouseButton(int button, out bool value) { value = false; return Blocking(); }
        public bool GetMouseButtonDown(int button, out bool value) { value = false; return Blocking(); }
        public bool GetMouseButtonUp(int button, out bool value) { value = false; return Blocking(); }

        public bool GetMousePosition(out Vector3 position) { position = NoPointer; return _active; }

        public bool GetAnyKey(out bool value) { value = false; return false; }
        public bool GetKey(KeyCode keycode, out bool value) { value = false; return Escape(keycode); }
        public bool GetKeyDown(KeyCode keycode, out bool value) { value = false; return Escape(keycode); }
        public bool GetKeyUp(KeyCode keycode, out bool value) { value = false; return Escape(keycode); }
    }
}
