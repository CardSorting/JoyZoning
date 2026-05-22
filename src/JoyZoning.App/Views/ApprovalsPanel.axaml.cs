using Avalonia.Controls;

namespace JoyZoning.App.Views;

public partial class ApprovalsPanel : UserControl
{
    public ApprovalsPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is ViewModels.ApprovalsViewModel vm)
                vm.RefreshApprovalsCommand.Execute(null);
        };
    }
}
