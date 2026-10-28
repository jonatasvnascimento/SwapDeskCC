using Avalonia.Controls;
using Avalonia.Threading;
using SwapDeskCC.App.ViewModels;

namespace SwapDeskCC.App.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage() => InitializeComponent();
}

public partial class AboutPage : UserControl
{
    public AboutPage() => InitializeComponent();
}

public partial class SwapDialog : UserControl
{
    public SwapDialog() => InitializeComponent();
}

public partial class ConfirmDialog : UserControl
{
    public ConfirmDialog() => InitializeComponent();
}

public partial class AccountEditorDialog : UserControl
{
    public AccountEditorDialog()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Foco no apelido sempre que o assistente abre.
        if (DataContext is AccountEditorViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(AccountEditorViewModel.IsOpen) && vm.IsOpen)
                    Dispatcher.UIThread.Post(() => NameBox.Focus(), DispatcherPriority.Input);
            };
        }
    }
}
