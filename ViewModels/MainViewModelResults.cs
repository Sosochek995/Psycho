using ZharikovApp.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZharikovApp.Models;
using ZharikovApp.Services;

namespace ZharikovApp.ViewModels;

/// <summary>
/// Скрытие промежуточных результатов и выдача итогового отчёта.
/// Отдельный файл partial-класса: подсчёт, ключи и формулы не затрагиваются.
/// </summary>
public partial class MainViewModel
{
    /// <summary>Панель «Ответы сохранены» — вместо баллов после каждого блока.</summary>
    [ObservableProperty]
    private bool _isSavedVisible;

    /// <summary>Панель итогового отчёта — только после всех 12 блоков.</summary>
    [ObservableProperty]
    private bool _isReportVisible;

    [ObservableProperty]
    private string _savedMessage = string.Empty;

    [ObservableProperty]
    private string _savedHint = string.Empty;

    /// <summary>Все 12 методик завершены.</summary>
    public bool IsFinalReportReady =>
        _blocks.Count > 0 &&
        _progressService.GetCompletedCount(_session, _blocks) >= BlockProgressService.TotalBlocks;

    /// <summary>Подпись-подсказка о доступности итогового отчёта.</summary>
    public string FinalReportGateText
    {
        get
        {
            int done = _progressService.GetCompletedCount(_session, _blocks);
            return IsFinalReportReady
                ? "Все 12 блоков завершены — итоговый отчёт доступен"
                : $"Итоговый отчёт откроется после всех 12 блоков · готово {done} из {BlockProgressService.TotalBlocks}";
        }
    }

    /// <summary>
    /// Показывает только факт сохранения. Баллы, уровень и интерпретация
    /// намеренно не выводятся: они появятся в итоговом отчёте.
    /// </summary>
    private void ShowSavedPanel(TestBlock block, string message, string hint)
    {
        CurrentBlockTitle = $"{block.Order}. {block.Title}";
        SavedMessage = message;
        SavedHint = hint;

        ResultLevel = string.Empty;
        ResultScoresText = string.Empty;
        ResultSummary = string.Empty;

        IsWelcomeVisible = false;
        IsInstructionVisible = false;
        IsQuestionsVisible = false;
        IsResultVisible = false;
        IsReportVisible = false;
        IsSavedVisible = true;
    }

    /// <summary>Переход к следующему незавершённому блоку, либо к итоговому отчёту.</summary>
    [RelayCommand]
    private void GoToNextBlock()
    {
        var next = Blocks.FirstOrDefault(b => b.Status != BlockStatus.Completed);
        if (next is not null)
        {
            SelectedBlock = next;
            return;
        }

        if (IsFinalReportReady) ShowFinalReport();
    }

    /// <summary>Сохранение итогового отчёта в текстовый файл с BOM — кириллица не ломается.</summary>
    [RelayCommand(CanExecute = nameof(IsFinalReportReady))]
    private void ExportReport()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Сохранить итоговый отчёт",
                FileName = $"Итоговый отчёт {DateTime.Now:yyyy-MM-dd}.txt",
                Filter = "Текстовый файл (*.txt)|*.txt",
                DefaultExt = ".txt",
                AddExtension = true
            };

            if (dialog.ShowDialog() != true) return;

            File.WriteAllText(dialog.FileName, BuildFinalReportText(), new UTF8Encoding(true));
            StatusMessage = "Отчёт сохранён: " + dialog.FileName;
        }
        catch (Exception ex)
        {
            StatusMessage = "Не удалось сохранить отчёт: " + ex.Message;
        }
    }

    /// <summary>Полный текст итогового отчёта — единственный источник для экрана и файла.</summary>
    private string BuildFinalReportText()
    {
        var lines = new List<string>
        {
            "ИТОГОВЫЙ ОТЧЁТ ПО МЕТОДИКАМ ЖАРИКОВА И КРУШЕЛЬНИЦКОГО",
            OverallProgressText,
            $"Дата: {DateTime.Now:dd.MM.yyyy HH:mm}",
            new string('═', 64),
            string.Empty
        };

        foreach (var block in _blocks)
        {
            var item = Blocks.FirstOrDefault(b => b.Id == block.Id);
            lines.Add($"{block.Order}. {block.Title} — {(item is not null ? item.StatusText : "Не начат")}");

            var result = _session.GetResult(block.Id);
            if (result is { IsCompleted: true })
            {
                var scores = FormatScores(result);
                if (!string.IsNullOrEmpty(result.Level))
                    lines.Add($"    Результат: {(scores.Length > 0 ? scores + " — " : "")}{result.Level}");
                if (!string.IsNullOrEmpty(result.Summary))
                {
                    foreach (var row in result.Summary.Split('\n'))
                        lines.Add("    " + row.TrimEnd('\r'));
                }
            }
            else
            {
                lines.Add("    Блок не завершён.");
            }

            lines.Add(string.Empty);
        }

        lines.Add(new string('═', 64));
        lines.Add("Промежуточные результаты во время прохождения не показывались намеренно:");
        lines.Add("это снижает подгонку ответов под ожидаемый результат.");
        return string.Join(Environment.NewLine, lines);
    }
}
