using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace SwapDeskCC.App.Views;

public partial class AccountsPage : UserControl
{
    private const double MinCardWidth = 230;
    private const double Gap = 12;

    public AccountsPage()
    {
        InitializeComponent();

        // Mesmo efeito do "grid-template-columns: repeat(auto-fill, minmax(230px, 1fr))" do protótipo.
        CardsList.SizeChanged += (_, e) =>
        {
            if (CardsList.ItemsPanelRoot is UniformGrid grid)
                grid.Columns = Math.Max(1, (int)(e.NewSize.Width / (MinCardWidth + Gap)));
        };
    }
}
