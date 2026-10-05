namespace LayoutOverlay;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        using var mutex = new Mutex(true, "LayoutOverlay.SingleInstance", out bool isNew);
        if (!isNew) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplication());
    }
}
