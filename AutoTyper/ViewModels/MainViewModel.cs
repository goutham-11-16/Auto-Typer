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
        private readonly IpcServerService _ipcServerService;
        private CancellationTokenSource? _typingCts;
        private string _currentExecutionState = "Ready";
        private int _currentProgress = 0;
        private int _currentTotal = 0;
        private string? _activeSnippetName = null;

        /// <summary>
        /// Safely runs a Task without observing it inline. Catches and logs any exceptions
        /// to prevent unobserved Task exceptions from crashing the process.
        /// </summary>
        private static async void SafeFireAndForget(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SafeFireAndForget] Error: {ex.Message}");
            }
        }

        public MainViewModel()
        {
            _hotKeyService = new HotKeyService();
            _storageService = new StorageService();
            _inputService = new InputService();
            _hotkeyValidationService = new HotkeyValidationService();
            _ipcServerService = new IpcServerService();
            _ipcServerService.OnCommandReceived = HandleIpcCommandAsync;

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

            // PausedChanged is legacy now, logic moved to TogglePause for manual control reliability


            // Update System Init
            _updateService = new UpdateService();
            CheckForUpdatesCommand = new RelayCommand(async o => await CheckForUpdates(true));
            OpenUpdatePageCommand = new RelayCommand(OpenUpdatePage);
            DismissUpdateCommand = new RelayCommand(o => IsUpdateOverlayVisible = false);
            // TestSnippetCommand removed
            OpenUrlCommand = new RelayCommand(OpenUrl);
        }

        // TestSnippet method removed as per cleanup

        
        private async Task CheckForUpdates(bool isManual = false)
        {
            try 
            {
                var update = await _updateService.CheckForUpdatesAsync();
                if (update != null)
                {
                    UpdateAvailable = update;
                    IsUpdateOverlayVisible = true;
                }
                else if (isManual)
                {
                    System.Windows.MessageBox.Show("You are using the latest version.", "Auto Typer", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch
            {
                if (isManual)
                {
                    System.Windows.MessageBox.Show("Failed to check for updates. Please check your internet connection.", "Update Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
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

        private void OpenUrl(object obj)
        {
            if (obj is string url)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
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

        public ICommand OpenUrlCommand { get; }
        
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
            try
            {
                var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Auto Typer byGo", "startup_log.txt");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] [MainViewModel] Initialize called. EnableGameBarIpc={_currentSettings.EnableGameBarIpc}\n");
            }
            catch { }

            _hotKeyService.Initialize(windowHandle);
            RegisterAllHotKeys();
            if (_currentSettings.EnableGameBarIpc)
            {
                _ipcServerService.Start();
            }
            SafeFireAndForget(CheckForUpdates());
        }

        private void RegisterAllHotKeys()
        {
            _hotKeyService.UnregisterAll();

            // Register emergency stop global hotkey
            if (_currentSettings.EmergencyStopHotkeyKey != Key.None)
            {
                _hotKeyService.Register(_currentSettings.EmergencyStopHotkeyModifiers, _currentSettings.EmergencyStopHotkeyKey, () =>
                {
                    StopTyping("Emergency stop triggered via global hotkey");
                });
            }
            
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

        private void ExecuteSnippet(Snippet snippet)
        {
            if (IsPaused) return;

            // Always fetch the freshest instance from Snippets collection by ID
            var current = Snippets.FirstOrDefault(s => string.Equals(s.Id, snippet.Id, StringComparison.OrdinalIgnoreCase)) ?? snippet;
            string textToType = current.Text;

            SafeFireAndForget(ExecuteTypingOperationAsync(textToType, current.Mode, current.DelayPerChar, current.DelayPerWord, 0, current.Id));
        }

        public async Task ExecuteTypingOperationAsync(string text, TypingMode mode, int delayPerChar, int delayPerWord, int countdown, string? snippetId)
        {
            if (IsPaused) return;

            if (_typingCts != null)
            {
                _typingCts.Cancel();
                _typingCts.Dispose();
                _typingCts = null;
            }

            _typingCts = new CancellationTokenSource();
            var token = _typingCts.Token;

            _activeSnippetName = snippetId != null ? Snippets.FirstOrDefault(s => s.Id == snippetId)?.Name : "Custom Text";
            _currentTotal = text.Length;
            _currentProgress = 0;

            try
            {
                if (countdown > 0)
                {
                    _currentExecutionState = "Countdown";
                    for (int sec = countdown; sec > 0; sec--)
                    {
                        token.ThrowIfCancellationRequested();
                        await _ipcServerService.BroadcastAsync(new IpcResponse
                        {
                            Type = "STATE_CHANGED",
                            State = "Countdown",
                            Countdown = sec,
                            Total = _currentTotal,
                            Progress = 0,
                            ActiveSnippet = _activeSnippetName
                        });
                        await Task.Delay(1000, token);
                    }
                }

                token.ThrowIfCancellationRequested();
                _currentExecutionState = "Typing";
                await _ipcServerService.BroadcastAsync(new IpcResponse
                {
                    Type = "STATE_CHANGED",
                    State = "Typing",
                    Total = _currentTotal,
                    Progress = 0,
                    ActiveSnippet = _activeSnippetName
                });

                var progress = new Progress<int>(p =>
                {
                    _currentProgress = p;
                    SafeFireAndForget(_ipcServerService.BroadcastAsync(new IpcResponse
                    {
                        Type = "STATE_CHANGED",
                        State = "Typing",
                        Progress = p,
                        Total = _currentTotal,
                        ActiveSnippet = _activeSnippetName
                    }));
                });

                await _inputService.TypeTextAsync(text, mode, delayPerChar, delayPerWord, token, 0, progress);

                _currentExecutionState = "Ready";
                _currentProgress = _currentTotal;
                await _ipcServerService.BroadcastAsync(new IpcResponse
                {
                    Type = "STATE_CHANGED",
                    State = "Ready",
                    Progress = _currentTotal,
                    Total = _currentTotal,
                    Message = "Typing completed successfully"
                });
            }
            catch (OperationCanceledException)
            {
                _currentExecutionState = "Stopped";
                await _ipcServerService.BroadcastAsync(new IpcResponse
                {
                    Type = "STATE_CHANGED",
                    State = "Stopped",
                    Progress = _currentProgress,
                    Total = _currentTotal,
                    Message = "Typing was stopped"
                });
            }
            catch (Exception ex)
            {
                _currentExecutionState = "Error";
                await _ipcServerService.BroadcastAsync(new IpcResponse
                {
                    Type = "ERROR",
                    State = "Error",
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        public void StopTyping(string? reason = null)
        {
            if (_typingCts != null)
            {
                try { _typingCts.Cancel(); } catch { }
            }
            _currentExecutionState = "Stopped";
            SafeFireAndForget(_ipcServerService.BroadcastAsync(new IpcResponse
            {
                Type = "STATE_CHANGED",
                State = "Stopped",
                Message = reason ?? "Stopped"
            }));
        }

        private async Task<IpcResponse> HandleIpcCommandAsync(IpcCommand command)
        {
            switch (command.Command?.ToUpperInvariant())
            {
                case "STATUS":
                    return CreateCurrentStatusResponse();

                case "GET_SNIPPETS":
                    return new IpcResponse
                    {
                        Type = "STATUS",
                        Success = true,
                        State = _currentExecutionState,
                        ActiveSnippet = SelectedSnippet?.Id ?? "",
                        Snippets = GetSnippetDtos()
                    };

                case "GET_SETTINGS":
                    return new IpcResponse
                    {
                        Type = "STATUS",
                        Success = true,
                        Mode = DefaultTypingMode.ToString(),
                        DelayPerChar = DefaultDelay,
                        DelayPerWord = DefaultDelay * 4
                    };

                case "SET_SPEED":
                    if (command.DelayPerChar.HasValue)
                    {
                        DefaultDelay = command.DelayPerChar.Value;
                    }
                    return CreateCurrentStatusResponse();

                case "LOAD_TEXT":
                    if (!string.IsNullOrEmpty(command.Text))
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (EditableSnippet != null)
                            {
                                EditableSnippet.Text = command.Text;
                            }
                            else
                            {
                                var snip = new Snippet { Name = "Game Bar Text", Text = command.Text, Mode = DefaultTypingMode };
                                Snippets.Add(snip);
                                SelectedSnippet = snip;
                            }
                        });
                    }
                    return CreateCurrentStatusResponse();

                case "SAVE_SNIPPET":
                    Snippet? savedSnippet = null;
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Snippet? target = null;
                        if (!string.IsNullOrEmpty(command.SnippetId))
                        {
                            target = Snippets.FirstOrDefault(s => string.Equals(s.Id, command.SnippetId, StringComparison.OrdinalIgnoreCase));
                        }
                        if (target == null && !string.IsNullOrEmpty(command.Hotkey))
                        {
                            target = Snippets.FirstOrDefault(s => s.HotKeyDisplay == command.Hotkey);
                        }
                        if (target == null)
                        {
                            target = new Snippet();
                            Snippets.Add(target);
                        }

                        if (!string.IsNullOrEmpty(command.Name)) target.Name = command.Name;
                        if (command.Text != null) target.Text = command.Text;
                        if (!string.IsNullOrEmpty(command.Mode) && Enum.TryParse<TypingMode>(command.Mode, true, out var m)) target.Mode = m;
                        if (command.DelayPerChar.HasValue) target.DelayPerChar = command.DelayPerChar.Value;
                        if (command.DelayPerWord.HasValue) target.DelayPerWord = command.DelayPerWord.Value;

                        if (command.Hotkey != null)
                        {
                            ParseAndAssignHotkey(target, command.Hotkey);
                        }

                        savedSnippet = target;

                        // Synchronize desktop UI working copy
                        if (SelectedSnippet == null || string.Equals(SelectedSnippet.Id, target.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            if (_editableSnippet != null)
                            {
                                _editableSnippet.PropertyChanged -= EditableSnippet_PropertyChanged;
                            }
                            _selectedSnippet = target;
                            OnPropertyChanged(nameof(SelectedSnippet));
                            _editableSnippet = target.Clone();
                            _editableSnippet.PropertyChanged += EditableSnippet_PropertyChanged;
                            OnPropertyChanged(nameof(EditableSnippet));
                            IsDirty = false;
                            ValidateCurrentHotkey();
                        }

                        _storageService.SaveSnippets(Snippets.ToList());
                        RegisterAllHotKeys();
                    });

                    var saveResp = new IpcResponse
                    {
                        Type = "SAVE_RESULT",
                        Success = true,
                        Message = "Snippet saved",
                        ActiveSnippet = savedSnippet?.Id,
                        Snippets = GetSnippetDtos()
                    };
                    return saveResp;

                case "ADD_SNIPPET":
                    Snippet? newSnippet = null;
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        newSnippet = new Snippet
                        {
                            Name = !string.IsNullOrEmpty(command.Name) ? command.Name : "New Snippet",
                            Text = command.Text ?? "",
                            Mode = !string.IsNullOrEmpty(command.Mode) && Enum.TryParse<TypingMode>(command.Mode, true, out var m) ? m : DefaultTypingMode,
                            DelayPerChar = command.DelayPerChar ?? 1,
                            DelayPerWord = command.DelayPerWord ?? 1
                        };
                        if (!string.IsNullOrEmpty(command.Hotkey))
                        {
                            ParseAndAssignHotkey(newSnippet, command.Hotkey);
                        }
                        Snippets.Add(newSnippet);
                        _selectedSnippet = newSnippet;
                        OnPropertyChanged(nameof(SelectedSnippet));
                        EditableSnippet = newSnippet.Clone();
                        IsDirty = false;
                        ValidateCurrentHotkey();
                        _storageService.SaveSnippets(Snippets.ToList());
                        RegisterAllHotKeys();
                    });

                    var addResp = new IpcResponse
                    {
                        Type = "STATUS",
                        Success = true,
                        Message = "Snippet added",
                        ActiveSnippet = newSnippet?.Id,
                        Snippets = GetSnippetDtos()
                    };
                    return addResp;

                case "DELETE_SNIPPET":
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!string.IsNullOrEmpty(command.SnippetId))
                        {
                            var target = Snippets.FirstOrDefault(s => string.Equals(s.Id, command.SnippetId, StringComparison.OrdinalIgnoreCase));
                            if (target != null)
                            {
                                Snippets.Remove(target);
                                _storageService.SaveSnippets(Snippets.ToList());
                                _selectedSnippet = Snippets.FirstOrDefault();
                                OnPropertyChanged(nameof(SelectedSnippet));
                                EditableSnippet = _selectedSnippet?.Clone();
                                IsDirty = false;
                                ValidateCurrentHotkey();
                                RegisterAllHotKeys();
                            }
                        }
                    });

                    var delResp = new IpcResponse
                    {
                        Type = "STATUS",
                        Success = true,
                        Message = "Snippet deleted",
                        ActiveSnippet = SelectedSnippet?.Id,
                        Snippets = GetSnippetDtos()
                    };
                    return delResp;

                case "START":
                    Snippet? found = null;
                    if (!string.IsNullOrEmpty(command.SnippetId))
                    {
                        found = Snippets.FirstOrDefault(s => s.Id == command.SnippetId);
                    }

                    // Prefer command.Text if non-empty, otherwise use snippet's text
                    string textToType = !string.IsNullOrEmpty(command.Text)
                        ? command.Text
                        : (found?.Text ?? string.Empty);

                    TypingMode mode = found?.Mode ?? DefaultTypingMode;
                    if (!string.IsNullOrEmpty(command.Mode) && Enum.TryParse<TypingMode>(command.Mode, true, out var parsedMode))
                    {
                        mode = parsedMode;
                    }

                    int charDelay = command.DelayPerChar ?? (found?.DelayPerChar ?? DefaultDelay);
                    int wordDelay = command.DelayPerWord ?? (found?.DelayPerWord ?? (charDelay * 4));
                    int countdown = command.CountdownSeconds ?? 0;

                    if (string.IsNullOrEmpty(textToType))
                    {
                        return new IpcResponse
                        {
                            Type = "ERROR",
                            Success = false,
                            Message = "No text provided to type"
                        };
                    }

                    SafeFireAndForget(Task.Run(() => ExecuteTypingOperationAsync(textToType, mode, charDelay, wordDelay, countdown, command.SnippetId)));

                    return new IpcResponse
                    {
                        Type = "STATE_CHANGED",
                        Success = true,
                        State = countdown > 0 ? "Countdown" : "Typing",
                        Total = textToType.Length,
                        Progress = 0,
                        Countdown = countdown
                    };

                case "PAUSE":
                    TogglePause(null);
                    _currentExecutionState = IsPaused ? "Paused" : "Ready";
                    SafeFireAndForget(_ipcServerService.BroadcastAsync(CreateCurrentStatusResponse()));
                    return CreateCurrentStatusResponse();

                case "RESUME":
                    if (IsPaused)
                    {
                        TogglePause(null);
                    }
                    _currentExecutionState = IsPaused ? "Paused" : "Ready";
                    SafeFireAndForget(_ipcServerService.BroadcastAsync(CreateCurrentStatusResponse()));
                    return CreateCurrentStatusResponse();

                case "STOP":
                    StopTyping("Stopped via Game Bar");
                    return CreateCurrentStatusResponse();

                default:
                    return new IpcResponse
                    {
                        Type = "ERROR",
                        Success = false,
                        Message = $"Unknown command: {command.Command}"
                    };
            }
        }

        private IpcResponse CreateCurrentStatusResponse()
        {
            return new IpcResponse
            {
                Type = "STATUS",
                Success = true,
                State = _currentExecutionState,
                IsPaused = IsPaused,
                ActiveSnippet = SelectedSnippet?.Id ?? "",
                Mode = DefaultTypingMode.ToString(),
                DelayPerChar = DefaultDelay,
                DelayPerWord = DefaultDelay * 4,
                Progress = _currentProgress,
                Total = _currentTotal
            };
        }

        private List<IpcSnippetDto> GetSnippetDtos()
        {
            var list = new List<IpcSnippetDto>();
            foreach (var s in Snippets)
            {
                string hotkeyStr = s.HotKeyKey != Key.None ? $"{s.HotKeyModifiers} + {s.HotKeyKey}" : "";
                list.Add(new IpcSnippetDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Text = s.Text,
                    Mode = s.Mode.ToString(),
                    DelayPerChar = s.DelayPerChar,
                    DelayPerWord = s.DelayPerWord,
                    Hotkey = hotkeyStr
                });
            }
            return list;
        }

        private static void ParseAndAssignHotkey(Snippet snippet, string hotkeyStr)
        {
            if (string.IsNullOrWhiteSpace(hotkeyStr) || hotkeyStr.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                snippet.HotKeyKey = Key.None;
                snippet.HotKeyModifiers = ModifierKeys.None;
                return;
            }

            ModifierKeys modifiers = ModifierKeys.None;
            Key key = Key.None;

            var parts = hotkeyStr.Split(new[] { '+', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var p in parts)
            {
                if (p.Equals("Control", StringComparison.OrdinalIgnoreCase) || p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Control;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Shift;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Alt;
                else if (p.Equals("Windows", StringComparison.OrdinalIgnoreCase) || p.Equals("Win", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Windows;
                else if (p.Equals("Enter", StringComparison.OrdinalIgnoreCase) || p.Equals("Return", StringComparison.OrdinalIgnoreCase))
                    key = Key.Return;
                else if (p.Equals("Esc", StringComparison.OrdinalIgnoreCase) || p.Equals("Escape", StringComparison.OrdinalIgnoreCase))
                    key = Key.Escape;
                else if (p.Length == 1 && char.IsDigit(p[0]))
                    key = (Key)((int)Key.D0 + (p[0] - '0'));
                else
                {
                    if (Enum.TryParse<Key>(p, true, out var parsedKey))
                    {
                        key = parsedKey;
                    }
                }
            }

            snippet.HotKeyModifiers = modifiers;
            snippet.HotKeyKey = key;
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
            BroadcastSnippetsUpdated();
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
            BroadcastSnippetsUpdated();
        }

        private void RemoveSnippet(object obj)
        {
            if (SelectedSnippet != null)
            {
                Snippets.Remove(SelectedSnippet);
                SelectedSnippet = null;
                RegisterAllHotKeys();
                BroadcastSnippetsUpdated();
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
                BroadcastSnippetsUpdated();
            }
        }

        private void BroadcastSnippetsUpdated()
        {
            if (_ipcServerService != null)
            {
                SafeFireAndForget(_ipcServerService.BroadcastAsync(new IpcResponse
                {
                    Type = "STATUS",
                    Success = true,
                    Snippets = GetSnippetDtos()
                }));
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
            if (!_hotKeyService.IsPaused)
            {
                // We are switching TO Paused state (Stop)
                // Stop Service (Visuals)
                _hotKeyService.IsPaused = true;
                
                // Cancel Execution Immediately
                if (_typingCts != null)
                {
                    try { _typingCts.Cancel(); } catch {}
                }
            }
            else
            {
                // We are switching TO Active state (Start)
                _hotKeyService.IsPaused = false;
            }
            
            UpdateStatusProperties();
        }

        private void UpdateStatusProperties()
        {
            OnPropertyChanged(nameof(IsPaused));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(ServiceButtonText));
        }

        private void Exit(object obj)
        {
            _ipcServerService.Dispose();
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
