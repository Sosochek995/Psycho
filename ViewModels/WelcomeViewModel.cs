using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ZharikovApp.ViewModels;

/// <summary>
/// ViewModel приветственного окна. Показывает список исследуемых аспектов личности,
/// предлагает «Начать тестирование», а при наличии сохранённой сессии —
/// «Продолжить» / «Начать заново». Никаких данных методик окно не меняет:
/// «Начать заново» удаляет только пользовательскую сессию (прогресс) после
/// явного подтверждения пользователем.
/// </summary>
public partial class WelcomeViewModel : ObservableObject
{
    /// <summary>Имя файла лога отладочных сообщений (на экран ничего не выводим).</summary>
    private const string LogFileName = "welcome.log";

    public WelcomeViewModel()
    {
        StartCommand = new RelayCommand(() => Close?.Invoke(StartAction));
        ContinueCommand = new RelayCommand(() => Close?.Invoke(ContinueAction));
        RestartCommand = new RelayCommand(OnRestart);
        CancelCommand = new RelayCommand(() => Close?.Invoke(CancelAction));
    }

    // ===================== Коды действий (для App.OnStartup) =====================

    /// <summary>Пользователь выбрал: начать новое прохождение.</summary>
    public const int StartAction = 1;

    /// <summary>Пользователь выбрал: продолжить сохранённую сессию.</summary>
    public const int ContinueAction = 2;

    /// <summary>Пользователь выбрал: начать заново (сессия уже очищена с подтверждением).</summary>
    public const int RestartAction = 3;

    /// <summary>Пользователь закрыл окно без начала тестирования.</summary>
    public const int CancelAction = 0;

    /// <summary>Действие, выбранное пользователем (считывается после ShowDialog).</summary>
    public int SelectedAction { get; private set; } = CancelAction;

    /// <summary>Колбэк закрытия окна, задаётся из code-behind.</summary>
    public Action<int>? Close { get; set; }

    /// <summary>Есть ли сохранённая сессия с прогрессом. Задаётся перед показом окна.</summary>
    [ObservableProperty]
    private bool _hasSavedSession;

    /// <summary>Краткое описание сохранённого прогресса для подписи под кнопками.</summary>
    [ObservableProperty]
    private string _savedSessionInfo = string.Empty;

    /// <summary>Запрашивает подтверждение и выполняет сброс через колбэк.</summary>
    public Func<bool>? ConfirmAndClearSession { get; set; }

    // ============================ Команды ============================

    /// <summary>«Начать тестирование» — новая сессия без изменений сохранённых данных.</summary>
    public IRelayCommand StartCommand { get; }

    /// <summary>«Продолжить» — открыть главное окно с восстановлением сессии.</summary>
    public IRelayCommand ContinueCommand { get; }

    /// <summary>«Отмена» — закрыть приложение без прохождения.</summary>
    public IRelayCommand CancelCommand { get; }

    /// <summary>«Начать заново» — требует подтверждения, затем очищает только пользовательскую сессию.</summary>
    public IRelayCommand RestartCommand { get; }

    private void OnRestart()
    {
        var confirmed = MessageBox.Show(
            "Вы уверены, что хотите начать заново?\n" +
            "Все ранее сохранённые результаты этого пользователя будут удалены.\n" +
            "Файлы методик и настройки приложения останутся без изменений.",
            "Подтверждение сброса",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmed != MessageBoxResult.Yes)
            return;

        // Очистку выполняет вызывающая сторона (App), здесь только запрос.
        var cleared = ConfirmAndClearSession?.Invoke() ?? false;
        if (!cleared)
        {
            TryLog("Сброс сессии не выполнен (отказ или ошибка).");
            return;
        }

        Close?.Invoke(RestartAction);
    }

    /// <summary>Отладочная информация пишется только в лог-файл, не на экран.</summary>
    private static void TryLog(string message)
    {
        try
        {
            var dir = ZharikovApp.Helpers.PathHelper.UserDataDirectory;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(dir, LogFileName),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // Логирование не должно влиять на работу приложения.
        }
    }
}
