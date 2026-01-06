using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using WPFKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WPFKey = System.Windows.Input.Key;
using WPFKeyboard = System.Windows.Input.Keyboard;
using System.Windows.Input;
using AutoTyper.ViewModels;
using FormNotifyIcon = System.Windows.Forms.NotifyIcon;

namespace AutoTyper
{
    public partial class MainWindow : Window
    {
        private FormNotifyIcon _notifyIcon;
        private bool _isExplicitExit = false;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                Loaded += MainWindow_Loaded;
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("startup_error.txt", ex.ToString());
                System.Windows.MessageBox.Show($"Window Initialization Error: {ex.Message}\n\n{ex.StackTrace}", "Auto Typer Init Fail", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                throw;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try { System.IO.File.AppendAllText("startup_log.txt", "MainWindow Loaded Event\n"); } catch { }
            // Initialize ViewModel
            var handle = new WindowInteropHelper(this).Handle;
            if (ViewModel != null)
            {
                ViewModel.Initialize(handle);
            }

            // Setup Tray Icon (Moved from Constructor)
            SetupTrayIcon();

            // Explicitly show window (Override any previous hidden state)
            Show();
            Activate();
            try { System.IO.File.AppendAllText("startup_log.txt", "MainWindow Activated\n"); } catch { }
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new FormNotifyIcon();
            try
            {
                var resourceInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/logo.png"));
                if (resourceInfo != null)
                {
                    using (var stream = resourceInfo.Stream)
                    using (var bitmap = new System.Drawing.Bitmap(stream))
                    {
                        _notifyIcon.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
                    }
                }
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            } 
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Auto Typer - byGo";
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();
            
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("Show", null, (s, e) => ShowWindow());
            contextMenu.Items.Add("Exit", null, (s, e) => ExitApp());
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private MainViewModel ViewModel => DataContext as MainViewModel;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Moved Initialization to Loaded event
            
            // DISABLED MINIMIZE LOGIC FOR VERIFICATION
            /*
            if (ViewModel != null && ViewModel.StartMinimized)
            {
                Hide();
            }
            */
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApp()
        {
            _isExplicitExit = true;
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExplicitExit)
            {
                // DISABLED TRAY CLOSE FOR VERIFICATION - Application will close normally
                // e.Cancel = true;
                // Hide();
                // return; 
            }

            // Check for unsaved changes
            if (ViewModel != null && ViewModel.IsDirty)
            {
                var result = System.Windows.MessageBox.Show("You have unsaved changes. Do you want to save them before exiting?", "Unsaved Changes", System.Windows.MessageBoxButton.YesNoCancel, System.Windows.MessageBoxImage.Warning);
                if (result == System.Windows.MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    ViewModel.SaveCommand.Execute(null);
                }
            }

            base.OnClosing(e);
        }

        private void HotkeyBox_PreviewKeyDown(object sender, WPFKeyEventArgs e)
        {
            e.Handled = true;

            var key = (e.Key == WPFKey.System ? e.SystemKey : e.Key);

            if (key == WPFKey.LeftCtrl || key == WPFKey.RightCtrl || 
                key == WPFKey.LeftAlt || key == WPFKey.RightAlt || 
                key == WPFKey.LeftShift || key == WPFKey.RightShift ||
                key == WPFKey.LWin || key == WPFKey.RWin)
            {
                return;
            }

            if (ViewModel != null)
            {
                ViewModel.HandleHotkeyInput(key, WPFKeyboard.Modifiers);
            }
        }

        private void InsertToken_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string token)
            {
                if (SnippetEditorBox != null)
                {
                    int caretIndex = SnippetEditorBox.CaretIndex;
                    SnippetEditorBox.Text = SnippetEditorBox.Text.Insert(caretIndex, token);
                    SnippetEditorBox.CaretIndex = caretIndex + token.Length;
                    SnippetEditorBox.Focus();
                    var binding = SnippetEditorBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                    binding?.UpdateSource();
                }
            }
        }
    }
}