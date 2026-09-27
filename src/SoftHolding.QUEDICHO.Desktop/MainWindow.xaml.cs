using System.Windows;
using SoftHolding.QUEDICHO.Desktop.ViewModels;

namespace SoftHolding.QUEDICHO.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
        Closed += (_, _) => viewModel.Dispose();
    }
}
