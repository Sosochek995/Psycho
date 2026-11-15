using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZharikovApp.Models;
using ZharikovApp.Services;

namespace ZharikovApp.ViewModels;

/// <summary>
/// ViewModel главного окна: панель навигации из 12 блоков, область контента
/// (инструкция → вопросы → результат), кнопки Назад/Далее/Завершить блок/
/// Итоговый отчёт. Прогресс автосохраняется через <see cref="SessionService"/>.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly BlockLoaderService _loader = new();
    private readonly SessionService _sessionService = new();
    private readonly NavigationService _navigation = new();
    private readonly BlockProgressService _progressService = new();
    private readonly ScoringService _scoring = new();

    private AppSession _session = new();
    private IReadOnlyList<TestBlock> _blocks = Array.Empty<TestBlock>();

    /// <summary>Идёт ли загрузка/восстановление состояния (отключает побочные эффекты).</summary>
    private bool _isRestoring;

    public MainViewModel()
    {
        Blocks = new ObservableCollection<BlockItemViewModel>();
        Questions = new ObservableCollection<QuestionViewModel>();
        LoadedCommand = new AsyncRelayCommand(LoadAsync);
    }

    // ============================ Общие свойства ============================

    [ObservableProperty]
    private string _title = "Методики Жарикова и Крушельницкого";

    /// <summary>Список 12 блоков для панели навигации.</summary>
    public ObservableCollection<BlockItemViewModel> Blocks { get; }

    /// <summary>Вопросы текущего блока (область контента).</summary>
    public ObservableCollection<QuestionViewModel> Questions { get; }

    [ObservableProperty]
    private BlockItemViewModel? _selectedBlock;

    [ObservableProperty]
    private QuestionViewModel? _currentQuestion;

    [ObservableProperty]
    private int _currentQuestionIndex; // 1-based для отображения

    [ObservableProperty]
    private int _totalQuestions;

    [ObservableProperty]
    private double _overallProgressPercent;

    [ObservableProperty]
    private string _overallProgressText = "Пройдено блоков: 0 из 12";

    [ObservableProperty]
    private bool _isWelcomeVisible = true;

    [ObservableProperty]
    private bool _isInstructionVisible;

    [ObservableProperty]
    private bool _isQuestionsVisible;

    [ObservableProperty]
    private bool _isResultVisible;

    [ObservableProperty]
    private string _blockInstruction = string.Empty;

    [ObservableProperty]
    private string _currentBlockTitle = string.Empty;

    // --- Результат блока ---
    [ObservableProperty]
    private string _resultLevel = string.Empty;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private string _resultScoresText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // ============================ Команды ============================

    public IAsyncRelayCommand LoadedCommand { get; }

    /// <summary>Кнопка «Назад».</summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void GoBack()
    {
        switch (_navigation.Phase)
        {
            case ContentPhase.Questions when _navigation.MovePrevious():
                SyncQuestionView();
                break;
            case ContentPhase.Questions:
                // Уже первый вопрос — возвращаем к инструкции.
                SetPhase(ContentPhase.Instruction);
                break;
            case ContentPhase.Result:
            case ContentPhase.Instruction:
                SetPhase(ContentPhase.Welcome);
                break;
        }
        NotifyButtonStates();
    }

    private bool CanGoBack() => !IsWelcomeVisible;

    /// <summary>Кнопка «Далее»: подтверждение инструкции / переход к следующему вопросу.</summary>
    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void GoNext()
    {
        if (_navigation.Phase == ContentPhase.Instruction)
        {
            _navigation.StartQuestions(_session.CurrentState);
            RebuildQuestions();
            SetPhase(ContentPhase.Questions);
        }
        else if (_navigation.Phase == ContentPhase.Questions)
        {
            if (!_navigation.MoveNext())
            {
                StatusMessage = "Это последний вопрос блока — нажмите «Завершить блок».";
                return;
            }
            SyncQuestionView();
        }
        SaveSession();
        NotifyButtonStates();
    }

    private bool CanGoNext() =>
        IsInstructionVisible ||
        (IsQuestionsVisible && CurrentQuestionIndex < TotalQuestions);

    /// <summary>Кнопка «Завершить блок» — активна только при полной заполненности.</summary>
    [RelayCommand(CanExecute = nameof(CanCompleteBlock))]
    private void CompleteBlock()
    {
        var block = _navigation.CurrentBlock;
        if (block is null) return;

        var answers = UserAnswers.FromSessionState(_session.CurrentState, block.Id);
        var validationErrors = _scoring.ValidateAnswers(block, answers);
        if (validationErrors.Count > 0)
        {
            StatusMessage = "Не все обязательные ответы заполнены: " +
                            string.Join(" ", validationErrors.Take(3));
            NotifyButtonStates();
            return;
        }

        try
        {
            var result = _scoring.CalculateBlock(block, answers);
            _session.UpsertResult(result);
            _session.CurrentState.Reset();
            _navigation.ShowResult();
            ShowResult(result);
            SetPhase(ContentPhase.Result);
            SaveSession();
            RefreshAllStatuses();
            StatusMessage = $"Блок «{block.Title}» завершён.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка подсчёта: " + ex.Message;
        }

        NotifyButtonStates();
    }

    private bool CanCompleteBlock() => IsQuestionsVisible && AllRequiredAnswered();

    /// <summary>Кнопка «Итоговый отчёт».</summary>
    [RelayCommand]
    private void ShowFinalReport()
    {
        var lines = new List<string>
        {
            $"ИТОГОВЫЙ ОТЧЁТ — {OverallProgressText}",
            $"Дата: {DateTime.Now:dd.MM.yyyy HH:mm}",
            new string('─', 60),
        };

        foreach (var block in _blocks)
        {
            var item = Blocks.FirstOrDefault(b => b.Id == block.Id);
            var status = item is not null ? item.StatusText : "Не начат";
            lines.Add($"{block.Order}. {block.Title} — {status}");

            var result = _session.GetResult(block.Id);
            if (result is { IsCompleted: true })
            {
                var scores = FormatScores(result);
                if (!string.IsNullOrEmpty(result.Level))
                    lines.Add($"    Результат: {(scores.Length > 0 ? scores + " — " : "")}{result.Level}");
                if (!string.IsNullOrEmpty(result.Summary))
                    lines.Add($"    {result.Summary}");
            }
        }

        ResultLevel = string.Empty;
        ResultScoresText = string.Empty;
        ResultSummary = string.Join(Environment.NewLine, lines);
        CurrentBlockTitle = "Итоговый отчёт";
        SetPhase(ContentPhase.Result);
        NotifyButtonStates();
    }

    /// <summary>Пройти текущий блок заново (сброс черновика и результата).</summary>
    [RelayCommand]
    private void RestartBlock()
    {
        var block = _navigation.CurrentBlock ??
                    SelectedBlock?.Block;
        if (block is null) return;

        _session.Results.RemoveAll(r =>
            string.Equals(r.BlockId, block.Id, StringComparison.OrdinalIgnoreCase));
        _session.CurrentState.Reset();
        _session.CurrentState.ActiveBlockId = block.Id;

        _navigation.OpenBlock(block.Id, _session.CurrentState); // сбросит фазу на Instruction
        _navigation.StartQuestions(_session.CurrentState);
        CurrentBlockTitle = $"{block.Order}. {block.Title}";
        BlockInstruction = block.Instruction;
        RebuildQuestions();
        SetPhase(ContentPhase.Questions);
        SaveSession();
        RefreshAllStatuses();
        NotifyButtonStates();
    }

    // ============================ Навигация по списку ============================

    partial void OnSelectedBlockChanged(BlockItemViewModel? value)
    {
        if (_isRestoring || value is null) return;

        var status = value.Status;
        if (status == BlockStatus.Completed)
        {
            // Завершённый блок можно открыть повторно — показываем результат.
            _navigation.OpenCompletedResult(value.Id);
            var result = _session.GetResult(value.Id);
            if (result is not null) ShowResult(result);
            CurrentBlockTitle = value.DisplayTitle;
            SetPhase(ContentPhase.Result);
        }
        else
        {
            _navigation.OpenBlock(value.Id, _session.CurrentState);
            CurrentBlockTitle = value.DisplayTitle;
            BlockInstruction = value.Block.Instruction;
            if (_navigation.Phase == ContentPhase.Questions)
                RebuildQuestions();
            SetPhase(_navigation.Phase);
        }

        NotifyButtonStates();
        SaveSession();
    }

    // ============================ Инициализация ============================

    private async Task LoadAsync()
    {
        try
        {
            _isRestoring = true;

            _blocks = await _loader.LoadTestBlocksAsync();
            _navigation.SetBlocks(_blocks);

            _session = _sessionService.Load();

            Blocks.Clear();
            foreach (var block in _blocks)
            {
                var item = new BlockItemViewModel(block, _progressService);
                item.Refresh(_session);
                Blocks.Add(item);
            }

            RefreshOverallProgress();
            StatusMessage = $"Загружено блоков: {_blocks.Count}.";

            // Восстановление незавершённого прохождения после перезапуска.
            var activeId = _session.CurrentState.ActiveBlockId;
            if (!string.IsNullOrEmpty(activeId))
            {
                var item = Blocks.FirstOrDefault(b =>
                    string.Equals(b.Id, activeId, StringComparison.OrdinalIgnoreCase));
                if (item is not null && _session.CurrentState.HasAnswers)
                {
                    SelectedBlock = item; // откроет блок в режиме продолжения
                    StatusMessage = "Прогресс восстановлен — продолжайте прохождение блока.";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Не удалось загрузить блоки: " + ex.Message;
            MessageBox.Show(StatusMessage, "ZharikovApp",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isRestoring = false;
            NotifyButtonStates();
        }
    }

    // ==================== Вспомогательная логика статусов/кнопок ====================

    private bool AllRequiredAnswered()
    {
        var block = _navigation.CurrentBlock;
        if (block is null) return false;

        return block.Questions
            .Where(q => q.Required)
            .All(q => !string.IsNullOrWhiteSpace(_session.CurrentState.Answers.GetValueOrDefault(q.Id)));
    }

    private void RebuildQuestions()
    {
        var block = _navigation.CurrentBlock;
        Questions.Clear();
        if (block is null) return;

        for (var i = 0; i < block.Questions.Count; i++)
        {
            var qvm = new QuestionViewModel(block.Questions[i], i + 1, _session.CurrentState, OnAnswerChanged);
            Questions.Add(qvm);
        }

        TotalQuestions = block.Questions.Count;
        SyncQuestionView();
    }

    private void SyncQuestionView()
    {
        CurrentQuestionIndex = _navigation.CurrentQuestionIndex + 1;
        if (CurrentQuestionIndex >= 1 && CurrentQuestionIndex <= Questions.Count)
            CurrentQuestion = Questions[CurrentQuestionIndex - 1];
        _session.CurrentState.CurrentQuestionIndex = _navigation.CurrentQuestionIndex;
    }

    private void OnAnswerChanged()
    {
        SaveSession();          // автосохранение прогресса на каждый ответ
        RefreshActiveStatus();  // список слева: Не начат → В процессе
        NotifyButtonStates();   // «Завершить блок» активируется при полноте ответов
    }

    private void SaveSession()
    {
        try
        {
            _sessionService.Save(_session);
        }
        catch (Exception ex)
        {
            StatusMessage = "Автосохранение не удалось: " + ex.Message;
        }
    }

    private void RefreshActiveStatus()
    {
        var activeId = _navigation.CurrentBlock?.Id;
        if (activeId is null) return;
        var item = Blocks.FirstOrDefault(b => b.Id == activeId);
        item?.Refresh(_session);
    }

    private void RefreshAllStatuses()
    {
        foreach (var item in Blocks)
            item.Refresh(_session);
        RefreshOverallProgress();
    }

    private void RefreshOverallProgress()
    {
        OverallProgressPercent = _progressService.GetOverallProgressPercent(_session, _blocks);
        var completed = _progressService.GetCompletedCount(_session, _blocks);
        OverallProgressText = $"Пройдено блоков: {completed} из {BlockProgressService.TotalBlocks}";
    }

    private void SetPhase(ContentPhase phase)
    {
        IsWelcomeVisible = phase == ContentPhase.Welcome;
        IsInstructionVisible = phase == ContentPhase.Instruction;
        IsQuestionsVisible = phase == ContentPhase.Questions;
        IsResultVisible = phase == ContentPhase.Result;

        if (phase == ContentPhase.Welcome)
        {
            _isRestoring = true;
            SelectedBlock = null;
            _isRestoring = false;
            _navigation.GoHome();
            CurrentBlockTitle = string.Empty;
        }
    }

    private void ShowResult(BlockResult result)
    {
        CurrentBlockTitle = $"{_blocks.FirstOrDefault(b => b.Id == result.BlockId)?.Order}. {result.Title}";
        ResultLevel = result.Level ?? string.Empty;
        ResultSummary = result.Summary ?? string.Empty;
        ResultScoresText = FormatScores(result);
    }

    private static string FormatScores(BlockResult result)
    {
        if (result.Scores.Count == 0) return string.Empty;
        return string.Join("; ", result.Scores.Select(kv => $"{kv.Key}: {TrimNumber(kv.Value)}"));
    }

    private static string TrimNumber(double v) =>
        Math.Abs(v - Math.Round(v)) < 1e-9 ? ((long)Math.Round(v)).ToString() : v.ToString("0.##");

    private void NotifyButtonStates()
    {
        GoBackCommand.NotifyCanExecuteChanged();
        GoNextCommand.NotifyCanExecuteChanged();
        CompleteBlockCommand.NotifyCanExecuteChanged();
    }
}
