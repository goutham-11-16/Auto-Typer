using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AutoTyper.Models;
using AutoTyper.Services;

namespace AutoTyper.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly HotKeyService _hotKeyService;
        private readonly StorageService _storageService;
        private readonly InputService _inputService;
        private readonly HotkeyValidationService _hotkeyValidationService;
        private CancellationTokenSource _typingCts;

        public MainViewModel()
        {
            _hotKeyService = new HotKeyService();
            _storageService = new StorageService();
            _inputService = new InputService();
            _hotkeyValidationService = new HotkeyValidationService();

            Snippets = new ObservableCollection<Snippet>(_storageService.LoadSnippets());
            
            AddSnippetCommand = new RelayCommand(AddSnippet);
            RemoveSnippetCommand = new RelayCommand(RemoveSnippet, o => SelectedSnippet != null);
            DuplicateSnippetCommand = new RelayCommand(DuplicateSnippet, o => SelectedSnippet != null);
            
            SaveCommand = new RelayCommand(SaveSnippet, o => SelectedSnippet != null && IsDirty);
            DiscardCommand = new RelayCommand(DiscardChanges, o => SelectedSnippet != null && IsDirty);
            
            TogglePauseCommand = new RelayCommand(TogglePause);
            ExitCommand = new RelayCommand(Exit);

            
            ToggleHelpCommand = new RelayCommand(o => IsHelpVisible = !IsHelpVisible);
            ToggleSettingsCommand = new RelayCommand(o => IsSettingsVisible = !IsSettingsVisible);

            // Walkthrough Init
            NextWalkthroughStepCommand = new RelayCommand(o => 
            {
                if (WalkthroughStep < 5) WalkthroughStep++;
                else SkipWalkthrough(null);
            });
            SkipWalkthroughCommand = new RelayCommand(SkipWalkthrough);
            RestartWalkthroughCommand = new RelayCommand(RestartWalkthrough);

            var settings = _storageService.LoadSettings();
            _currentSettings = settings;

            if(!settings.IsWalkthroughCompleted)
            {
                IsWalkthroughVisible = true;
                WalkthroughStep = 1;
            }

            _hotKeyService.PausedChanged += (s, paused) => 
            {
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(ServiceButtonText));
            };

            // Update System Init
            _updateService = new UpdateService();
            CheckForUpdatesCommand = new RelayCommand(async o => await CheckForUpdates());
            OpenUpdatePageCommand = new RelayCommand(OpenUpdatePage);
            DismissUpdateCommand = new RelayCommand(o => IsUpdateOverlayVisible = false);
            TestSnippetCommand = new RelayCommand(TestSnippet, o => EditableSnippet != null);
        }

        private async void TestSnippet(object obj)
        {
            if (EditableSnippet == null) return;

            // Countdown / Focus wait
            // We can't easily switch focus back to last app here without a "Test Window" or delay.
            // Let's just wait 3 seconds.
            
            // Should valid Safety here too?
            if (SafetyConfirmation && EditableSnippet.Text.Length > 500)
            {
                var result = System.Windows.MessageBox.Show($"You are about to type {EditableSnippet.Text.Length} characters. Continue?", "Long Text Warning", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                if (result == System.Windows.MessageBoxResult.No) return;
            }

            try 
            {
                await Task.Delay(3000); // 3 sec countdown
                await _inputService.TypeTextAsync(EditableSnippet.Text, EditableSnippet.Mode, EditableSnippet.DelayPerChar, EditableSnippet.DelayPerWord, CancellationToken.None);
            }
            catch (Exception ex)
            {
                 System.Windows.MessageBox.Show($"Test failed: {ex.Message}");
            }
        }
        
        private async Task CheckForUpdates()
        {
            try 
            {
                var update = await _updateService.CheckForUpdatesAsync();
                if (update != null)
                {
                    UpdateAvailable = update;
                    IsUpdateOverlayVisible = true;
                }
                else
                {
                    System.Windows.MessageBox.Show("You are using the latest version.", "Auto Typer", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch
            {
                System.Windows.MessageBox.Show("Failed to check for updates. Please check your internet connection.", "Update Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        private void OpenUpdatePage(object obj)
        {
            if (UpdateAvailable?.ReleasePage != null)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = UpdateAvailable.ReleasePage,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
            IsUpdateOverlayVisible = false;
        }

        public ObservableCollection<Snippet> Snippets { get; set; }

        private Snippet _selectedSnippet;
        public Snippet SelectedSnippet
        {
            get => _selectedSnippet;
            set 
            {
                if (_selectedSnippet != value)
                {
                    // Check for unsaved changes in previous snippet
                    if (_selectedSnippet != null && IsDirty)
                    {
                         var result = System.Windows.MessageBox.Show($"Snippet '{_selectedSnippet.Name}' has unsaved changes. Save them?", "Unsaved Changes", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                         if (result == System.Windows.MessageBoxResult.Yes)
                         {
                             SaveSnippet(null);
                         }
                    }

                    _selectedSnippet = value; 
                    OnPropertyChanged();
                    
                    // working copy logic
                    if (_selectedSnippet != null)
                    {
                        // Create a clone for editing
                        EditableSnippet = _selectedSnippet.Clone();
                        IsDirty = false;
                        ValidateCurrentHotkey();
                    }
                    else
                    {
                        EditableSnippet = null;
                        IsDirty = false;
                    }
                }
            }
        }

        private Snippet _editableSnippet;
        public Snippet EditableSnippet
        {
             get => _editableSnippet;
             set
             {
                 if (_editableSnippet != null)
                 {
                     _editableSnippet.PropertyChanged -= EditableSnippet_PropertyChanged;
                 }
                 _editableSnippet = value;
                 if (_editableSnippet != null)
                 {
                     _editableSnippet.PropertyChanged += EditableSnippet_PropertyChanged;
                 }
                 OnPropertyChanged();
             }
        }

        private void EditableSnippet_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
             if (!IsDirty)
             {
                 IsDirty = true;
             }
             
             if (e.PropertyName == nameof(Snippet.HotKeyKey) || e.PropertyName == nameof(Snippet.HotKeyModifiers))
             {
                 ValidateCurrentHotkey();
             }
        }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set { _isDirty = value; OnPropertyChanged(); }
        }

        public bool IsPaused => _hotKeyService.IsPaused;
        public string StatusText => IsPaused ? "PAUSED" : "ACTIVE";
        public string StatusColor => IsPaused ? "#FF4444" : "#44FF44";
        public string ServiceButtonText => IsPaused ? "Start Service" : "Stop Service";

        // Hotkey Validation Properties
        private string _hotkeyStatusMessage;
        public string HotkeyStatusMessage
        {
            get => _hotkeyStatusMessage;
            set { _hotkeyStatusMessage = value; OnPropertyChanged(); }
        }

        private bool _isHotkeyValid = true;
        public bool IsHotkeyValid
        {
            get => _isHotkeyValid;
            set { _isHotkeyValid = value; OnPropertyChanged(); }
        }
        
        // Visibility Properties
        private bool _isHelpVisible;
        public bool IsHelpVisible
        {
            get => _isHelpVisible;
            set { _isHelpVisible = value; OnPropertyChanged(); }
        }

        private bool _isSettingsVisible;
        public bool IsSettingsVisible
        {
             get => _isSettingsVisible;
             set { _isSettingsVisible = value; OnPropertyChanged(); }
        }

        // Walkthrough Properties
        private bool _isWalkthroughVisible;
        public bool IsWalkthroughVisible
        {
            get => _isWalkthroughVisible;
            set { _isWalkthroughVisible = value; OnPropertyChanged(); }
        }

        private int _walkthroughStep = 1;
        public int WalkthroughStep
        {
            get => _walkthroughStep;
            set { _walkthroughStep = value; OnPropertyChanged(); }
        }

        // Settings Properties
        private AppSettings _currentSettings;

        public bool StartMinimized
        {
            get => _currentSettings.StartMinimized;
            set
            {
                if (_currentSettings.StartMinimized != value)
                {
                    _currentSettings.StartMinimized = value;
                    OnPropertyChanged();
                    _storageService.SaveSettings(_currentSettings);
                }
            }
        }

        public bool StartWithWindows
        {
            get => _currentSettings.StartWithWindows;
            set
            {
                if (_currentSettings.StartWithWindows != value)
                {
                    _currentSettings.StartWithWindows = value;
                    OnPropertyChanged();
                    _storageService.SaveSettings(_currentSettings);
                    SetStartup(value);
                }
            }
        }

        public TypingMode DefaultTypingMode
        {
            get => _currentSettings.DefaultTypingMode;
            set
            {
                if (_currentSettings.DefaultTypingMode != value)
                {
                    _currentSettings.DefaultTypingMode = value;
                    OnPropertyChanged();
                    _storageService.SaveSettings(_currentSettings);
                }
            }
        }

        public int DefaultDelay
        {
            get => _currentSettings.DefaultDelay;
            set
            {
                if (_currentSettings.DefaultDelay != value)
                {
                    _currentSettings.DefaultDelay = value;
                    OnPropertyChanged();
                    _storageService.SaveSettings(_currentSettings);
                }
            }
        }

        public bool SafetyConfirmation
        {
            get => _currentSettings.SafetyConfirmation;
            set
            {
                if (_currentSettings.SafetyConfirmation != value)
                {
                    _currentSettings.SafetyConfirmation = value;
                    OnPropertyChanged();
                    _storageService.SaveSettings(_currentSettings);
                }
            }
        }

        private void SetStartup(bool enable)
        {
             try
             {
                 string runKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
                 using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, true))
                 {
                     if (enable)
                     {
                         string path = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                         key.SetValue("AutoTyper", path);
                     }
                     else
                     {
                         key.DeleteValue("AutoTyper", false);
                     }
                 }
             }
             catch (Exception ex)
             {
                 System.Diagnostics.Debug.WriteLine($"Startup registry error: {ex.Message}");
             }
        }

        public ICommand AddSnippetCommand { get; }
        public ICommand RemoveSnippetCommand { get; }
        public ICommand DuplicateSnippetCommand { get; }
        public ICommand SaveCommand { get; } // Save Snippet only
        public ICommand DiscardCommand { get; }
        public ICommand TogglePauseCommand { get; }
        public ICommand ExitCommand { get; }

        public ICommand ToggleHelpCommand { get; }
        public ICommand ToggleSettingsCommand { get; }
        public ICommand CheckForUpdatesCommand { get; }
        public ICommand OpenUpdatePageCommand { get; }
        public ICommand DismissUpdateCommand { get; }
        public ICommand TestSnippetCommand { get; }
        
        // Walkthrough Commands
        public ICommand NextWalkthroughStepCommand { get; }
        public ICommand SkipWalkthroughCommand { get; }
        public ICommand RestartWalkthroughCommand { get; }

        private readonly UpdateService _updateService;
        
        private UpdateInfo _updateAvailable;
        public UpdateInfo UpdateAvailable
        {
            get => _updateAvailable;
            set { _updateAvailable = value; OnPropertyChanged(); }
        }

        private bool _isUpdateOverlayVisible;
        public bool IsUpdateOverlayVisible
        {
            get => _isUpdateOverlayVisible;
            set { _isUpdateOverlayVisible = value; OnPropertyChanged(); }
        }

        public string CurrentVersion => _updateService.GetCurrentVersion();

        public void Initialize(IntPtr windowHandle)
        {
            _hotKeyService.Initialize(windowHandle);
            RegisterAllHotKeys();
            _ = CheckForUpdates();
        }

        private void RegisterAllHotKeys()
        {
            _hotKeyService.UnregisterAll();
            
            // We register the "Committed" snippets, not the editable one
            foreach (var snippet in Snippets)
            {
                if (snippet.HotKeyKey == Key.None || !snippet.IsEnabled) continue;

                bool success = _hotKeyService.Register(snippet.HotKeyModifiers, snippet.HotKeyKey, () =>
                {
                    ExecuteSnippet(snippet);
                });

                if (!success)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to register hotkey for {snippet.Name}");
                }
            }
        }

        private async void ExecuteSnippet(Snippet snippet)
        {
            if (IsPaused) return;

            if (_typingCts != null)
            {
                _typingCts.Cancel();
                _typingCts.Dispose();
                _typingCts = null;
            }

            _typingCts = new CancellationTokenSource();

            try
            {
                await _inputService.TypeTextAsync(snippet.Text, snippet.Mode, snippet.DelayPerChar, snippet.DelayPerWord, _typingCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Typing cancelled
            }
        }
        
        public void HandleHotkeyInput(Key key, ModifierKeys modifiers)
        {
            if (EditableSnippet == null) return;

            var result = _hotkeyValidationService.Validate(modifiers, key);
            
            if (result.IsValid)
            {
                // Conflict check against committed snippets, excluding current
                var conflict = Snippets.FirstOrDefault(s => s.Id != EditableSnippet.Id && s.IsEnabled && s.HotKeyKey == key && s.HotKeyModifiers == modifiers);
                
                if (conflict != null)
                {
                    IsHotkeyValid = false;
                    HotkeyStatusMessage = $"Conflict with '{conflict.Name}'";
                }
                else
                {
                    IsHotkeyValid = true;
                    HotkeyStatusMessage = "";
                    EditableSnippet.HotKeyKey = key;
                    EditableSnippet.HotKeyModifiers = modifiers;
                }
            }
            else
            {
                IsHotkeyValid = false;
                HotkeyStatusMessage = result.Message;
            }
        }

        private void ValidateCurrentHotkey()
        {
            if (EditableSnippet == null) 
            {
                IsHotkeyValid = true;
                HotkeyStatusMessage = "";
                return;
            }

            if (EditableSnippet.HotKeyKey == Key.None)
            {
                IsHotkeyValid = true; 
                HotkeyStatusMessage = "No hotkey assigned";
                return;
            }

            var result = _hotkeyValidationService.Validate(EditableSnippet.HotKeyModifiers, EditableSnippet.HotKeyKey);
            if (!result.IsValid)
            {
                 IsHotkeyValid = false;
                 HotkeyStatusMessage = result.Message;
            }
            else
            {
                var conflict = Snippets.FirstOrDefault(s => s.Id != EditableSnippet.Id && s.IsEnabled && s.HotKeyKey == EditableSnippet.HotKeyKey && s.HotKeyModifiers == EditableSnippet.HotKeyModifiers);
                if (conflict != null)
                {
                    IsHotkeyValid = false;
                    HotkeyStatusMessage = $"Conflict with '{conflict.Name}'";
                }
                else
                {
                    IsHotkeyValid = true;
                    HotkeyStatusMessage = "";
                }
            }
        }

        private void AddSnippet(object obj)
        {
            var newSnippet = new Snippet { Name = "New Snippet", Mode = TypingMode.HumanLike };
            
            // Auto-assign next safe hotkey
            for (int i = 7; i <= 12; i++)
            {
                var key = (Key)Enum.Parse(typeof(Key), $"F{i}");
                var conflict = Snippets.FirstOrDefault(s => s.HotKeyModifiers == (ModifierKeys.Control | ModifierKeys.Alt) && s.HotKeyKey == key);
                if (conflict == null)
                {
                    newSnippet.HotKeyModifiers = ModifierKeys.Control | ModifierKeys.Alt;
                    newSnippet.HotKeyKey = key;
                    break;
                }
            }

            Snippets.Add(newSnippet);
            SelectedSnippet = newSnippet;
        }

        private void DuplicateSnippet(object obj)
        {
            if (SelectedSnippet == null) return;
            var newSnippet = new Snippet
            {
                Name = $"{SelectedSnippet.Name} (Copy)",
                Text = SelectedSnippet.Text,
                Mode = SelectedSnippet.Mode,
                DelayPerChar = SelectedSnippet.DelayPerChar,
                DelayPerWord = SelectedSnippet.DelayPerWord,
                IsEnabled = SelectedSnippet.IsEnabled,
                HotKeyModifiers = ModifierKeys.None, 
                HotKeyKey = Key.None
            };
            Snippets.Add(newSnippet);
            SelectedSnippet = newSnippet;
        }

        private void RemoveSnippet(object obj)
        {
            if (SelectedSnippet != null)
            {
                Snippets.Remove(SelectedSnippet);
                SelectedSnippet = null;
                RegisterAllHotKeys();
            }
        }

        private void SaveSnippet(object obj)
        {
            if (SelectedSnippet != null && EditableSnippet != null)
            {
                SelectedSnippet.CopyFrom(EditableSnippet);
                _storageService.SaveSnippets(Snippets.ToList());
                RegisterAllHotKeys();
                IsDirty = false;
            }
        }
        
        private void DiscardChanges(object obj)
        {
            if (SelectedSnippet != null)
            {
                EditableSnippet = SelectedSnippet.Clone();
                IsDirty = false;
                ValidateCurrentHotkey();
            }
        }



        private void SkipWalkthrough(object obj)
        {
            IsWalkthroughVisible = false;
            var settings = _storageService.LoadSettings();
            settings.IsWalkthroughCompleted = true;
            _storageService.SaveSettings(settings);
        }

        private void RestartWalkthrough(object obj)
        {
            IsHelpVisible = false;
            IsWalkthroughVisible = true;
            WalkthroughStep = 1;
        }

        private void TogglePause(object obj)
        {
            _hotKeyService.IsPaused = !_hotKeyService.IsPaused;
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(ServiceButtonText));
        }

        private void Exit(object obj)
        {
            _hotKeyService.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
