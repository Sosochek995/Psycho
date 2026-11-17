#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ZharikovApp;

/// <summary>
/// Страж запуска. Чинит жизненный цикл окон и делает ошибки видимыми.
/// Существующую логику приложения не изменяет: только наблюдает и корректирует.
/// Журнал: %LOCALAPPDATA%\ZharikovApp\startup.log
/// </summary>
public static class StartupGuard
{
    private static bool _installed;
    private static bool _mainEnsured;
    private static string _logPath = "";
    private static readonly HashSet<Window> Seen = new HashSet<Window>();

    public static void Install()
    {
        if (_installed) return;
        _installed = true;

        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZharikovApp");
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "startup.log");
        }
        catch
        {
            _logPath = Path.Combine(Path.GetTempPath(), "ZharikovApp-startup.log");
        }

        Log("=== ЗАПУСК " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
        Log("BaseDirectory = " + AppContext.BaseDirectory);
        string exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        Log("ExeDirectory  = " + exeDir);
        Probe(Path.Combine(AppContext.BaseDirectory, "Data", "Blocks"));
        Probe(Path.Combine(exeDir, "Data", "Blocks"));

        var app = Application.Current;
        if (app == null) { Log("Application.Current is null"); return; }

        Log("ShutdownMode было = " + app.ShutdownMode);
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Log("ShutdownMode стало = OnExplicitShutdown");

        app.DispatcherUnhandledException += (s, a) =>
        {
            Log("UI EXCEPTION", a.Exception);
            Show(a.Exception, "Ошибка в интерфейсе");
            a.Handled = true;
        };

        app.Exit += (s, a) => Log("=== ВЫХОД код=" + a.ApplicationExitCode + " ===");

        AppDomain.CurrentDomain.UnhandledException += (s, a) =>
        {
            var ex = a.ExceptionObject as Exception;
            if (ex != null)
            {
                Log("DOMAIN EXCEPTION terminating=" + a.IsTerminating, ex);
                Show(ex, "Критическая ошибка");
            }
            else
            {
                Log("DOMAIN EXCEPTION terminating=" + a.IsTerminating);
            }
        };

        TaskScheduler.UnobservedTaskException += (s, a) =>
        {
            Log("TASK EXCEPTION", a.Exception);
            a.SetObserved();
        };

        EventManager.RegisterClassHandler(typeof(Window),
            FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));

        Log("StartupGuard установлен");
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var w = sender as Window;
        if (w == null) return;
        if (!Seen.Add(w)) return;

        string name = w.GetType().Name;
        Log("окно загружено: " + w.GetType().FullName);

        w.Closing += (s, a) =>
        {
            Log("окно закрывается: " + name);
            if (name.IndexOf("Welcome", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Кнопки привязаны к командам, поэтому DialogResult не выставляется сам.
                // Без этого App трактует закрытие как «Отмена» и завершает работу.
                try
                {
                    w.DialogResult = true;
                    Log("DialogResult приветствия = true");
                }
                catch (Exception ex)
                {
                    Log("DialogResult не установлен: " + ex.Message);
                }
            }
        };

        w.Closed += (s, a) =>
        {
            Log("окно закрыто: " + name);
            var app = Application.Current;
            if (app == null) return;
            // Откладываем до простоя диспетчера: сначала должен отработать
            // код App после ShowDialog, включая возможный явный Shutdown.
            app.Dispatcher.BeginInvoke(new Action(EnsureMainWindow),
                                       DispatcherPriority.ApplicationIdle);
        };

        if (name == "MainWindow")
        {
            var app = Application.Current;
            if (app != null)
            {
                app.MainWindow = w;
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                Log("MainWindow зарегистрирован, ShutdownMode = OnMainWindowClose");
            }
        }
    }

    private static void EnsureMainWindow()
    {
        var app = Application.Current;
        if (app == null) return;
        if (app.Dispatcher.HasShutdownStarted)
        {
            Log("завершение уже начато, главное окно не создаём");
            return;
        }

        foreach (Window w in app.Windows)
        {
            if (w.GetType().Name == "MainWindow" && w.IsVisible)
            {
                Log("главное окно уже открыто");
                return;
            }
        }

        if (_mainEnsured) return;
        _mainEnsured = true;

        try
        {
            Type mt = FindMainType();
            if (mt == null) { Log("тип MainWindow не найден"); return; }

            var win = Activator.CreateInstance(mt) as Window;
            if (win == null) { Log("не удалось создать MainWindow"); return; }

            app.MainWindow = win;
            win.Show();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            Log("главное окно показано после закрытия приветствия");
        }
        catch (Exception ex)
        {
            Log("ПРОВАЛ создания MainWindow", ex);
            Show(ex, "Не удалось открыть главное окно");
        }
    }

    private static Type FindMainType()
    {
        var asm = typeof(StartupGuard).Assembly;
        var t = asm.GetType("ZharikovApp.Views.MainWindow");
        if (t != null) return t;
        t = asm.GetType("ZharikovApp.MainWindow");
        if (t != null) return t;
        try
        {
            foreach (var x in asm.GetTypes())
                if (typeof(Window).IsAssignableFrom(x) && x.Name == "MainWindow") return x;
        }
        catch { }
        return null;
    }

    private static void Probe(string path)
    {
        try
        {
            bool exists = Directory.Exists(path);
            int count = exists ? Directory.GetFiles(path, "*.json").Length : 0;
            Log("Data/Blocks | " + path + " | exists=" + exists + " | json=" + count);
        }
        catch (Exception ex)
        {
            Log("probe error " + path + ": " + ex.Message);
        }
    }

    private static void Show(Exception ex, string title)
    {
        try
        {
            MessageBox.Show(
                ex.GetType().Name + ": " + ex.Message +
                "\n\nЖурнал:\n" + _logPath + "\n\n" + ex.StackTrace,
                title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }

    private static void Log(string line)
    {
        Append("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + line + Environment.NewLine);
    }

    private static void Log(string line, Exception ex)
    {
        Append("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + line +
               Environment.NewLine + ex + Environment.NewLine);
    }

    private static void Append(string text)
    {
        try { File.AppendAllText(_logPath, text, Encoding.UTF8); }
        catch { }
    }
}
