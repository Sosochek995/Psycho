using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ZharikovApp.Models;
using ZharikovApp.Services;

namespace ZharikovApp.ViewModels;

/// <summary>
/// Элемент панели навигации — одна строка списка из 12 блоков.
/// Показывает номер, название и статус прохождения.
/// </summary>
public partial class BlockItemViewModel : ObservableObject
{
    private readonly BlockProgressService _progressService;

    [ObservableProperty]
    private BlockStatus _status = BlockStatus.NotStarted;

    public BlockItemViewModel(TestBlock block, BlockProgressService progressService)
    {
        Block = block ?? throw new ArgumentNullException(nameof(block));
        _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
    }

    /// <summary>Модель блока.</summary>
    public TestBlock Block { get; }

    public string Id => Block.Id;

    /// <summary>Строка вида «1. Диагностика лидерских способностей».</summary>
    public string DisplayTitle => $"{Block.Order}. {Block.Title}";

    /// <summary>Статус-подпись: Не начат / В процессе / Завершён.</summary>
    public string StatusText => BlockProgressService.FormatStatus(Status);

    /// <summary>Значок статуса для списка.</summary>
    public string StatusGlyph => Status switch
    {
        BlockStatus.Completed => "✔",
        BlockStatus.InProgress => "…",
        _ => "○",
    };

    partial void OnStatusChanged(BlockStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusGlyph));
    }

    /// <summary>Пересчитать статус по текущей сессии.</summary>
    public void Refresh(AppSession session)
    {
        Status = _progressService.GetStatus(Block, session);
    }
}
