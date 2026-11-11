using System.Windows;
using ZharikovApp.Views;

namespace ZharikovApp;

/// <summary>
/// Точка входа приложения ZharikovApp (каркас, логика методик будет добавлена позже).
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
