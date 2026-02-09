using System;
using System.Windows;
using AutoTyper.ViewModels;

namespace AutoTyper.Views
{
    public partial class AccessWindow : Window
    {
        public AccessWindow()
        {
            InitializeComponent();
            var viewModel = new AccessViewModel();
            viewModel.RequestClose += (s, e) => 
            {
                DialogResult = true; // Signal success
                Close();
            };
            DataContext = viewModel;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}
