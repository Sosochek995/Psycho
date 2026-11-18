#nullable enable
using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZharikovApp.Models;
using ZharikovApp.Services;

namespace ZharikovApp.ViewModels;

/// <summary>
/// Дополнения к ViewModel главного окна: вкладки пакетов, счётчики прогресса,
/// перехват показа результата и открытие итогового отчёта.
/// Экспорт и переход к следующему блоку берутся из MainViewModelResults.cs.
/// Подсчёт, ключи и формулы не затрагиваются.
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty] private bool _isFinalReport;
    [ObservableProperty] private bool _allCompleted;
    [ObservableProperty] private bool _hasNextBlock;
    [ObservableProperty] private int _completedCount;
    [ObservableProperty] private int _remainingCount;
    [ObservableProperty] private string _savedTitle = string.Empty;
    [ObservableProperty] private string _savedNote = string.Empty;
    [ObservableProperty] private string _nextBlockTitle = string.Empty;
    [ObservableProperty] private string _reportText = string.Empty;
    [ObservableProperty] private string _packTitle = "Основной пакет";
    [ObservableProperty] private PackTab? _selectedPack;

    /// <summary>Вкладки пакетов методик. Пакет собирается из загруженных блоков.</summary>
    public ObservableCollection<PackTab> Packs { get; } = new();

    /// <summary>Короткая подпись прогресса для шапки: «7 / 12».</summary>
    public string ProgressShort => $"{CompletedCount} / {BlockProgressService.TotalBlocks}";

    private void EnsurePacks()
    {
        if (Packs.Count > 0 || _blocks.Count == 0) return;
        var pack = new PackTab("Основной пакет", _blocks.Count);
        Packs.Add(pack);
        SelectedPack = pack;
    }

    partial void OnSelectedPackChanged(PackTab? value)
    {
        if (value is null) return;
        PackTitle = value.Name;
        StatusMessage = $"Пакет «{value.Name}»: методик — {value.Count}.";
    }

    /// <summary>
    /// Перехват показа результата: промежуточные баллы и уровни скрываются,
    /// остаётся только подтверждение сохранения. Итоговый отчёт помечается
    /// IsFinalReport заранее и проходит без очистки.
    /// </summary>
    partial void OnIsResultVisibleChanged(bool value)
    {
        if (!value) { IsFinalReport = false; return; }
        if (IsFinalReport) return;

        ResultScoresText = string.Empty;
        ResultLevel = string.Empty;
        ResultSummary = string.Empty;
        ReportText = string.Empty;

        SavedTitle = string.IsNullOrEmpty(CurrentBlockTitle) ? "Ответы сохранены" : CurrentBlockTitle;

        var next = Blocks.FirstOrDefault(b => b.Status != BlockStatus.Completed);
        HasNextBlock = next is not null;
        NextBlockTitle = next?.DisplayTitle ?? string.Empty;
        RemainingCount = Blocks.Count(b => b.Status != BlockStatus.Completed);

        SavedNote = RemainingCount > 0
            ? $"Блок пройден и сохранён. Осталось методик: {RemainingCount}. Баллы, уровни и подробные интерпретации откроются в итоговом отчёте после всех 12."
            : "Все 12 методик пройдены. Итоговый отчёт с баллами и подробными интерпретациями готов.";

        SavedMessage = "Ответы сохранены";
        SavedHint = SavedNote;

        ExportReportCommand.NotifyCanExecuteChanged();
    }

    partial void OnOverallProgressTextChanged(string value)
    {
        EnsurePacks();
        CompletedCount = _progressService.GetCompletedCount(_session, _blocks);
        AllCompleted = _blocks.Count > 0 && CompletedCount >= BlockProgressService.TotalBlocks;
        RemainingCount = Math.Max(0, BlockProgressService.TotalBlocks - CompletedCount);
        OnPropertyChanged(nameof(ProgressShort));
        OnPropertyChanged(nameof(IsFinalReportReady));
        OnPropertyChanged(nameof(FinalReportGateText));
        OpenFinalReportCommand.NotifyCanExecuteChanged();
        ExportReportCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsFinalReportChanged(bool value) => ExportReportCommand.NotifyCanExecuteChanged();

    partial void OnReportTextChanged(string value) => ExportReportCommand.NotifyCanExecuteChanged();

    /// <summary>Открыть следующую непройденную методику.</summary>
    [RelayCommand]
    private void GoNextBlock()
    {
        var next = Blocks.FirstOrDefault(b => b.Status != BlockStatus.Completed);
        if (next is not null) { SelectedBlock = next; return; }
        OpenFinalReport();
    }

    /// <summary>Итоговый отчёт. Доступен только после прохождения всех методик.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenFinalReport))]
    private void OpenFinalReport()
    {
        IsFinalReport = true;
        ShowFinalReport();
        ReportText = BuildFinalReportText();
        ResultSummary = ReportText;
        StatusMessage = "Итоговый отчёт сформирован. Его можно сохранить в текстовый файл.";
        ExportReportCommand.NotifyCanExecuteChanged();
    }

    private bool CanOpenFinalReport() => AllCompleted;
}

/// <summary>Вкладка пакета методик.</summary>
public class PackTab
{
    public PackTab(string name, int count) { Name = name; Count = count; }
    public string Name { get; }
    public int Count { get; }
    public string Header => $"{Name} · {Count}";
}
