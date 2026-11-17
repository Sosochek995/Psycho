namespace ZharikovApp.Models;

/// <summary>
/// Базовая модель одного блока методики.
/// Соответствует блокам 1–12 из docs/methodics_source.md.
/// Логика заполнения и подсчёта будет реализована позже.
/// </summary>
public class BlockBase
{
    /// <summary>Уникальный идентификатор блока, например "block_01_leadership".</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Порядковый номер блока (1–12).</summary>
    public int Order { get; set; }

    /// <summary>Название блока.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Инструкция для испытуемого.</summary>
    public string Instruction { get; set; } = string.Empty;
}
