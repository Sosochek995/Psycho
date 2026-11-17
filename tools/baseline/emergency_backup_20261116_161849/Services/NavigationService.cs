using ZharikovApp.Models;

namespace ZharikovApp.Services;

/// <summary>Фаза отображаемого контента.</summary>
public enum ContentPhase
{
    /// <summary>Экран приветствия (блок не выбран).</summary>
    Welcome,

    /// <summary>Инструкция перед началом блока.</summary>
    Instruction,

    /// <summary>Вопросы блока.</summary>
    Questions,

    /// <summary>Краткий результат завершённого блока.</summary>
    Result,
}

/// <summary>
/// Навигация приложения: выбор блока из списка, переходы по вопросам,
/// переключение фаз «инструкция → вопросы → результат».
/// Чистая логика без зависимостей от WPF.
/// </summary>
public class NavigationService
{
    private IReadOnlyList<TestBlock> _blocks = Array.Empty<TestBlock>();

    /// <summary>Текущий блок (null — экран приветствия).</summary>
    public TestBlock? CurrentBlock { get; private set; }

    /// <summary>Индекс текущего вопроса внутри блока (0-based).</summary>
    public int CurrentQuestionIndex { get; private set; }

    /// <summary>Текущая фаза контента.</summary>
    public ContentPhase Phase { get; private set; } = ContentPhase.Welcome;

    /// <summary>Задать список блоков (вызывается один раз после загрузки).</summary>
    public void SetBlocks(IReadOnlyList<TestBlock> blocks) =>
        _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));

    /// <summary>Все блоки в порядке Order.</summary>
    public IReadOnlyList<TestBlock> Blocks => _blocks;

    /// <summary>Открыть блок: если есть черновики — продолжаем с текущего вопроса,
    /// иначе показываем инструкцию.</summary>
    public void OpenBlock(string blockId, SessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var block = _blocks.FirstOrDefault(b =>
            string.Equals(b.Id, blockId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Блок '{blockId}' не найден.");

        CurrentBlock = block;

        if (string.Equals(state.ActiveBlockId, block.Id, StringComparison.OrdinalIgnoreCase) && state.HasAnswers)
        {
            // Возобновление черновика: встаём на первый неотвеченный вопрос.
            Phase = ContentPhase.Questions;
            CurrentQuestionIndex = FirstUnansweredIndex(block, state);
        }
        else
        {
            Phase = ContentPhase.Instruction;
            CurrentQuestionIndex = 0;
        }
    }

    /// <summary>Пользователь подтвердил инструкцию — начинаем отвечать.</summary>
    public void StartQuestions(SessionState state)
    {
        if (CurrentBlock is null) return;

        if (!string.Equals(state.ActiveBlockId, CurrentBlock.Id, StringComparison.OrdinalIgnoreCase))
        {
            state.Reset();
            state.ActiveBlockId = CurrentBlock.Id;
        }

        Phase = ContentPhase.Questions;
        CurrentQuestionIndex = Math.Clamp(CurrentQuestionIndex, 0, CurrentBlock.Questions.Count - 1);
    }

    /// <summary>Перейти к следующему вопросу (если возможно). true — перешли.</summary>
    public bool MoveNext()
    {
        if (Phase != ContentPhase.Questions || CurrentBlock is null) return false;
        if (CurrentQuestionIndex >= CurrentBlock.Questions.Count - 1) return false;
        CurrentQuestionIndex++;
        return true;
    }

    /// <summary>Вернуться к предыдущему вопросу. true — вернулись; false — уже первый
    /// (ViewModel может вернуть пользователя к инструкции).</summary>
    public bool MovePrevious()
    {
        if (Phase != ContentPhase.Questions || CurrentBlock is null) return false;
        if (CurrentQuestionIndex <= 0) return false;
        CurrentQuestionIndex--;
        return true;
    }

    /// <summary>Показать результат завершённого блока.</summary>
    public void ShowResult()
    {
        if (CurrentBlock is not null)
            Phase = ContentPhase.Result;
    }

    /// <summary>Повторно открыть завершённый блок (просмотр результата).</summary>
    public void OpenCompletedResult(string blockId)
    {
        var block = _blocks.FirstOrDefault(b =>
            string.Equals(b.Id, blockId, StringComparison.OrdinalIgnoreCase));
        if (block is null) return;

        CurrentBlock = block;
        CurrentQuestionIndex = 0;
        Phase = ContentPhase.Result;
    }

    /// <summary>Покинуть блок, вернуться на экран приветствия.</summary>
    public void GoHome()
    {
        CurrentBlock = null;
        CurrentQuestionIndex = 0;
        Phase = ContentPhase.Welcome;
    }

    /// <summary>Индекс первого неотвеченного обязательного вопроса (или последний допустимый).</summary>
    public static int FirstUnansweredIndex(TestBlock block, SessionState state)
    {
        for (var i = 0; i < block.Questions.Count; i++)
        {
            var q = block.Questions[i];
            if (q.Required && !state.Answers.ContainsKey(q.Id))
                return i;
        }
        return Math.Max(0, block.Questions.Count - 1);
    }
}
