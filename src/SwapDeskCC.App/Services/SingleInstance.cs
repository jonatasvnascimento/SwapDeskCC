namespace SwapDeskCC.App.Services;

/// <summary>
/// Uma instância só. Se o usuário abrir o SwapDeskCC de novo, a instância que já roda mostra a janela.
/// O aviso entre processos usa EventWaitHandle nomeado, que só existe no Windows; no Linux a segunda instância apenas sai.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private static readonly string Id = $"SwapDeskCC-{Environment.UserName}";
    private readonly Mutex _mutex;
    private EventWaitHandle? _show;

    public bool IsFirst { get; }

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, Id, out var createdNew);
        IsFirst = createdNew;
    }

    public void SignalFirstInstance()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var show = EventWaitHandle.OpenExisting(Id + "-show");
            show.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { }
    }

    public void Listen(Action onShowRequested)
    {
        if (!OperatingSystem.IsWindows()) return;
        _show = new EventWaitHandle(false, EventResetMode.AutoReset, Id + "-show");
        var thread = new Thread(() =>
        {
            while (_show.WaitOne()) onShowRequested();
        }) { IsBackground = true, Name = "SwapDeskCC single instance" };
        thread.Start();
    }

    public void Dispose()
    {
        if (IsFirst) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
