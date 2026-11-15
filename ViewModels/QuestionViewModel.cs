using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ZharikovApp.Models;

namespace ZharikovApp.ViewModels;

/// <summary>
/// ViewModel одного вопроса в области контента.
/// Унифицирует все типы вопросов (YesNo/SingleChoice/Scale/ImageSingleChoice/
/// LeftRightChoice/ParameterChoice) для отображения через DataTemplate по Type.
/// </summary>
public partial class QuestionViewModel : ObservableObject
{
    private readonly SessionState _state;
    private bool _suppressSave;

    public QuestionViewModel(Question question, int number, SessionState state, Action onChanged)
    {
        Question = question ?? throw new ArgumentNullException(nameof(question));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        OnChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));

        Number = number;
        Options = new ObservableCollection<AnswerOptionViewModel>(
            question.Options.Select(o => new AnswerOptionViewModel(o, this)));

        // Восстановление выбранного значения из черновика сессии.
        var saved = _state.Answers.GetValueOrDefault(question.Id);
        if (!string.IsNullOrEmpty(saved))
        {
            _selectedOptionKey = saved;
            _scaleValue = TryParseScale(saved, question, out var v) ? v : DefaultScale(question);
        }
        else
        {
            _scaleValue = DefaultScale(question);
        }
    }

    /// <summary>Модель вопроса.</summary>
    public Question Question { get; }

    /// <summary>Порядковый номер вопроса (1-based).</summary>
    public int Number { get; }

    /// <summary>Действие «ответ изменился» (автосохранение + пересчёт кнопок).</summary>
    public Action OnChanged { get; }

    /// <summary>Варианты ответа (для шкалы — пустой список).</summary>
    public ObservableCollection<AnswerOptionViewModel> Options { get; }

    public QuestionType Type => Question.Type;
    public string Text => Question.Text;
    public string? ImageKey => Question.ImageKey;
    public bool IsScale => Question.Type == QuestionType.Scale;
    public bool IsChoice => !IsScale;

    public int ScaleMin => Question.Scale?.Min ?? 0;
    public int ScaleMax => Question.Scale?.Max ?? 10;

    [ObservableProperty]
    private string? _selectedOptionKey;

    [ObservableProperty]
    private int _scaleValue;

    /// <summary>Есть ли ответ на этот вопрос.</summary>
    public bool IsAnswered => Question.Type == QuestionType.Scale
        ? HasExplicitScaleAnswer
        : !string.IsNullOrEmpty(SelectedOptionKey);

    /// <summary>Шкала: проставлен ли ответ явно (в отличие от дефолтного 0).</summary>
    public bool HasExplicitScaleAnswer => _state.Answers.ContainsKey(Question.Id);

    /// <summary>Выбрать вариант ответа (вызывается из AnswerOptionViewModel).</summary>
    public void SelectOption(string key)
    {
        SelectedOptionKey = key;
    }

    partial void OnSelectedOptionKeyChanged(string? value)
    {
        if (_suppressSave || string.IsNullOrEmpty(value)) return;
        _state.Answers[Question.Id] = value;
        foreach (var opt in Options)
            opt.RefreshSelection();
        OnPropertyChanged(nameof(IsAnswered));
        MarkDirtyAndNotify();
    }

    partial void OnScaleValueChanged(int value)
    {
        if (_suppressSave) return;
        var min = ScaleMin;
        var max = ScaleMax;
        var clamped = Math.Clamp(value, min, max);
        if (clamped != value)
        {
            _suppressSave = true;
            ScaleValue = clamped;
            _suppressSave = false;
        }
        _state.Answers[Question.Id] = clamped.ToString(System.Globalization.CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(HasExplicitScaleAnswer));
        OnPropertyChanged(nameof(IsAnswered));
        MarkDirtyAndNotify();
    }

    private void MarkDirtyAndNotify()
    {
        _state.UpdatedAt = DateTimeOffset.Now;
        OnChanged();
    }

    private static bool TryParseScale(string s, Question q, out int value) =>
        int.TryParse(s, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out value) &&
        (q.Scale is null || q.Scale.Contains(value));

    private static int DefaultScale(Question q) => q.Scale?.Min ?? 0;
}

/// <summary>ViewModel варианта ответа вопроса.</summary>
public partial class AnswerOptionViewModel : ObservableObject
{
    private readonly QuestionViewModel _owner;

    public AnswerOptionViewModel(AnswerOption option, QuestionViewModel owner)
    {
        Option = option ?? throw new ArgumentNullException(nameof(option));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        SelectCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => _owner.SelectOption(Key));
    }

    public AnswerOption Option { get; }
    public string Key => Option.Key;
    public string Text => Option.Text;

    /// <summary>Команда выбора варианта (привязывается из XAML).</summary>
    public CommunityToolkit.Mvvm.Input.IRelayCommand SelectCommand { get; }

    /// <summary>Выделен ли этот вариант (радио-логика внутри вопроса).</summary>
    public bool IsSelected =>
        string.Equals(_owner.SelectedOptionKey, Key, StringComparison.OrdinalIgnoreCase);

    public void RefreshSelection() => OnPropertyChanged(nameof(IsSelected));
}
