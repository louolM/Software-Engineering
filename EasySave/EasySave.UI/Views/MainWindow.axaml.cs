using Avalonia.Controls;
using EasySave.UI.ViewModels;

namespace EasySave.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.SetWindow(this);
        };
    }
}