using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace AutoTyper.Services
{
    public enum TokenType
    {
        Text,
        KeyPress,
        Delay
    }

    public class TypingToken
    {
        public TokenType Type { get; set; }
        public string TextValue { get; set; }
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public int DelayMs { get; set; }
    }

    public class TokenParserService
    {
        public List<TypingToken> Parse(string input)
        {
            var tokens = new List<TypingToken>();
            if (string.IsNullOrEmpty(input)) return tokens;

            // Regex to find {TAG}
            // Tags: {ENTER}, {TAB}, {BS}, {BACKSPACE}, {DELAY 100}, {CTRL+C}
            var regex = new Regex(@"(\{[^}]+\})");
            var parts = regex.Split(input);

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("{") && part.EndsWith("}"))
                {
                    var content = part.Substring(1, part.Length - 2).ToUpperInvariant();
                    var token = ParseTag(content);
                    if (token != null)
                    {
                        tokens.Add(token);
                    }
                    else
                    {
                        // If invalid tag, treat as literal text
                        tokens.Add(new TypingToken { Type = TokenType.Text, TextValue = part });
                    }
                }
                else
                {
                    tokens.Add(new TypingToken { Type = TokenType.Text, TextValue = part });
                }
            }

            return tokens;
        }

        private TypingToken ParseTag(string tag)
        {
            // Basic delay parsing {DELAY 100}
            if (tag.StartsWith("DELAY "))
            {
                if (int.TryParse(tag.Substring(6), out int ms))
                {
                    return new TypingToken { Type = TokenType.Delay, DelayMs = ms };
                }
            }

            // Key mapping
            switch (tag)
            {
                case "ENTER": return new TypingToken { Type = TokenType.KeyPress, Key = Key.Enter };
                case "TAB": return new TypingToken { Type = TokenType.KeyPress, Key = Key.Tab };
                case "BS":
                case "BACKSPACE": return new TypingToken { Type = TokenType.KeyPress, Key = Key.Back };
                case "ESC":
                case "ESCAPE": return new TypingToken { Type = TokenType.KeyPress, Key = Key.Escape };
                case "CTRL+C": return new TypingToken { Type = TokenType.KeyPress, Key = Key.C, Modifiers = ModifierKeys.Control };
                case "CTRL+V": return new TypingToken { Type = TokenType.KeyPress, Key = Key.V, Modifiers = ModifierKeys.Control };
                case "CTRL+X": return new TypingToken { Type = TokenType.KeyPress, Key = Key.X, Modifiers = ModifierKeys.Control };
                case "CTRL+A": return new TypingToken { Type = TokenType.KeyPress, Key = Key.A, Modifiers = ModifierKeys.Control };
                // Add more as needed
                default: 
                    // Try to generic parse? For now return null to treat as text
                    return null;
            }
        }
    }
}
