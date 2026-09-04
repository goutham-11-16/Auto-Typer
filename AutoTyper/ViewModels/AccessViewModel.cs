using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AutoTyper.Models;
using AutoTyper.Services;

namespace AutoTyper.ViewModels
{
    public class AccessViewModel : INotifyPropertyChanged
    {
        private readonly AccessControlService _accessService;
        private readonly DeviceIdService _deviceIdService;
        
        public event EventHandler RequestClose; // Event to close the window when authorized

        public AccessViewModel()
        {
            _accessService = new AccessControlService();
            _deviceIdService = new DeviceIdService();
            
            DeviceId = _deviceIdService.GetDeviceId();
            
            checkAccessCommand = new RelayCommand(async _ => await CheckAccessAsync());
            requestAccessCommand = new RelayCommand(async _ => await SubmitRequestAsync(), _ => !IsLoading && !string.IsNullOrWhiteSpace(Username));
            
            // Start check automatically
            _ = CheckAccessAsync();
        }

        private string _deviceId = string.Empty;
        public string DeviceId
        {
            get => _deviceId;
            set { _deviceId = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "Initializing...";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private string _statusColor = "#CCCCCC"; // Default Gray
        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set 
            { 
                _isLoading = value; 
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // UI State Flags
        private bool _isUpdateRequired;
        public bool IsUpdateRequired
        {
            get => _isUpdateRequired;
            set { _isUpdateRequired = value; OnPropertyChanged(); }
        }

        private bool _isAccessDenied;
        public bool IsAccessDenied
        {
            get => _isAccessDenied;
            set { _isAccessDenied = value; OnPropertyChanged(); }
        }

        private bool _isRequestFormVisible;
        public bool IsRequestFormVisible
        {
            get => _isRequestFormVisible;
            set { _isRequestFormVisible = value; OnPropertyChanged(); }
        }

        private bool _isPending;
        public bool IsPending
        {
            get => _isPending;
            set { _isPending = value; OnPropertyChanged(); }
        }

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set 
            { 
                _username = value; 
                OnPropertyChanged(); 
                CommandManager.InvalidateRequerySuggested();
            }
        }
        
        private string _updateMessage = string.Empty;
        public string UpdateMessage
        {
             get => _updateMessage;
             set { _updateMessage = value; OnPropertyChanged(); }
        }
        
        private string _updateUrl = string.Empty;
        public string UpdateUrl
        {
             get => _updateUrl;
             set { _updateUrl = value; OnPropertyChanged(); }
        }

        private ICommand checkAccessCommand;
        public ICommand CheckAccessCommand => checkAccessCommand;

        private ICommand requestAccessCommand;
        public ICommand RequestAccessCommand => requestAccessCommand;
        
        public ICommand OpenUpdateUrlCommand => new RelayCommand(_ => 
        {
             if (!string.IsNullOrEmpty(UpdateUrl))
             {
                 try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = UpdateUrl, UseShellExecute = true }); } catch {}
             }
        });

        private static void LogAccess(string msg)
        {
            try
            {
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_log.txt");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
            }
            catch { }
        }

        public async Task CheckAccessAsync()
        {
            IsLoading = true;
            StatusMessage = "Checking Connection...";
            ResetState();

            try
            {
                LogAccess("CheckAccess: Step 0 (Internet check)");
                // STEP 0: Check Internet
                bool isOnline = await _accessService.CheckInternetConnection();
                if (!isOnline)
                {
                    LogAccess("CheckAccess: No Internet Connection");
                    StatusMessage = "No Internet Connection";
                    StatusColor = "#FF4444";
                    System.Windows.MessageBox.Show("No Internet Connection. Please connect to the internet and reopen the application.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    System.Windows.Application.Current.Shutdown();
                    return;
                }

                // STEP 1: Global Kill Switch
                LogAccess("CheckAccess: Step 1 (Global Kill Switch)");
                StatusMessage = "Verifying System State...";
                var globalState = await _accessService.GetGlobalStateAsync();
                
                if (globalState != null && !globalState.AppEnabled)
                {
                    LogAccess("CheckAccess: System Disabled by admin");
                    StatusMessage = "System Disabled";
                    System.Windows.MessageBox.Show(globalState.KillMessage, "Access Denied", MessageBoxButton.OK, MessageBoxImage.Stop);
                    System.Windows.Application.Current.Shutdown();
                    return;
                }

                // STEP 2: Mandatory Update
                LogAccess("CheckAccess: Step 2 (Version check)");
                StatusMessage = "Checking Version...";
                var updateInfo = await _accessService.GetUpdateConfigAsync();
                if (updateInfo != null && IsUpdateMandatory(updateInfo))
                {
                    LogAccess($"CheckAccess: Mandatory update to {updateInfo.LatestVersion}");
                    IsUpdateRequired = true;
                    StatusMessage = "Update Required";
                    StatusColor = "#FF4444";
                    UpdateMessage = $"New version {updateInfo.LatestVersion} is available. You must update to continue.";
                    UpdateUrl = updateInfo.DownloadUrl;
                    IsLoading = false;
                    return;
                }

                // STEP 3: Device Authorization
                LogAccess("CheckAccess: Step 3 (Identity verification)");
                StatusMessage = "Verifying Identity...";
                var usersConfig = await _accessService.GetUsersConfigAsync();
                
                LogAccess($"CheckAccess: Found {usersConfig.Users.Count} users in config. Local DeviceId={DeviceId}");
                var userEntry = usersConfig.Users.FirstOrDefault(u => u.DeviceId == DeviceId);

                if (userEntry != null)
                {
                    if (userEntry.Authenticated)
                    {
                        // STEP 4: ACCESS GRANTED
                        LogAccess($"CheckAccess: ACCESS GRANTED for user {userEntry.Code}");
                        StatusMessage = "Access Granted";
                        StatusColor = "#44FF44";
                        await Task.Delay(300); 
                        RequestClose?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        LogAccess("CheckAccess: ACCESS REVOKED");
                        IsAccessDenied = true;
                        StatusMessage = "Access Revoked";
                        StatusColor = "#FF4444"; 
                    }
                }
                else
                {
                    LogAccess("CheckAccess: Device Not Registered");
                    IsRequestFormVisible = true;
                    StatusMessage = "Device Not Registered";
                    StatusColor = "#4444FF"; 
                }
            }
            catch (Exception ex)
            {
                LogAccess($"CheckAccess: Exception: {ex.Message}");
                StatusMessage = "Fatal Error";
                System.Windows.Application.Current.Shutdown();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SubmitRequestAsync()
        {
            if (string.IsNullOrWhiteSpace(Username)) return;

            IsLoading = true;
            StatusMessage = "Submitting Request...";

            try
            {
                bool success = await _accessService.SubmitAccessRequestAsync(DeviceId, Username);
                if (success)
                {
                    StatusMessage = "Request Submitted";
                    IsRequestFormVisible = false;
                    IsPending = true; // Show Pending UI
                }
                else
                {
                    StatusMessage = "Submission Failed (API Error)";
                }
            }
            catch
            {
                StatusMessage = "Submission Failed";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private void ResetState()
        {
            IsUpdateRequired = false;
            IsAccessDenied = false;
            IsRequestFormVisible = false;
            IsPending = false;
        }

        private bool IsUpdateMandatory(AppVersionInfo info)
        {
             if (!info.Mandatory) return false;
             
             // Simple version comparison logic
             if (Version.TryParse(info.LatestVersion, out var remoteVer))
             {
                 var localVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                 return remoteVer > localVer;
             }
             return false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
