using System.Windows;
using ZharikovApp.Helpers;
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

        // Single-file публикация: внешние данные (Data/Blocks, docs) не извлекаются
        // рядом с exe — при первом запуске копируем их в %APPDATA%/ZharikovApp/Data.
        PathHelper.EnsureExternalData();

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
