using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AutoTyper.Models
{
    public class Snippet : INotifyPropertyChanged
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); OnPropertyChanged("", "HotKeyDisplay"); }
        }

        private string _name = "New Snippet";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _text = "";
        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(); }
        }

        private ModifierKeys _hotKeyModifiers;
        public ModifierKeys HotKeyModifiers
        {
            get => _hotKeyModifiers;
            set { _hotKeyModifiers = value; OnPropertyChanged("", "HotKeyDisplay"); }
        }

        private Key _hotKeyKey;
        public Key HotKeyKey
        {
            get => _hotKeyKey;
            set { _hotKeyKey = value; OnPropertyChanged("", "HotKeyDisplay"); }
        }

        private TypingMode _mode = TypingMode.HumanLike;
        public TypingMode Mode
        {
            get => _mode;
            set { _mode = value; OnPropertyChanged(); }
        }

        private int _delayPerChar = 10;
        public int DelayPerChar
        {
            get => _delayPerChar;
            set { _delayPerChar = value; OnPropertyChanged(); }
        }

        private int _delayPerWord = 30;
        public int DelayPerWord
        {
            get => _delayPerWord;
            set { _delayPerWord = value; OnPropertyChanged(); }
        }
        
        public string HotKeyDisplay => $"{HotKeyModifiers} + {HotKeyKey}";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null, string? otherProperty = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            if (otherProperty != null) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(otherProperty));
        }

        public Snippet Clone()
        {
            return new Snippet
            {
                Id = this.Id, // Keep ID for tracking identity matches
                IsEnabled = this.IsEnabled,
                Name = this.Name,
                Text = this.Text,
                Mode = this.Mode,
                DelayPerChar = this.DelayPerChar,
                DelayPerWord = this.DelayPerWord,
                HotKeyModifiers = this.HotKeyModifiers,
                HotKeyKey = this.HotKeyKey
            };
        }

        public void CopyFrom(Snippet other)
        {
            if (other == null) return;
            // Id is NOT copied typically if we are updating existing, but here identity matches.
            IsEnabled = other.IsEnabled;
            Name = other.Name;
            Text = other.Text;
            Mode = other.Mode;
            DelayPerChar = other.DelayPerChar;
            DelayPerWord = other.DelayPerWord;
            HotKeyModifiers = other.HotKeyModifiers;
            HotKeyKey = other.HotKeyKey;
        }
    }
}
