using Avalonia.Controls;
using PsarcImporter.Gui.ViewModels;

namespace PsarcImporter.Gui.Views;

internal partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(this);
    }
}
