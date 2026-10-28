namespace SwapDeskCC.Core.Platform;

public static class PlatformFactory
{
    public static IPlatformService Create()
    {
        if (OperatingSystem.IsWindows()) return new WindowsPlatform();
        if (OperatingSystem.IsLinux()) return new LinuxPlatform();
        throw new PlatformNotSupportedException("O SwapDeskCC roda no Windows e no Linux.");
    }
}
