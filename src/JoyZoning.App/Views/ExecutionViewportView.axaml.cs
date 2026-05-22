using Avalonia.Controls;
using Avalonia.VisualTree;
using JoyZoning.App.ViewModels;
using SvcSystems.UI.Terminal;

namespace JoyZoning.App.Views;

public partial class ExecutionViewportView : UserControl
{
    public ExecutionViewportView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is ExecutionViewportViewModel vm && vm.SelectedTabIndex == 1)
                FocusTerminal();
        };
    }

    private void FocusTerminal()
    {
        var term = this.GetVisualDescendants().OfType<TerminalControl>().FirstOrDefault();
        term?.Focus();
    }
}
