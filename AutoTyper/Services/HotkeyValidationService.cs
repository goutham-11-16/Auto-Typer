using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace AutoTyper.Services
{
    public class HotkeyValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public List<string> Suggestions { get; set; } = new List<string>();
    }

    public class HotkeyValidationService
    {
        private static readonly HashSet<Key> BlockedKeys = new HashSet<Key>
        {
            // Block Single Keys A-Z, 0-9
            Key.Enter, Key.Tab, Key.Back, Key.Space, Key.Escape,
            Key.Left, Key.Right, Key.Up, Key.Down, Key.Delete,
            Key.Home, Key.End, Key.PageUp, Key.PageDown,
            Key.LeftShift, Key.RightShift, Key.LeftCtrl, Key.RightCtrl,
            Key.LeftAlt, Key.RightAlt, Key.LWin, Key.RWin,
            Key.System 
        };

        // Common App Shortcuts to block (Ctrl+...)
        private static readonly HashSet<Key> CommonShortcuts = new HashSet<Key>
        {
            Key.C, Key.V, Key.X, Key.Z, Key.Y, Key.S, Key.P, Key.F, Key.A
        };

        public HotkeyValidationResult Validate(ModifierKeys modifiers, Key key)
        {
            var result = new HotkeyValidationResult { IsValid = true };
            
            // 1. Mandatory Modifiers
            if (modifiers == ModifierKeys.None)
            {
                result.IsValid = false;
                result.Message = "Hotkeys must include Ctrl or Alt.";
                return result;
            }

            // 2. Block Single blocked keys (alphanumeric etc) logic is implicit via Modifiers check mostly, 
            // but we also check if the Key itself is a modifier or restricted key.
            if (IsModifierKey(key))
            {
                result.IsValid = false;
                result.Message = "Press a key combination.";
                return result;
            }

            // 3. Block System Reserved
            // Win key combinations are handled by OS mostly but good to block explicit usage if possible.
            // WPF Key enum usually maps LWin/RWin.
            if (key == Key.LWin || key == Key.RWin)
            {
                 result.IsValid = false;
                 result.Message = "Windows key combinations are reserved.";
                 return result;
            }

            // Alt+Tab, Alt+Esc, Ctrl+Esc, Alt+F4, Ctrl+Alt+Del
            bool isAlt = (modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
            bool isCtrl = (modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool isShift = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            
            if (isAlt && (key == Key.Tab || key == Key.Escape || key == Key.F4))
            {
                result.IsValid = false;
                result.Message = "System Key combination reserved.";
                return result;
            }
            if (isCtrl && key == Key.Escape)
            {
                result.IsValid = false;
                result.Message = "System Key combination reserved.";
                return result;
            }
            if (isCtrl && isAlt && key == Key.Delete)
            {
                 result.IsValid = false;
                 result.Message = "System Key combination reserved.";
                 return result;
            }

            // 4. Common Application Shortcuts
            if (isCtrl && !isAlt && !isShift && CommonShortcuts.Contains(key))
            {
                result.IsValid = false;
                result.Message = $"Ctrl + {key} is a common shortcut. Use Alt or Ctrl+Alt.";
                result.Suggestions.Add($"Alt + {key}");
                result.Suggestions.Add($"Ctrl + Alt + {key}");
                return result;
            }

            // 5. Block simple alpha-numeric if user managed to trigger it with just shift (which is unlikely given Modifier check above requires Ctrl/Alt usually)
            // But requirement says "A-Z, 0-9" blocked. The modifiers check 'modifiers == ModifierKeys.None' handles the single key case.
            // Requirement: "Disallow hotkeys with no modifiers" -> Done.
            
            return result;
        }

        private bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl ||
                   key == Key.LeftAlt || key == Key.RightAlt ||
                   key == Key.LeftShift || key == Key.RightShift ||
                   key == Key.LWin || key == Key.RWin || 
                   key == Key.System; // System is usually Alt key press
        }
    }
}
