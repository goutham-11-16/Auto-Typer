using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AutoTyper.Models;

namespace AutoTyper.Services
{
    public class InputService
    {
        private readonly TokenParserService _parser;

        public InputService()
        {
            _parser = new TokenParserService();
        }

        public async Task TypeTextAsync(string text, TypingMode mode, int delayPerChar = 0, int delayPerWord = 0, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Ensure we are not blocking the UI thread if called synchronously
            await Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (mode)
                {
                    case TypingMode.Paste:
                        await TypePasteAsync(text);
                        break;
                    case TypingMode.Fast:
                        await TypeRawAsync(text, delayPerChar, delayPerWord, cancellationToken);
                        break;
                    case TypingMode.HumanLike:
                        await TypeHumanLikeAsync(text, delayPerWord, cancellationToken);
                        break;
                    case TypingMode.Macro:
                        await TypeMacroAsync(text, delayPerChar, delayPerWord, cancellationToken);
                        break;
                }
            }, cancellationToken);
        }

        private async Task TypePasteAsync(string text)
        {
            // Note: Clipboard access must be on STA thread. 
            // Since we are on a Task.Run thread, we need to dispatch to UI thread for clipboard set.
            // But getting focus back is tricky. 
            // Strategy: 
            // 1. Save current clipboard
            // 2. Set new text
            // 3. Send Ctrl+V
            // 4. Restore clipboard (optional/tricky due to timing)

            string originalText = null;
            bool success = false;
            
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try 
                {
                    if (System.Windows.Clipboard.ContainsText()) originalText = System.Windows.Clipboard.GetText();
                    System.Windows.Clipboard.SetText(text);
                    success = true;
                }
                catch { /* Clipboard is often locked */ }
            });

            if (success)
            {
                SendModifierKey(Key.LeftCtrl, true);
                SendKey(Key.V);
                SendModifierKey(Key.LeftCtrl, false);
                
                // Small delay to allow paste to happen before restoring clipboard
                await Task.Delay(100); 

                // Optional: Restore clipboard? 
                // Users might prefer the snippet to stay in clipboard. keeping it simple for now.
            }
        }

        private async Task TypeRawAsync(string text, int delayChar, int delayWord, CancellationToken cancellationToken)
        {
            foreach (char c in text)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (c == '\r') continue; // Skip CR, handle LF

                bool isWordBoundary = false;
                if (c == '\n')
                {
                    SendKey(Key.Enter);
                    isWordBoundary = true;
                }
                else if (c == '\t')
                {
                    SendKey(Key.Tab);
                }
                else 
                {
                    SendChar(c);
                    if (c == ' ') isWordBoundary = true;
                }

                if (isWordBoundary && delayWord > 0)
                {
                    await Task.Delay(delayWord, cancellationToken);
                }
                else if (!isWordBoundary && delayChar > 0)
                {
                    await Task.Delay(delayChar, cancellationToken);
                }
            }
        }

        private async Task TypeHumanLikeAsync(string text, int delayWord, CancellationToken cancellationToken)
        {
            var rand = new Random();
            foreach (char c in text)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (c == '\r') continue;

                if (c == '\n')
                {
                    SendKey(Key.Enter);
                    if (delayWord > 0) await Task.Delay(delayWord, cancellationToken);
                    else await Task.Delay(rand.Next(50, 150), cancellationToken); // Natural pause at line end
                    continue;
                }
                else if (c == '\t')
                {
                    SendKey(Key.Tab);
                    continue;
                }

                SendChar(c);
                
                if (c == ' ')
                {
                     // Word boundary
                     if (delayWord > 0) await Task.Delay(delayWord, cancellationToken);
                     else await Task.Delay(rand.Next(30, 80), cancellationToken);
                }
                else
                {
                    int delay = rand.Next(10, 60); // 10-60ms variant
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        private async Task TypeMacroAsync(string text, int delayChar, int delayWord, CancellationToken cancellationToken)
        {
            var tokens = _parser.Parse(text);
            foreach (var token in tokens)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (token.Type == TokenType.Text)
                {
                    // Use TypeRaw logic for text parts but with async delays
                   foreach (char c in token.TextValue)
                   {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (c == '\r') continue; 

                        bool isWordBoundary = false;
                        if (c == '\n')
                        {
                            SendKey(Key.Enter);
                            isWordBoundary = true;
                        }
                        else if (c == '\t')
                        {
                            SendKey(Key.Tab);
                        }
                        else 
                        {
                            SendChar(c);
                            if (c == ' ') isWordBoundary = true;
                        }

                        if (isWordBoundary && delayWord > 0)
                        {
                            await Task.Delay(delayWord, cancellationToken);
                        }
                        else if (!isWordBoundary && delayChar > 0)
                        {
                            await Task.Delay(delayChar, cancellationToken);
                        }
                        else if (!isWordBoundary)
                        {
                            // Minimal default delay for macro text to ensure stability if no delay set
                             await Task.Delay(2, cancellationToken);
                        }
                   }
                }
                else if (token.Type == TokenType.KeyPress)
                {
                    if ((token.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) SendModifierKey(Key.LeftCtrl, true);
                    if ((token.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) SendModifierKey(Key.LeftAlt, true);
                    if ((token.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) SendModifierKey(Key.LeftShift, true);

                    SendKey(token.Key);

                    if ((token.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) SendModifierKey(Key.LeftShift, false);
                    if ((token.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt) SendModifierKey(Key.LeftAlt, false);
                    if ((token.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) SendModifierKey(Key.LeftCtrl, false);
                }
                else if (token.Type == TokenType.Delay)
                {
                    await Task.Delay(token.DelayMs, cancellationToken);
                }
            }
        }

        public void SendChar(char c)
        {
            var inputs = new NativeMethods.INPUT[2];
            
            // KEY_DOWN
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].u.ki.wScan = (ushort)c;
            inputs[0].u.ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE; 

            // KEY_UP
            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].u.ki.wScan = (ushort)c;
            inputs[1].u.ki.dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP;

            NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }

        public void SendKey(Key key)
        {
            int vKey = KeyInterop.VirtualKeyFromKey(key);
            
            var inputs = new NativeMethods.INPUT[2];
            
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = (ushort)vKey;
            
            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = (ushort)vKey;
            inputs[1].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }

        private void SendModifierKey(Key key, bool down)
        {
            int vKey = KeyInterop.VirtualKeyFromKey(key);
            var inputs = new NativeMethods.INPUT[1];
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = (ushort)vKey;
            inputs[0].u.ki.dwFlags = down ? 0 : NativeMethods.KEYEVENTF_KEYUP;
            
            NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        }
    }
}
