using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using SwapDeskCC.App.ViewModels;

namespace SwapDeskCC.App.Views;

/// <summary>
/// Aviso no canto da tela (inferior direito no Windows, topo no Linux), como o toast do protótipo.
/// Janela própria para aparecer mesmo com a janela principal escondida na bandeja.
/// </summary>
public partial class ToastWindow : Window
{
    private static readonly TimeSpan Visible = TimeSpan.FromSeconds(3.5);
    private readonly DispatcherTimer _timer;
    private readonly bool _topCenter;

    public event Action? Clicked;

    public ToastWindow() : this(topCenter: false) { }

    public ToastWindow(bool topCenter)
    {
        _topCenter = topCenter;
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = Visible };
        _timer.Tick += (_, _) => FadeOut();
        Card.PointerPressed += OnCardPressed;
    }

    private void OnCardPressed(object? sender, PointerPressedEventArgs e)
    {
        FadeOut();
        Clicked?.Invoke();
    }

    public void ShowToast(ToastRequest request)
    {
        Avatar.Background = Brush.Parse(request.Color);
        AvatarText.Text = request.Initials;
        TitleText.Text = request.Title;
        SubText.Text = request.Subtitle;

        _timer.Stop();
        if (!IsVisible) Show();
        Dispatcher.UIThread.Post(PlaceOnScreen, DispatcherPriority.Render);
        Card.Opacity = 1;
        if (!request.Sticky) _timer.Start();
    }

    private void PlaceOnScreen()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        var width = (int)(Bounds.Width * scale);
        var height = (int)(Bounds.Height * scale);

        Position = _topCenter
            ? new PixelPoint(area.X + (area.Width - width) / 2, area.Y)
            : new PixelPoint(area.Right - width, area.Bottom - height);
    }

    private async void FadeOut()
    {
        _timer.Stop();
        Card.Opacity = 0;
        await Task.Delay(300);
        if (Card.Opacity == 0) Hide();
    }
}
