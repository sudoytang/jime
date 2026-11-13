using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace Jime;

public partial class MainWindow : Window
{
    private const int MaxPageSize = 9;
    private const double MaxCandidatesHeight = 260;
    private const string SegmentSeparator = "ʼ";
    private static WindowPosition? _rememberedPosition = PositionSettings.Load();
    private string _confirmed = "";
    private int _confirmedCaret;
    private string _rawInput = "";
    private int _rawCaret;
    private IReadOnlyList<string> _romanSegments = Array.Empty<string>();
    private readonly FlowDocument _viewDocument;
    private readonly Paragraph _viewParagraph;
    private readonly Run _prefixRun = new();
    private readonly Run _rawRun = new() { TextDecorations = TextDecorations.Underline };
    private readonly Run _suffixRun = new();
    private readonly List<CandidateItem[]> _pages = new();
    private int _page;
    private int _conversionVersion;
    private readonly MozcConverter _mozc = new();

    public event Action<string>? CommitRequested;
    public event Action? CancelRequested;

    public MainWindow()
    {
        InitializeComponent();
        _viewDocument = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = InputView.FontFamily,
            FontSize = InputView.FontSize,
            Foreground = System.Windows.Media.Brushes.White
        };
        _viewParagraph = new Paragraph { Margin = new Thickness(0), TextIndent = 0 };
        _viewParagraph.Inlines.Add(_prefixRun);
        _viewParagraph.Inlines.Add(_rawRun);
        _viewParagraph.Inlines.Add(_suffixRun);
        _viewDocument.Blocks.Add(_viewParagraph);
        InputView.Document = _viewDocument;
        Loaded += (_, _) => UpdateComposition();
        InputView.Focus();
        UpdateComposition();
    }

    public void BeginInput()
    {
        _confirmed = "";
        _confirmedCaret = 0;
        _rawInput = "";
        _rawCaret = 0;
        _romanSegments = Array.Empty<string>();
        _conversionVersion++;
        ReadingText.Text = "输入罗马字，例如：ohayou";
        Candidates.Visibility = Visibility.Collapsed;
        UpdateComposition();
        ShowCentered();
        Activate();
        InputView.Focus();
    }

    private void ShowCentered()
    {
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        UpdateLayout();
        var width = (ActualWidth > 0 ? ActualWidth : Width) * dpi.DpiScaleX;
        var height = (ActualHeight > 0 ? ActualHeight : MinHeight) * dpi.DpiScaleY;
        if (_rememberedPosition is { } remembered && IsOnScreen(remembered, width, height, dpi))
        {
            Left = remembered.Left;
            Top = remembered.Top;
        }
        else
        {
            var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var area = screen.WorkingArea;
            Left = (area.Left + (area.Width - width) / 2) / dpi.DpiScaleX;
            Top = (area.Top + (area.Height - height) / 2) / dpi.DpiScaleY;
        }
        Show();
    }

    private static bool IsOnScreen(WindowPosition position, double width, double height,
        System.Windows.DpiScale dpi)
    {
        var screen = System.Windows.Forms.SystemInformation.VirtualScreen;
        var left = position.Left * dpi.DpiScaleX;
        var top = position.Top * dpi.DpiScaleY;
        return left + width >= screen.Left && left <= screen.Right
            && top + height >= screen.Top && top <= screen.Bottom;
    }

    internal bool TryInsertCharacter(char character)
    {
        if (char.IsControl(character)) return false;
        if (character == ' ' && Candidates.Visibility == Visibility.Visible) return true;
        _rawInput = _rawInput.Insert(_rawCaret, character.ToString());
        _rawCaret++;
        ApplyRawEdit();
        return true;
    }

    private void ApplyRawEdit()
    {
        var version = ++_conversionVersion;
        _page = 0;
        Candidates.Visibility = _rawInput.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        ReadingText.Text = _rawInput.Length == 0
            ? _confirmed.Length == 0 ? "输入罗马字，例如：ohayou" : "继续输入，或按 Enter 完成"
            : $"读音：{RomajiConverter.ToHiragana(_rawInput)}";
        _romanSegments = _rawInput.Length == 0 ? Array.Empty<string>() : [_rawInput];
        UpdateComposition();
        if (_rawInput.Length > 0) _ = ConvertAsync(_rawInput, version);
    }

    private async Task ConvertAsync(string romaji, int version)
    {
        try
        {
            var conversion = await _mozc.GetConversionAsync(romaji);
            if (version != _conversionVersion || _rawInput != romaji) return;
            _romanSegments = RomajiConverter.SplitByKanaSegments(romaji, conversion.SegmentReadings);
            BuildPages(conversion);
            ShowPage();
            UpdateComposition();
        }
        catch (Exception ex)
        {
            if (version == _conversionVersion) ReadingText.Text = $"Mozc：{ex.Message}";
        }
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Candidates.Visibility == Visibility.Visible && TrySelectNumber(key))
        {
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt && key is Key.OemMinus or Key.OemPlus)
        {
            InsertText(key == Key.OemMinus ? "-" : "=");
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift)
            && key is Key.OemMinus or Key.OemPlus)
        {
            InsertText(key == Key.OemMinus ? "_" : "+");
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            && key is Key.A or Key.C or Key.X or Key.V or Key.Z or Key.Y)
        {
            e.Handled = true;
            return;
        }

        switch (key)
        {
            case Key.Enter when Keyboard.Modifiers == ModifierKeys.Alt:
                ConfirmText(RomajiConverter.ToHiragana(RawInputText));
                e.Handled = true;
                break;
            case Key.Enter when Keyboard.Modifiers == ModifierKeys.Control:
                ConfirmText(RomajiConverter.ToKatakana(RomajiConverter.ToHiragana(RawInputText)));
                e.Handled = true;
                break;
            case Key.Enter:
                if (Candidates.Visibility == Visibility.Visible) ConfirmText(RawInputText);
                else Commit();
                e.Handled = true;
                break;
            case Key.Escape:
                CancelRequested?.Invoke();
                e.Handled = true;
                break;
            case Key.Back:
                if (_rawInput.Length > 0)
                {
                    if (_rawCaret > 0)
                    {
                        var start = PreviousTextElementStart(_rawInput, _rawCaret);
                        _rawInput = _rawInput.Remove(start, _rawCaret - start);
                        _rawCaret = start;
                        ApplyRawEdit();
                    }
                }
                else if (_confirmedCaret > 0)
                {
                    var start = PreviousTextElementStart(_confirmed, _confirmedCaret);
                    _confirmed = _confirmed.Remove(start, _confirmedCaret - start);
                    _confirmedCaret = start;
                    RefreshConfirmedEdit();
                }
                e.Handled = true;
                break;
            case Key.Delete:
                if (_rawInput.Length > 0)
                {
                    if (_rawCaret < _rawInput.Length)
                    {
                        var end = NextTextElementEnd(_rawInput, _rawCaret);
                        _rawInput = _rawInput.Remove(_rawCaret, end - _rawCaret);
                        ApplyRawEdit();
                    }
                }
                else if (_confirmedCaret < _confirmed.Length)
                {
                    var end = NextTextElementEnd(_confirmed, _confirmedCaret);
                    _confirmed = _confirmed.Remove(_confirmedCaret, end - _confirmedCaret);
                    RefreshConfirmedEdit();
                }
                e.Handled = true;
                break;
            case Key.Left:
                if (_rawInput.Length > 0)
                {
                    _rawCaret = PreviousTextElementStart(_rawInput, _rawCaret);
                }
                else
                {
                    _confirmedCaret = PreviousTextElementStart(_confirmed, _confirmedCaret);
                }
                SyncView();
                e.Handled = true;
                break;
            case Key.Right:
                if (_rawInput.Length > 0)
                {
                    _rawCaret = NextTextElementEnd(_rawInput, _rawCaret);
                }
                else
                {
                    _confirmedCaret = NextTextElementEnd(_confirmed, _confirmedCaret);
                }
                SyncView();
                e.Handled = true;
                break;
            case Key.Home:
                if (_rawInput.Length > 0) _rawCaret = 0;
                else _confirmedCaret = 0;
                SyncView();
                e.Handled = true;
                break;
            case Key.End:
                if (_rawInput.Length > 0) _rawCaret = _rawInput.Length;
                else _confirmedCaret = _confirmed.Length;
                SyncView();
                e.Handled = true;
                break;
            case Key.Space when Candidates.Visibility == Visibility.Visible:
                ConfirmCandidate();
                e.Handled = true;
                break;
            case Key.OemMinus when Candidates.Visibility == Visibility.Visible
                && Keyboard.Modifiers == ModifierKeys.None:
                if (_page == 0) InsertText("-");
                else ChangePage(-1);
                e.Handled = true;
                break;
            case Key.OemPlus when Candidates.Visibility == Visibility.Visible
                && Keyboard.Modifiers == ModifierKeys.None:
                if (_pages.Count > 0 && _page < _pages.Count - 1) ChangePage(1);
                e.Handled = true;
                break;
            case Key.PageUp when Candidates.Visibility == Visibility.Visible:
                ChangePage(-1);
                e.Handled = true;
                break;
            case Key.PageDown when Candidates.Visibility == Visibility.Visible:
                ChangePage(1);
                e.Handled = true;
                break;
            case Key.Down:
                if (Candidates.SelectedIndex < Candidates.Items.Count - 1) Candidates.SelectedIndex++;
                e.Handled = true;
                break;
            case Key.Up:
                if (Candidates.SelectedIndex > 0) Candidates.SelectedIndex--;
                e.Handled = true;
                break;
            case Key.F6:
                ConfirmText(RomajiConverter.ToHiragana(RawInputText));
                e.Handled = true;
                break;
            case Key.F7:
                ConfirmText(RomajiConverter.ToKatakana(RomajiConverter.ToHiragana(RawInputText)));
                e.Handled = true;
                break;
        }
    }

    private void InsertText(string text)
    {
        _rawInput = _rawInput.Insert(_rawCaret, text);
        _rawCaret += text.Length;
        ApplyRawEdit();
        InputView.Focus();
    }

    private bool TrySelectNumber(Key key)
    {
        var number = key switch
        {
            >= Key.D1 and <= Key.D9 => key - Key.D1 + 1,
            >= Key.NumPad1 and <= Key.NumPad9 => key - Key.NumPad1 + 1,
            _ => 0
        };
        if (number == 0 || number > Candidates.Items.Count) return false;
        Candidates.SelectedIndex = number - 1;
        ConfirmCandidate();
        return true;
    }

    private void BuildPages(MozcConversion conversion)
    {
        _pages.Clear();
        var page = new List<CandidateItem>();
        var height = 0.0;
        var width = Candidates.ActualWidth > 0 ? Candidates.ActualWidth - 20 : 400;
        for (var index = 0; index < conversion.Candidates.Count; index++)
        {
            var value = conversion.Candidates[index];
            var display = index == 0 && conversion.SegmentValues.Count > 1
                ? string.Join(' ', conversion.SegmentValues)
                : value;
            var item = new CandidateItem($"{page.Count + 1}  {display}", value);
            var textBlock = new TextBlock
            {
                Text = item.Display,
                FontFamily = Candidates.FontFamily,
                FontSize = Candidates.FontSize,
                TextWrapping = TextWrapping.Wrap
            };
            textBlock.Measure(new System.Windows.Size(width, double.PositiveInfinity));
            var itemHeight = textBlock.DesiredSize.Height + 10;
            if (page.Count > 0 && (page.Count == MaxPageSize || height + itemHeight > MaxCandidatesHeight))
            {
                _pages.Add(page.ToArray());
                page.Clear();
                height = 0;
                item = new CandidateItem($"{page.Count + 1}  {display}", value);
                textBlock.Text = item.Display;
                textBlock.Measure(new System.Windows.Size(width, double.PositiveInfinity));
                itemHeight = textBlock.DesiredSize.Height + 10;
            }
            page.Add(item);
            height += itemHeight;
        }
        if (page.Count > 0) _pages.Add(page.ToArray());
    }

    private void ShowPage()
    {
        var items = _pages.Count == 0 ? Array.Empty<CandidateItem>() : _pages[_page];
        Candidates.ItemsSource = items;
        Candidates.SelectedIndex = items.Length == 0 ? -1 : 0;
    }

    private void ChangePage(int delta)
    {
        if (_pages.Count == 0) return;
        _page = (_page + delta + _pages.Count) % _pages.Count;
        ShowPage();
    }

    private void ConfirmCandidate()
    {
        if (Candidates.SelectedItem is not CandidateItem item) return;
        ConfirmText(item.Value);
    }

    private void ConfirmText(string text)
    {
        _confirmed = _confirmed.Insert(_confirmedCaret, text);
        _confirmedCaret += text.Length;
        _rawInput = "";
        _rawCaret = 0;
        _romanSegments = Array.Empty<string>();
        _conversionVersion++;
        _page = 0;
        Candidates.Visibility = Visibility.Collapsed;
        ReadingText.Text = _confirmed.Length == 0 ? "输入罗马字，例如：ohayou" : "继续输入，或按 Enter 完成";
        UpdateComposition();
        InputView.Focus();
    }

    private void Commit()
    {
        if (_confirmed.Length > 0) CommitRequested?.Invoke(_confirmed);
        else CancelRequested?.Invoke();
    }

    private string RawInputText => _rawInput;

    private void RefreshConfirmedEdit()
    {
        _conversionVersion++;
        _page = 0;
        _romanSegments = Array.Empty<string>();
        Candidates.Visibility = Visibility.Collapsed;
        ReadingText.Text = _confirmed.Length == 0 ? "输入罗马字，例如：ohayou" : "继续输入，或按 Enter 完成";
        UpdateComposition();
    }

    private void UpdateComposition()
    {
        if (_confirmed.Length == 0 && _rawInput.Length == 0)
        {
            _prefixRun.Foreground = System.Windows.Media.Brushes.Gray;
            SetRunText(_prefixRun, "罗马字");
            SetRunText(_rawRun, "");
            SetRunText(_suffixRun, "");
            InputView.CaretPosition = _prefixRun.ContentStart;
            return;
        }

        _prefixRun.Foreground = System.Windows.Media.Brushes.White;
        if (_rawInput.Length > 0)
        {
            SetRunText(_prefixRun, _confirmed[.._confirmedCaret]);
            SetRunText(_rawRun, BuildDisplayText());
            SetRunText(_suffixRun, _confirmed[_confirmedCaret..]);
        }
        else
        {
            SetRunText(_prefixRun, _confirmed);
            SetRunText(_rawRun, "");
            SetRunText(_suffixRun, "");
        }
        UpdateCaretPosition();
    }

    private static void SetRunText(Run run, string text)
    {
        if (run.Text != text) run.Text = text;
    }

    private void UpdateCaretPosition()
    {
        if (_confirmed.Length == 0 && _rawInput.Length == 0)
        {
            InputView.CaretPosition = _prefixRun.ContentStart;
            return;
        }
        if (_rawInput.Length > 0)
        {
            var displayCaret = GetDisplayCaretIndex(_rawCaret);
            InputView.CaretPosition =
                _rawRun.ContentStart.GetPositionAtOffset(displayCaret, LogicalDirection.Forward)
                ?? _rawRun.ContentEnd;
            return;
        }
        var caret = _prefixRun.ContentStart.GetPositionAtOffset(
            Math.Clamp(_confirmedCaret, 0, _confirmed.Length), LogicalDirection.Forward);
        InputView.CaretPosition = caret ?? _prefixRun.ContentEnd;
    }

    private void SyncView() => UpdateCaretPosition();

    private string BuildDisplayText()
    {
        if (_romanSegments.Count > 1 && string.Concat(_romanSegments) == _rawInput)
            return string.Join(SegmentSeparator, _romanSegments);
        return _rawInput;
    }

    private int GetDisplayCaretIndex(int caret)
    {
        var remaining = Math.Clamp(caret, 0, _rawInput.Length);
        if (_romanSegments.Count <= 1 || string.Concat(_romanSegments) != _rawInput)
            return remaining;
        var display = 0;
        for (var index = 0; index < _romanSegments.Count && remaining > 0; index++)
        {
            var segment = _romanSegments[index];
            var take = Math.Min(remaining, segment.Length);
            display += take;
            remaining -= take;
            if (remaining > 0 && index < _romanSegments.Count - 1) display++;
        }
        return display;
    }

    private static int PreviousTextElementStart(string text, int position)
    {
        position = Math.Clamp(position, 0, text.Length);
        if (position == 0) return 0;
        var starts = System.Globalization.StringInfo.ParseCombiningCharacters(text);
        for (var index = starts.Length - 1; index >= 0; index--)
        {
            if (starts[index] < position) return starts[index];
        }
        return 0;
    }

    private static int NextTextElementEnd(string text, int position)
    {
        position = Math.Clamp(position, 0, text.Length);
        if (position == text.Length) return text.Length;
        var starts = System.Globalization.StringInfo.ParseCombiningCharacters(text);
        foreach (var start in starts)
        {
            if (start > position) return start;
        }
        return text.Length;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Candidates.SelectedItem != null) Candidates.ScrollIntoView(Candidates.SelectedItem);
    }

    private void OnCandidateDoubleClick(object sender, MouseButtonEventArgs e) => ConfirmCandidate();

    private void OnCompositionClick(object sender, MouseButtonEventArgs e)
    {
        InputView.Focus();
        e.Handled = true;
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is System.Windows.Controls.RichTextBox) return;
        DragMove();
        _rememberedPosition = new WindowPosition(Left, Top);
        PositionSettings.Save(_rememberedPosition);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}

public sealed record CandidateItem(string Display, string Value)
{
    public override string ToString() => Display;
}
