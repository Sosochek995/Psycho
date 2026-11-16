using ZharikovApp.Models;

namespace ZharikovApp.Services;

/// <summary>Статус прохождения блока пользователем.</summary>
public enum BlockStatus
{
    /// <summary>Не начат: нет ни черновых ответов, ни результата.</summary>
    NotStarted,

    /// <summary>В процессе: есть черновые ответы (или блок открыт, но не завершён).</summary>
    InProgress,

    /// <summary>Завершён: есть сохранённый результат блока.</summary>
    Completed,
}

/// <summary>
/// Вычисление статусов и прогресса блоков по состоянию сессии.
/// Чистая логика без зависимостей от UI; детерминирована.
/// </summary>
public class BlockProgressService
{
    /// <summary>Общее число блоков методики.</summary>
    public const int TotalBlocks = 12;

    /// <summary>
    /// Статус блока:
    /// Completed — есть завершённый результат;
    /// InProgress — блок активен или есть черновые ответы;
    /// иначе — NotStarted.
    /// </summary>
    public BlockStatus GetStatus(TestBlock block, AppSession session)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(session);

        var result = session.GetResult(block.Id);
        if (result is { IsCompleted: true })
            return BlockStatus.Completed;

        // Черновые ответы относятся только к активному блоку (одиночное прохождение).
        var isDraftActive = string.Equals(session.CurrentState.ActiveBlockId, block.Id,
                                              StringComparison.OrdinalIgnoreCase)
                            && session.CurrentState.HasAnswers;

        return isDraftActive ? BlockStatus.InProgress : BlockStatus.NotStarted;
    }

    /// <summary>Число завершённых блоков (0–12).</summary>
    public int GetCompletedCount(AppSession session, IReadOnlyList<TestBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(blocks);
        return blocks.Count(b => session.GetResult(b.Id) is { IsCompleted: true });
    }

    /// <summary>Прогресс в процентах (доля завершённых блоков из 12), 0–100.</summary>
    public double GetOverallProgressPercent(AppSession session, IReadOnlyList<TestBlock> blocks)
    {
        var total = Math.Max(1, Math.Min(blocks.Count, TotalBlocks));
        return GetCompletedCount(session, blocks) * 100.0 / total;
    }

    /// <summary>Доля отвеченных обязательных вопросов активного блока, 0–1.</summary>
    public double GetBlockCompletionRatio(TestBlock block, AppSession session)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(session);

        if (session.GetResult(block.Id) is { IsCompleted: true })
            return 1.0;

        if (!string.Equals(session.CurrentState.ActiveBlockId, block.Id, StringComparison.OrdinalIgnoreCase))
            return 0.0;

        var required = block.Questions.Count(q => q.Required);
        if (required == 0)
            return 1.0;

        var answered = block.Questions
            .Where(q => q.Required && !string.IsNullOrWhiteSpace(session.CurrentState.Answers.GetValueOrDefault(q.Id)))
            .Count();

        return (double)answered / required;
    }

    /// <summary>Текстовое представление статуса для списка навигации.</summary>
    public static string FormatStatus(BlockStatus status) => status switch
    {
        BlockStatus.Completed => "Завершён",
        BlockStatus.InProgress => "В процессе",
        _ => "Не начат",
    };
}
