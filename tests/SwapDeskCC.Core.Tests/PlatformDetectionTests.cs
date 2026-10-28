using SwapDeskCC.Core.Platform;

namespace SwapDeskCC.Core.Tests;

public class PlatformDetectionTests
{
    /// <summary>
    /// Informativo: diz se os testes estão rodando dentro do contêiner de um app empacotado
    /// (ex.: disparados por um terminal do Claude Desktop). A detecção não pode lançar exceção nem deixar a pasta de prova.
    /// </summary>
    [Fact]
    public void Package_container_detection_runs_and_cleans_up()
    {
        var platform = PlatformFactory.Create();
        _ = platform.IsRunningInsidePackage;

        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Assert.Empty(Directory.GetDirectories(roaming, ".swapdeskcc-probe-*"));
    }
}
