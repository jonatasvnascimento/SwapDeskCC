using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SwapDeskCC.App.ViewModels;

namespace SwapDeskCC.App.Services;

/// <summary>Ícone na bandeja com a bolinha da cor da conta ativa e o menu do protótipo.</summary>
public sealed class TrayService : IDisposable
{
    private readonly MainViewModel _vm;
    private readonly Action _showWindow;
    private readonly Action _quit;
    private readonly TrayIcon _tray;
    private readonly Bitmap _baseIcon;

    public TrayService(Application app, MainViewModel vm, Action showWindow, Action quit)
    {
        _vm = vm;
        _showWindow = showWindow;
        _quit = quit;
        _baseIcon = new Bitmap(AssetLoader.Open(new Uri("avares://SwapDeskCC/Assets/app.png")));

        _tray = new TrayIcon { ToolTipText = "SwapDeskCC", IsVisible = true };
        _tray.Clicked += (_, _) => _showWindow();
        TrayIcon.SetIcons(app, [_tray]);

        _vm.StateChanged += Rebuild;
        Rebuild();
    }

    private void Rebuild()
    {
        var config = _vm.Config;
        var active = config.ActiveAccount;
        _tray.ToolTipText = active is null ? "SwapDeskCC" : $"SwapDeskCC · {active.Name}";
        _tray.Icon = RenderTrayIcon(active?.Color);

        var menu = new NativeMenu();
        menu.Add(new NativeMenuItem($"SwapDeskCC · trocar {_vm.ScopeLabel}") { IsEnabled = false });
        menu.Add(new NativeMenuItemSeparator());

        foreach (var account in config.Accounts)
        {
            var id = account.Id;
            var isActive = id == config.ActiveId;
            // No Windows o ícone ocupa o lugar do check, então a conta ativa também ganha um ✓ no texto.
            var item = new NativeMenuItem(isActive ? $"{account.Name}   ✓" : account.Name)
            {
                ToggleType = NativeMenuItemToggleType.Radio,
                IsChecked = isActive,
                Icon = RenderDot(account.Color),
                IsEnabled = !_vm.IsSwapping,
            };
            item.Click += async (_, _) => await _vm.SwapToAsync(id);
            menu.Add(item);
        }

        menu.Add(new NativeMenuItemSeparator());
        var open = new NativeMenuItem("Abrir janela");
        open.Click += (_, _) => _showWindow();
        menu.Add(open);
        var quit = new NativeMenuItem("Sair");
        quit.Click += (_, _) => _quit();
        menu.Add(quit);

        _tray.Menu = menu;
    }

    private WindowIcon RenderTrayIcon(string? color)
    {
        const int size = 64;
        using var rtb = new RenderTargetBitmap(new PixelSize(size, size));
        using (var ctx = rtb.CreateDrawingContext())
        {
            ctx.DrawImage(_baseIcon, new Rect(0, 0, size, size));
            if (color is not null)
                ctx.DrawEllipse(Brush.Parse(color), new Pen(Brushes.White, 4), new Point(size - 15, size - 15), 13, 13);
        }
        return ToIcon(rtb);
    }

    private static Bitmap RenderDot(string color)
    {
        const int size = 16;
        var rtb = new RenderTargetBitmap(new PixelSize(size, size));
        using (var ctx = rtb.CreateDrawingContext())
            ctx.DrawEllipse(Brush.Parse(color), null, new Point(size / 2.0, size / 2.0), 5, 5);
        return rtb;
    }

    private static WindowIcon ToIcon(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms);
        ms.Position = 0;
        return new WindowIcon(ms);
    }

    public void Dispose()
    {
        _vm.StateChanged -= Rebuild;
        _tray.IsVisible = false;
        _tray.Dispose();
        _baseIcon.Dispose();
    }
}
