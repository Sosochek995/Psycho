using System.IO;
using System.Windows;
using ZharikovApp.Helpers;
using ZharikovApp.Services;
using ZharikovApp.ViewModels;
using ZharikovApp.Views;

namespace ZharikovApp;

/// <summary>
/// Точка входа приложения ZharikovApp.
/// При запуске показывает приветственное окно, затем — главное окно.
/// Логика подсчёта и данные методик здесь не используются.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ZharikovApp.StartupGuard.Install();

        // КРИТИЧНО: пока открыто только приветственное окно, его закрытие при
        // ShutdownMode=OnLastWindowClose завершает приложение — главного окна
        // в этот момент ещё нет. На время приветствия переходим на явное завершение.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        base.OnStartup(e);

        // Single-file публикация: внешние данные (Data/Blocks, docs) не извлекаются
        // рядом с exe — при первом запуске копируем их в %APPDATA%/ZharikovApp/Data.
        PathHelper.EnsureExternalData();

        // --- Приветственное окно ---
        var sessionService = new SessionService();
        var savedSession = sessionService.Load();
        bool hasProgress = savedSession.CompletedCount > 0 || HasDraftAnswers(savedSession);

        var welcome = new WelcomeWindow();
        if (hasProgress)
        {
            welcome.ViewModel.HasSavedSession = true;
            welcome.ViewModel.SavedSessionInfo =
                $"Найдена сохранённая сессия: завершено блоков — {savedSession.CompletedCount} из 12.";
        }

        // Сброс удаляет ТОЛЬКО пользовательскую сессию (прогресс),
        // файлы методик (Data/Blocks, docs) не затрагиваются.
        welcome.ViewModel.ConfirmAndClearSession = () =>
        {
            try
            {
                sessionService.Clear();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        };

        welcome.ShowDialog();

        switch (welcome.ChosenAction)
        {
            case WelcomeViewModel.CancelAction:
                // Пользователь закрыл окно без начала тестирования.
                Shutdown();
                return;

            case WelcomeViewModel.StartAction:
            case WelcomeViewModel.ContinueAction:
            case WelcomeViewModel.RestartAction:
            default:
                // Главное окно само восстанавливает сессию через SessionService
                // («Начать заново» уже очистило файл — откроется чистая сессия).
                break;
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();

        // Главное окно открыто — возвращаем штатный режим завершения.
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    /// <summary>Есть ли у сессии черновые (незавершённые) ответы.</summary>
    private static bool HasDraftAnswers(Models.AppSession session)
    {
        var answers = session.CurrentState.Answers;
        return answers is { Count: > 0 };
    }
}
