namespace SwapDeskCC.Core.Models;

public enum SwapStepState
{
    Pending,
    Running,
    Done,
    Failed,
}

/// <summary>Uma etapa da troca, como aparece na janela de progresso.</summary>
public sealed record SwapStep(int Index, string Title, string? Note, SwapStepState State);

public sealed record SwapResult(bool Success, string? Error = null)
{
    public static readonly SwapResult Ok = new(true);
    public static SwapResult Fail(string error) => new(false, error);
}

public static class SwapScopeExtensions
{
    public static bool IncludesDesktop(this SwapScope s) => s != SwapScope.CodeOnly;
    public static bool IncludesCode(this SwapScope s) => s != SwapScope.DesktopOnly;

    public static string Label(this SwapScope s) => s switch
    {
        SwapScope.Both => "Desktop + Code",
        SwapScope.DesktopOnly => "Só Desktop",
        _ => "Só Code",
    };
}
