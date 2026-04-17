using System;
using System.Windows;
using System.Windows.Input;
using KeyboardSwitch.ViewModels;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace KeyboardSwitch.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void OnHide(object sender, RoutedEventArgs e) => Hide();

    private void OnExit(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void OnTestSound(object sender, RoutedEventArgs e) => _vm.TestSound();

    private void OnBrowseSound(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите WAV-файл",
            Filter = "WAV (*.wav)|*.wav|Все файлы (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) == true)
            _vm.CustomSoundPath = dlg.FileName;
    }

    private void OnAddIgnored(object sender, RoutedEventArgs e) => _vm.AddIgnoredProcess();

    private void OnIgnoredKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { _vm.AddIgnoredProcess(); e.Handled = true; }
    }

    private void OnRemoveIgnored(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string name)
            _vm.RemoveIgnoredProcess(name);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Keep the app alive in the tray when the user closes the window.
        e.Cancel = true;
        Hide();
    }
}
