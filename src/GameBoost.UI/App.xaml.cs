using System.Windows;
namespace GameBoost.UI;

public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, @"Local\GameBoost.UI", out var created);
        if (!created) { MessageBox.Show("O GameBoost já está aberto."); Shutdown(); return; }
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
