namespace CornwallRoom;

internal static class AppConfig
{
    public static void Initialize()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
    }
}

internal static class Program
{
    [STAThread]
    static void Main()
    {
        AppConfig.Initialize();          
        Application.Run(new MainForm()); 
    }
}