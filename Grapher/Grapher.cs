namespace Grapher;

public static class Grapher
{
    public static void Start(Action<Renderer> callback)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var form = new Form(callback);
        Application.Run(form);
    }
}
