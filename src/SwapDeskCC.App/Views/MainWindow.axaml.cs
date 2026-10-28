using Avalonia;
using Avalonia.Controls;

namespace SwapDeskCC.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Sem Mica/Acrylic (ex.: Linux), o fundo semitransparente ficaria escuro: usa a cor sólida.
        if (change.Property == ActualTransparencyLevelProperty)
            UpdateBackground();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UpdateBackground();
    }

    // Root é nulo enquanto o XAML ainda está carregando.
    private void UpdateBackground() =>
        Root?.Classes.Set("solid", ActualTransparencyLevel == WindowTransparencyLevel.None);
}
