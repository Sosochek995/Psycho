using ZharikovApp.Models;

namespace ZharikovApp.Services.Interfaces;

/// <summary>
/// Сервис подсчёта результатов блоков методик.
/// Не зависит от UI; все расчёты детерминированы:
/// одинаковые (block, answers) → одинаковый результат (кроме CompletedAt).
/// </summary>
public interface IScoringService
{
    /// <summary>
    /// Проверить ответы пользователя на полноту и корректность
    /// относительно структуры блока. Возвращает список ошибок
    /// (пустой список — ответы валидны).
    /// </summary>
    IReadOnlyList<string> ValidateAnswers(TestBlock block, UserAnswers answers);

    /// <summary>
    /// Рассчитать результат блока по ответам. Бросает
    /// <see cref="InvalidOperationException"/>, если ответы невалидны
    /// (список ошибок включается в сообщение).
    /// </summary>
    BlockResult CalculateBlock(TestBlock block, UserAnswers answers);

    /// <summary>
    /// Подобрать интерпретацию для уже рассчитанного результата.
    /// Возвращает найденное правило или null, если правило не подходит.
    /// </summary>
    InterpretationRule? GetInterpretation(BlockResult result);
}
