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
            InitializeComponent();
            
            // Setup Tray Icon
            _notifyIcon = new FormNotifyIcon();
            try
            {
                // Create Icon from logo.png. System.Drawing.Icon doesn't support PNG directly in constructor.
                // But we can load Bitmap and convert.
                using (var bitmap = new System.Drawing.Bitmap("logo.png"))
                {
                    _notifyIcon.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
                }
            }
            catch
            {
                // Fallback if logo invalid
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
            var handle = new WindowInteropHelper(this).Handle;
            ViewModel?.Initialize(handle);

            // Start Minimized Check
            if (ViewModel != null && ViewModel.StartMinimized)
            {
                Hide();
                // Ensure Tray Icon is there (it is initialized in Constructor)
            }
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
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            // Trigger ViewModel Exit just in case cleanup needs to run there too, 
            // but we can just Close() and let closing handler proceed.
            // ViewModel.ExitCommand.Execute(null); 
            // Better to let ViewModel handle disposal, but we need to stop the cancel in Closing.
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExplicitExit)
            {
                e.Cancel = true;
                Hide();
                return; // Minimize to tray
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

            // Ignore modifier keys alone
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
                // Find the TextBox. Since we know the structure or can name it, let's use name.
                // But accessing x:Name="SnippetEditorBox" directly is better if available.
                // Assuming I name the TextBox "SnippetEditorBox" in XAML.
                if (SnippetEditorBox != null)
                {
                    int caretIndex = SnippetEditorBox.CaretIndex;
                    SnippetEditorBox.Text = SnippetEditorBox.Text.Insert(caretIndex, token);
                    SnippetEditorBox.CaretIndex = caretIndex + token.Length;
                    SnippetEditorBox.Focus();
                    // Force update binding since we modified Text directly? 
                    // Usually Text binding with UpdateSourceTrigger=PropertyChanged handles it if PropertyChanged fires.
                    // But direct property set on TextBox might not fire the source update if it's OneWayToSource or if not focused.
                    // Actually, modifying .Text programmatically DOES NOT automatically trigger the binding Source update in some cases.
                    // Let's manually get the binding expression and update.
                    var binding = SnippetEditorBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                    binding?.UpdateSource();
                }
            }
        }
    }
}