using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using NOTM.Core.Display;

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

    private void OnSetPrimaryClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DisplayInfo display })
        {
            ViewModel.SetTargetPrimary(display);
        }
    }

    private void OnToggleBlockClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DisplayInfo display })
        {
            ViewModel.ToggleExcluded(display);
        }
    }

    private void OnIdentifySingleClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DisplayInfo display })
        {
            ViewModel.IdentifySingleDisplay(display);
        }
    }

    private void OnClearLogClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearLog();
    }

    private void OnMapMonitorClicked(DisplayInfo display)
    {
        ViewModel.ToggleExcluded(display);
    }
}