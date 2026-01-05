namespace AutoTyper.Models
{
    public class AppSettings
    {
        public bool IsWalkthroughCompleted { get; set; } = false;
        
        // P2 Settings
        public bool StartMinimized { get; set; } = false;
        public bool StartWithWindows { get; set; } = false;
        public TypingMode DefaultTypingMode { get; set; } = TypingMode.HumanLike;
        public int DefaultDelay { get; set; } = 10;
        public bool SafetyConfirmation { get; set; } = true;
    }
}
