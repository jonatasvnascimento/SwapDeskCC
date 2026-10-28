using SwapDeskCC.Core.Models;

namespace SwapDeskCC.Core.Services;

/// <summary>
/// Monta a lista de etapas (a mesma que a janela de progresso mostra) e executa uma a uma.
/// Se algo falhar depois de fechar o Claude, tenta reabri-lo para o usuário não ficar sem nada.
/// </summary>
public sealed class SwapEngine(ISwapOperations ops, AppSettings settings)
{
    public ISwapOperations Operations => ops;

    private enum StepKind { Other, Close, Launch }

    private sealed record PlannedStep(string Title, string? Note, Func<CancellationToken, Task<string?>> Run, StepKind Kind = StepKind.Other);

    /// <summary>Etapas de uma troca, sem executar nada (usado na pré-visualização e nos testes).</summary>
    public IReadOnlyList<SwapStep> Describe(Account from, Account to, SwapScope scope) =>
        PlanSwap(from, to, scope).Select((s, i) => new SwapStep(i, s.Title, s.Note, SwapStepState.Pending)).ToList();

    public Task<SwapResult> SwapAsync(Account from, Account to, SwapScope scope, IProgress<SwapStep>? progress = null, CancellationToken ct = default) =>
        RunAsync(PlanSwap(from, to, scope), scope.IncludesDesktop(), progress, ct);

    /// <summary>Primeira vez no modo real: guarda a pasta atual do Claude como perfil da conta ativa.</summary>
    public Task<SwapResult> ImportAsync(Account current, IProgress<SwapStep>? progress = null, CancellationToken ct = default)
    {
        var steps = new List<PlannedStep>
        {
            CloseStep(),
            UnlockStep(),
            new("Backup completo de segurança", ops.BackupsDisplay, async c => await ops.BackupAsync(current, c)),
            new($"Movendo {ops.DataDirDisplay} para o perfil \"{current.Name}\"", $"{ops.ProfilesDisplay} · via {ops.LinkMethod}",
                async c => { await ops.ImportCurrentDesktopAsync(current, c); return null; }),
            LaunchStep(),
        };
        return RunAsync(steps, true, progress, ct);
    }

    private List<PlannedStep> PlanSwap(Account from, Account to, SwapScope scope)
    {
        var steps = new List<PlannedStep>();

        if (scope.IncludesDesktop())
        {
            steps.Add(CloseStep());
            steps.Add(UnlockStep());
            if (settings.BackupBeforeSwap)
                steps.Add(new("Backup de segurança", Path.Combine(ops.BackupsDisplay, $"{from.Id}-….zip"),
                    async c => await ops.BackupAsync(from, c)));
            steps.Add(new($"Apontando {ops.DataDirDisplay} para \"{to.Name}\"", $"via {ops.LinkMethod}",
                async c => { await ops.ActivateDesktopAsync(to, c); return null; }));
        }

        if (scope.IncludesCode())
        {
            steps.Add(new("Claude Code: trocando CLAUDE_CONFIG_DIR", $"{ops.CodeDirDisplay(to)}  (vale para terminais novos)",
                async c => { await ops.ActivateCodeAsync(to, c); return null; }));
        }

        if (scope.IncludesDesktop() && settings.ReopenAfterSwap) steps.Add(LaunchStep());

        return steps;
    }

    private PlannedStep CloseStep() => new("Fechando Claude Desktop", ops.ProcessFilterDescription, async c =>
    {
        var n = await ops.CloseDesktopAsync(c);
        var verb = ops.IsSimulation ? "encontrado(s)" : "encerrado(s)";
        return n == 0 ? "o Claude Desktop não estava aberto" : $"{n} processo(s) {verb} · {ops.ProcessFilterDescription}";
    }, StepKind.Close);

    private PlannedStep UnlockStep() => new("Aguardando liberar arquivos", null, async c =>
    {
        await ops.WaitForUnlockAsync(c);
        return null;
    });

    private PlannedStep LaunchStep() => new("Abrindo Claude Desktop", ops.LaunchDisplay, async c =>
    {
        await ops.LaunchDesktopAsync(c);
        return null;
    }, StepKind.Launch);

    private async Task<SwapResult> RunAsync(List<PlannedStep> steps, bool touchesDesktop, IProgress<SwapStep>? progress, CancellationToken ct)
    {
        var desktopClosed = false;
        var launched = false;

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            progress?.Report(new SwapStep(i, step.Title, step.Note, SwapStepState.Running));
            try
            {
                var note = await step.Run(ct);
                progress?.Report(new SwapStep(i, step.Title, note ?? step.Note, SwapStepState.Done));
            }
            catch (Exception e)
            {
                progress?.Report(new SwapStep(i, step.Title, e.Message, SwapStepState.Failed));
                if (desktopClosed && !launched && touchesDesktop && settings.ReopenAfterSwap)
                {
                    try { await ops.LaunchDesktopAsync(CancellationToken.None); }
                    catch { /* melhor esforço */ }
                }
                return SwapResult.Fail(e is OperationCanceledException ? "Troca cancelada." : e.Message);
            }

            if (step.Kind == StepKind.Close) desktopClosed = true;
            if (step.Kind == StepKind.Launch) launched = true;
        }

        return SwapResult.Ok;
    }
}
