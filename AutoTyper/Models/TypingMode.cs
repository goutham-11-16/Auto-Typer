namespace AutoTyper.Models
{
    public enum TypingMode
    {
        Paste,      // Clipboard + Ctrl+V
        HumanLike,  // Character by character with delay
        Fast,       // Character by character, no delay
        Macro       // Supports special keys {ENTER}, {TAB}, etc.
    }
}
