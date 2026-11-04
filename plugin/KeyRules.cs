using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HsDeckInspector
{
    /// <summary>
    /// Which keys the deck can be bound to, and what they are called on screen. The game list is what a normal client
    /// really reads (client 37.0): most of the keys in its code sit behind IsDevMode or IsInternal.
    /// </summary>
    internal static class KeyRules
    {
        /// <summary>Everything on a keyboard: from Mouse0 up, KeyCode is mouse buttons and joysticks.</summary>
        public static readonly KeyCode[] Keyboard = System.Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>()
            .Where(k => k != KeyCode.None && k < KeyCode.Mouse0).Distinct().ToArray();

        private static readonly HashSet<KeyCode> UsedByGame = new HashSet<KeyCode>
        {
            KeyCode.Escape, // menus and popups
            KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Tab, // text boxes and chat
            KeyCode.Space, // pack opening
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, // Arena draft picks
            KeyCode.Print, KeyCode.SysReq, KeyCode.F13, // screenshots
        };

        private static readonly HashSet<KeyCode> KeptFree = new HashSet<KeyCode>
        {
            KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.AltGr,
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.LeftMeta, KeyCode.RightMeta, KeyCode.LeftWindows, KeyCode.RightWindows, KeyCode.Menu,
            KeyCode.CapsLock, KeyCode.Numlock, KeyCode.ScrollLock,
            KeyCode.Backspace, KeyCode.Delete,
        };

        private static readonly Dictionary<KeyCode, string> Names = new Dictionary<KeyCode, string>
        {
            { KeyCode.Escape, "Esc" }, { KeyCode.Return, "Enter" }, { KeyCode.KeypadEnter, "Num Enter" },
            { KeyCode.LeftAlt, "Alt" }, { KeyCode.RightAlt, "Alt" }, { KeyCode.AltGr, "Alt" },
            { KeyCode.LeftControl, "Ctrl" }, { KeyCode.RightControl, "Ctrl" },
            { KeyCode.LeftShift, "Shift" }, { KeyCode.RightShift, "Shift" },
            { KeyCode.LeftMeta, "Win" }, { KeyCode.RightMeta, "Win" },
            { KeyCode.LeftWindows, "Win" }, { KeyCode.RightWindows, "Win" },
            { KeyCode.CapsLock, "Caps Lock" }, { KeyCode.Numlock, "Num Lock" }, { KeyCode.ScrollLock, "Scroll Lock" },
            { KeyCode.Print, "Print Screen" }, { KeyCode.SysReq, "SysRq" },
            { KeyCode.BackQuote, "`" }, { KeyCode.Minus, "-" }, { KeyCode.Equals, "=" },
            { KeyCode.LeftBracket, "[" }, { KeyCode.RightBracket, "]" }, { KeyCode.Backslash, "\\" },
            { KeyCode.Semicolon, ";" }, { KeyCode.Quote, "'" }, { KeyCode.Comma, "," },
            { KeyCode.Period, "." }, { KeyCode.Slash, "/" },
            { KeyCode.UpArrow, "Up" }, { KeyCode.DownArrow, "Down" }, { KeyCode.LeftArrow, "Left" }, { KeyCode.RightArrow, "Right" },
            { KeyCode.PageUp, "Page Up" }, { KeyCode.PageDown, "Page Down" },
            { KeyCode.KeypadPlus, "Num +" }, { KeyCode.KeypadMinus, "Num -" }, { KeyCode.KeypadMultiply, "Num *" },
            { KeyCode.KeypadDivide, "Num /" }, { KeyCode.KeypadPeriod, "Num ." },
        };

        /// <summary>Why a key can't be bound, as a line for the screen; null if it can.</summary>
        public static string Refusal(KeyCode key, KeyCode otherDeck)
        {
            string name = Name(key);
            if (key == otherDeck) return $"{name} is already the other deck's key";
            if (UsedByGame.Contains(key)) return $"{name} is used by the game";
            if (KeptFree.Contains(key)) return $"{name} can't be used";
            return null;
        }

        public static string Name(KeyCode key)
        {
            if (Names.TryGetValue(key, out var name)) return name;
            string text = key.ToString();
            if (text.StartsWith("Alpha")) return text.Substring(5);
            if (text.StartsWith("Keypad")) return "Num " + text.Substring(6);
            return text;
        }
    }
}
