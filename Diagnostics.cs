using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ZharikovApp;

/// <summary>
/// Диагностика запуска. Делает любую ошибку видимой и пишет журнал.
/// Логику приложения не меняет: только наблюдает и сообщает.
/// Журнал: %LOCALAPPDATA%\ZharikovApp\startup.log
/// </summary>
public static class Diagnostics
{
    private static bool _installed;
    private static string _logPath = "";

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

        Write("=== ЗАПУСК " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");

        var app = Application.Current;
        if (app != null)
        {
            Write("ShutdownMode  = " + app.ShutdownMode);
        }

        Write("BaseDirectory = " + AppContext.BaseDirectory);
        string exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        Write("ExeDirectory  = " + exeDir);

        Probe(Path.Combine(AppContext.BaseDirectory, "Data", "Blocks"));
        Probe(Path.Combine(exeDir, "Data", "Blocks"));

        if (app != null)
        {
            app.DispatcherUnhandledException += (s, args) =>
            {
                Write("UI EXCEPTION", args.Exception);
                Report(args.Exception, "Ошибка в интерфейсе");
                args.Handled = true;
            };

            app.Exit += (s, args) =>
            {
                Write("=== ВЫХОД код=" + args.ApplicationExitCode + " ===");
            };
        }

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            if (ex != null)
            {
                Write("DOMAIN EXCEPTION terminating=" + args.IsTerminating, ex);
                Report(ex, "Критическая ошибка");
            }
            else
            {
                Write("DOMAIN EXCEPTION terminating=" + args.IsTerminating);
            }
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Write("TASK EXCEPTION", args.Exception);
            args.SetObserved();
        };

        Write("обработчики ошибок установлены");
    }

    private static void Probe(string path)
    {
        try
        {
            bool exists = Directory.Exists(path);
            int count = exists ? Directory.GetFiles(path, "*.json").Length : 0;
            Write("Data/Blocks | " + path + " | exists=" + exists + " | json=" + count);
        }
        catch (Exception ex)
        {
            Write("probe error " + path + ": " + ex.Message);
        }
    }

    private static void Report(Exception ex, string title)
    {
        try
        {
            MessageBox.Show(
                ex.GetType().Name + ": " + ex.Message +
                "\n\nЖурнал:\n" + _logPath +
                "\n\n" + ex.StackTrace,
                title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // Показ ошибки не должен сам становиться причиной падения.
        }
    }

    private static void Write(string line)
    {
        Append("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + line + Environment.NewLine);
    }

    private static void Write(string line, Exception ex)
    {
        Append("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + line +
               Environment.NewLine + ex + Environment.NewLine);
    }

    private static void Append(string text)
    {
        try
        {
            File.AppendAllText(_logPath, text, Encoding.UTF8);
        }
        catch
        {
            // Журнал не должен ломать запуск, если диск недоступен.
        }
    }
}
