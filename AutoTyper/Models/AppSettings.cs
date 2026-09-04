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

        // Global Emergency Hotkey & IPC Settings
        public System.Windows.Input.Key EmergencyStopHotkeyKey { get; set; } = System.Windows.Input.Key.F12;
        public System.Windows.Input.ModifierKeys EmergencyStopHotkeyModifiers { get; set; } = System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift;
        public bool EnableGameBarIpc { get; set; } = true;
    }
}
