using System.Windows;
using SoftHolding.QUEDICHO.Application.TextInjection;
using SoftHolding.QUEDICHO.Desktop.ViewModels;

namespace SoftHolding.QUEDICHO.Desktop;

public partial class MainWindow : Window
{
    private readonly IGlobalHotkeyService _globalHotkeyService;
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(
        MainWindowViewModel viewModel,
        IGlobalHotkeyService globalHotkeyService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _globalHotkeyService = globalHotkeyService;
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs eventArgs)
    {
        if (!_globalHotkeyService.Register())
        {
            MessageBox.Show(
                "El atajo global no pudo registrarse porque otra aplicación podría estar utilizándolo. " +
                "Puedes seguir usando el botón Escribir en cursor.",
                "Atajo no disponible",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        _globalHotkeyService.Unregister();
        _viewModel.Dispose();
    }
}
