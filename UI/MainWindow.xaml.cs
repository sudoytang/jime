using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Jime.Core;
using Jime.Platform.Windows;
using Jime.Settings;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using Size = Avalonia.Size;

namespace Jime.UI;

public partial class MainWindow : Window
{
    private const int MaxPageSize = 9;
    private const double MaxCandidatesHeight = 260;
    private static WindowPosition? _rememberedPosition = PositionSettings.Load();
    private string _confirmed = "";
    private int _confirmedCaret;
    private string _rawInput = "";
    private int _rawCaret;
    private MozcConversion? _currentConversion;
    private readonly List<CandidateItem[]> _pages = new();
    private int _page;
    private int _conversionVersion;
    private readonly MozcConverter _mozc = new();

    public event Action<string>? CommitRequested;
    public event Action? CancelRequested;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateComposition();
        UpdateComposition();
    }

    public void BeginInput()
    {
        _confirmed = "";
        _confirmedCaret = 0;
        _rawInput = "";
        _rawCaret = 0;
        _currentConversion = null;
        _conversionVersion++;
        ReadingText.Text = "输入罗马字，例如：ohayou";
        Candidates.IsVisible = false;
        UpdateComposition();
        ShowCentered();
        Activate();
        Focus();
    }

    private void ShowCentered()
    {
        UpdateLayout();
        NativeMethods.GetCursorPos(out var cursor);
        var screen = Screens.ScreenFromPoint(new PixelPoint(cursor.X, cursor.Y)) ?? Screens.Primary;
        if (screen == null)
        {
            Show();
            return;
        }
        var scaling = screen.Scaling;
        var width = (Bounds.Width > 0 ? Bounds.Width : Width) * scaling;
        var height = (Bounds.Height > 0 ? Bounds.Height : MinHeight) * scaling;
        if (_rememberedPosition is { } remembered && IsOnScreen(remembered, width, height))
        {
            Position = new PixelPoint((int)remembered.Left, (int)remembered.Top);
        }
        else
        {
            var area = screen.WorkingArea;
            Position = new PixelPoint(
                (int)(area.X + (area.Width - width) / 2),
                (int)(area.Y + (area.Height - height) / 2));
        }
        Show();
    }

    private bool IsOnScreen(WindowPosition position, double width, double height)
    {
        var left = position.Left;
        var top = position.Top;
        return Screens.All.Any(screen =>
            left + width >= screen.Bounds.X && left <= screen.Bounds.Right
            && top + height >= screen.Bounds.Y && top <= screen.Bounds.Bottom);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (string.IsNullOrEmpty(e.Text)) return;
        foreach (var character in e.Text) TryInsertCharacter(character);
        e.Handled = true;
    }

    internal bool TryInsertCharacter(char character)
    {
        if (char.IsControl(character)) return false;
        if (character == ' ' && Candidates.IsVisible) return true;
        _rawInput = _rawInput.Insert(_rawCaret, character.ToString());
        _rawCaret++;
        ApplyRawEdit();
        return true;
    }

    private void ApplyRawEdit()
    {
        var version = ++_conversionVersion;
        _page = 0;
        Candidates.IsVisible = _rawInput.Length != 0;
        ReadingText.Text = _rawInput.Length == 0
            ? _confirmed.Length == 0 ? "输入罗马字，例如：ohayou" : "继续输入，或按 Enter 完成"
            : $"输入：{_rawInput}";
        _currentConversion = null;
        UpdateComposition();
        if (_rawInput.Length > 0) _ = ConvertAsync(_rawInput, version);
    }

    private async Task ConvertAsync(string romaji, int version)
    {
        try
        {
            var conversion = await _mozc.GetConversionAsync(romaji);
            if (version != _conversionVersion || _rawInput != romaji) return;
            _currentConversion = conversion;
            ReadingText.Text = $"读音：{conversion.Reading}";
            BuildPages(conversion);
            ShowPage();
            UpdateComposition();
        }
        catch (Exception ex)
        {
            if (version == _conversionVersion) ReadingText.Text = $"Mozc：{ex.Message}";
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var key = e.Key;
        if (Candidates.IsVisible && TrySelectNumber(key))
        {
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Alt && key is Key.OemMinus or Key.OemPlus)
        {
            InsertText(key == Key.OemMinus ? "-" : "=");
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers == (KeyModifiers.Alt | KeyModifiers.Shift)
            && key is Key.OemMinus or Key.OemPlus)
        {
            InsertText(key == Key.OemMinus ? "_" : "+");
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && key is Key.A or Key.C or Key.X or Key.V or Key.Z or Key.Y)
        {
            e.Handled = true;
            return;
        }

        switch (key)
        {
            case Key.Enter when e.KeyModifiers == KeyModifiers.Alt:
                ConfirmReading();
                e.Handled = true;
                break;
            case Key.Enter when e.KeyModifiers == KeyModifiers.Control:
                ConfirmKatakana();
                e.Handled = true;
                break;
            case Key.Enter:
                if (Candidates.IsVisible) ConfirmText(_rawInput);
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
                if (_rawInput.Length > 0) _rawCaret = PreviousTextElementStart(_rawInput, _rawCaret);
                else _confirmedCaret = PreviousTextElementStart(_confirmed, _confirmedCaret);
                SyncView();
                e.Handled = true;
                break;
            case Key.Right:
                if (_rawInput.Length > 0) _rawCaret = NextTextElementEnd(_rawInput, _rawCaret);
                else _confirmedCaret = NextTextElementEnd(_confirmed, _confirmedCaret);
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
            case Key.Space when Candidates.IsVisible:
                ConfirmCandidate();
                e.Handled = true;
                break;
            case Key.OemMinus when Candidates.IsVisible && e.KeyModifiers == KeyModifiers.None:
                if (_page == 0) InsertText("-");
                else ChangePage(-1);
                e.Handled = true;
                break;
            case Key.OemPlus when Candidates.IsVisible && e.KeyModifiers == KeyModifiers.None:
                if (_pages.Count > 0 && _page < _pages.Count - 1) ChangePage(1);
                e.Handled = true;
                break;
            case Key.PageUp when Candidates.IsVisible:
                ChangePage(-1);
                e.Handled = true;
                break;
            case Key.PageDown when Candidates.IsVisible:
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
                ConfirmReading();
                e.Handled = true;
                break;
            case Key.F7:
                ConfirmKatakana();
                e.Handled = true;
                break;
        }
    }

    private void InsertText(string text)
    {
        _rawInput = _rawInput.Insert(_rawCaret, text);
        _rawCaret += text.Length;
        ApplyRawEdit();
        Focus();
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
        var width = Candidates.Bounds.Width > 0 ? Candidates.Bounds.Width - 20 : 400;
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
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            textBlock.Measure(new Size(width, double.PositiveInfinity));
            var itemHeight = textBlock.DesiredSize.Height + 10;
            if (page.Count > 0 && (page.Count == MaxPageSize || height + itemHeight > MaxCandidatesHeight))
            {
                _pages.Add(page.ToArray());
                page.Clear();
                height = 0;
                item = new CandidateItem($"{page.Count + 1}  {display}", value);
                textBlock.Text = item.Display;
                textBlock.Measure(new Size(width, double.PositiveInfinity));
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

    private void ConfirmReading()
    {
        if (_rawInput.Length > 0 && _currentConversion?.Reading.Length > 0)
            ConfirmText(_currentConversion.Reading);
    }

    private void ConfirmKatakana()
    {
        if (_rawInput.Length > 0 && _currentConversion?.Katakana.Length > 0)
            ConfirmText(_currentConversion.Katakana);
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
        _currentConversion = null;
        _conversionVersion++;
        _page = 0;
        Candidates.IsVisible = false;
        ReadingText.Text = _confirmed.Length == 0 ? "输入罗马字，例如：ohayou" : "继续输入，或按 Enter 完成";
        UpdateComposition();
        Focus();
    }

    private void Commit()
    {
        if (_confirmed.Length > 0) CommitRequested?.Invoke(_confirmed);
        else CancelRequested?.Invoke();
    }

    private void RefreshConfirmedEdit()
    {
        _conversionVersion++;
        _page = 0;
        _currentConversion = null;
        Candidates.IsVisible = false;
        ReadingText.Text = _confirmed.Length == 0 ? "输入罗马字，例如：ohayou" : "继续输入，或按 Enter 完成";
        UpdateComposition();
    }

    private void UpdateComposition()
    {
        if (_confirmed.Length == 0 && _rawInput.Length == 0)
        {
            InputView.Prefix = "";
            InputView.Raw = "";
            InputView.Suffix = "";
            InputView.CaretOffset = 0;
            return;
        }

        if (_rawInput.Length > 0)
        {
            InputView.Prefix = _confirmed[.._confirmedCaret];
            InputView.Raw = _rawInput;
            InputView.Suffix = _confirmed[_confirmedCaret..];
            InputView.CaretOffset = InputView.Prefix.Length + _rawCaret;
        }
        else
        {
            InputView.Prefix = _confirmed;
            InputView.Raw = "";
            InputView.Suffix = "";
            InputView.CaretOffset = _confirmedCaret;
        }
    }

    private void SyncView() => UpdateComposition();

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

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Candidates.SelectedItem != null) Candidates.ScrollIntoView(Candidates.SelectedItem);
    }

    private void OnCandidateDoubleClick(object? sender, TappedEventArgs e) => ConfirmCandidate();

    private void OnCompositionClick(object? sender, PointerPressedEventArgs e)
    {
        Focus();
        e.Handled = true;
    }

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Source is CompositionView) return;
        BeginMoveDrag(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        _rememberedPosition = new WindowPosition(Position.X, Position.Y);
        PositionSettings.Save(_rememberedPosition);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}

public sealed record CandidateItem(string Display, string Value)
{
    public override string ToString() => Display;
}
