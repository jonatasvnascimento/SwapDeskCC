using SwapDeskCC.Core.Models;
using SwapDeskCC.Core.Services;

namespace SwapDeskCC.Core.Tests;

public class SwapEngineTests
{
    private static readonly Account Pessoal = new() { Id = "pessoal", Name = "Pessoal" };
    private static readonly Account Trabalho = new() { Id = "trabalho", Name = "Trabalho", CodeConfigDir = "/home/x/.claude-trabalho" };

    private sealed class FakeOps : ISwapOperations
    {
        public List<string> Calls { get; } = [];
        public string? FailOn { get; init; }

        public bool IsSimulation => false;
        public string DataDirDisplay => "DATA";
        public string ProfilesDisplay => "PROFILES";
        public string BackupsDisplay => "BACKUPS";
        public string LinkMethod => "junction";
        public string ProcessFilterDescription => "filtro";
        public string? LaunchDisplay => "launch";
        public string CodeDirDisplay(Account account) => account.CodeConfigDir;

        private Task Do(string name)
        {
            Calls.Add(name);
            if (name == FailOn) throw new IOException($"falhou em {name}");
            return Task.CompletedTask;
        }

        public async Task<int> CloseDesktopAsync(CancellationToken ct) { await Do("close"); return 3; }
        public Task WaitForUnlockAsync(CancellationToken ct) => Do("unlock");
        public async Task<string> BackupAsync(Account current, CancellationToken ct) { await Do("backup:" + current.Id); return "zip"; }
        public Task ImportCurrentDesktopAsync(Account current, CancellationToken ct) => Do("import:" + current.Id);
        public Task ActivateDesktopAsync(Account target, CancellationToken ct) => Do("desktop:" + target.Id);
        public Task ActivateCodeAsync(Account target, CancellationToken ct) => Do("code:" + target.Id);
        public Task LaunchDesktopAsync(CancellationToken ct) => Do("launch");
    }

    [Fact]
    public async Task Both_scope_runs_all_steps_in_order()
    {
        var ops = new FakeOps();
        var result = await new SwapEngine(ops, new AppSettings()).SwapAsync(Pessoal, Trabalho, SwapScope.Both);

        Assert.True(result.Success);
        Assert.Equal(["close", "unlock", "backup:pessoal", "desktop:trabalho", "code:trabalho", "launch"], ops.Calls);
    }

    [Fact]
    public async Task Code_only_never_touches_desktop()
    {
        var ops = new FakeOps();
        await new SwapEngine(ops, new AppSettings()).SwapAsync(Pessoal, Trabalho, SwapScope.CodeOnly);

        Assert.Equal(["code:trabalho"], ops.Calls);
    }

    [Fact]
    public async Task Respects_backup_and_reopen_settings()
    {
        var ops = new FakeOps();
        var settings = new AppSettings { BackupBeforeSwap = false, ReopenAfterSwap = false };
        await new SwapEngine(ops, settings).SwapAsync(Pessoal, Trabalho, SwapScope.DesktopOnly);

        Assert.Equal(["close", "unlock", "desktop:trabalho"], ops.Calls);
    }

    [Fact]
    public async Task Failure_after_close_reopens_claude_and_reports_step()
    {
        var ops = new FakeOps { FailOn = "desktop:trabalho" };
        var reports = new List<SwapStep>();
        var result = await new SwapEngine(ops, new AppSettings()).SwapAsync(Pessoal, Trabalho, SwapScope.Both, new SyncProgress(reports));

        Assert.False(result.Success);
        Assert.Contains("falhou em desktop:trabalho", result.Error);
        Assert.Equal("launch", ops.Calls[^1]);
        Assert.DoesNotContain("code:trabalho", ops.Calls);
        Assert.Equal(SwapStepState.Failed, reports[^1].State);
    }

    [Fact]
    public async Task Close_step_note_reports_process_count()
    {
        var reports = new List<SwapStep>();
        await new SwapEngine(new FakeOps(), new AppSettings()).SwapAsync(Pessoal, Trabalho, SwapScope.DesktopOnly, new SyncProgress(reports));

        var closeDone = reports.First(r => r.Index == 0 && r.State == SwapStepState.Done);
        Assert.StartsWith("3 processo(s) encerrado(s)", closeDone.Note);
    }

    [Fact]
    public void Describe_matches_prototype_steps()
    {
        var steps = new SwapEngine(new FakeOps(), new AppSettings()).Describe(Pessoal, Trabalho, SwapScope.Both);

        Assert.Equal(6, steps.Count);
        Assert.Equal("Fechando Claude Desktop", steps[0].Title);
        Assert.Equal("Apontando DATA para \"Trabalho\"", steps[3].Title);
        Assert.Equal("Abrindo Claude Desktop", steps[5].Title);
    }

    /// <summary>Progress&lt;T&gt; usa o SynchronizationContext; aqui queremos as notificações na hora.</summary>
    private sealed class SyncProgress(List<SwapStep> sink) : IProgress<SwapStep>
    {
        public void Report(SwapStep value) => sink.Add(value);
    }
}
