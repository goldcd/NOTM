using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using NOTM.Core.Native;

namespace NOTM;

public partial class MainWindow : Window
{
    private bool _isExplicitExit = false;

    public UI.MainWindowViewModel ViewModel { get; }

    public MainWindow(UI.MainWindowViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Hook window message loop for WM_DISPLAYCHANGE to auto-detect monitor plug/unplug or sleep/wake
        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_DISPLAYCHANGE)
        {
            // Windows display resolution or monitor setup changed!
            Dispatcher.InvokeAsync(() =>
            {
                ViewModel.LoadDisplays();
            });
        }
        return IntPtr.Zero;
    }

    public void RequestExplicitExit()
    {
        _isExplicitExit = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit)
        {
            // Minimize to system tray instead of exiting process
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }

    private void OnIdentifyAllClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.IdentifyAllDisplays();
    }

    private void OnSweepClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.SweepNow();
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.LoadDisplays();
    }

    private void OnSetSelectedAsPrimaryClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.SetSelectedAsPrimary();
    }

    private void OnToggleSelectedBlockClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleSelectedExcluded();
    }

    private void OnIdentifySelectedClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.IdentifySelected();
    }

    private void OnClearLogClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearLog();
    }
}