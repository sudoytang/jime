using System.Globalization;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Brushes = Avalonia.Media.Brushes;
using FlowDirection = Avalonia.Media.FlowDirection;
using Pen = Avalonia.Media.Pen;
using Point = Avalonia.Point;

namespace Jime;

public sealed class CompositionView : TemplatedControl
{
    public static readonly StyledProperty<string> PrefixProperty =
        AvaloniaProperty.Register<CompositionView, string>(nameof(Prefix), "");

    public static readonly StyledProperty<string> RawProperty =
        AvaloniaProperty.Register<CompositionView, string>(nameof(Raw), "");

    public static readonly StyledProperty<string> SuffixProperty =
        AvaloniaProperty.Register<CompositionView, string>(nameof(Suffix), "");

    public static readonly StyledProperty<int> CaretOffsetProperty =
        AvaloniaProperty.Register<CompositionView, int>(nameof(CaretOffset));

    public string Prefix
    {
        get => GetValue(PrefixProperty);
        set => SetValue(PrefixProperty, value);
    }

    public string Raw
    {
        get => GetValue(RawProperty);
        set => SetValue(RawProperty, value);
    }

    public string Suffix
    {
        get => GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    public int CaretOffset
    {
        get => GetValue(CaretOffsetProperty);
        set => SetValue(CaretOffsetProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PrefixProperty || change.Property == RawProperty
            || change.Property == SuffixProperty || change.Property == CaretOffsetProperty
            || change.Property == FontSizeProperty || change.Property == FontFamilyProperty
            || change.Property == ForegroundProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var foreground = Foreground ?? Brushes.White;
        if (Prefix.Length == 0 && Raw.Length == 0 && Suffix.Length == 0)
        {
            var placeholder = CreateText("罗马字", Brushes.Gray);
            context.DrawText(placeholder, new Point(0, (Bounds.Height - placeholder.Height) / 2));
            DrawCaret(context, 0, placeholder.Height);
            return;
        }

        var prefix = CreateText(Prefix, foreground);
        var raw = CreateText(Raw, foreground);
        var suffix = CreateText(Suffix, foreground);
        var height = Math.Max(prefix.Height, Math.Max(raw.Height, suffix.Height));
        var y = (Bounds.Height - height) / 2;
        context.DrawText(prefix, new Point(0, y));
        context.DrawText(raw, new Point(prefix.Width, y));
        context.DrawText(suffix, new Point(prefix.Width + raw.Width, y));

        if (Raw.Length > 0)
        {
            var underline = new Pen(foreground, 1);
            context.DrawLine(underline, new Point(prefix.Width, y + raw.Height + 1),
                new Point(prefix.Width + raw.Width, y + raw.Height + 1));
        }
        var caretX = CreateText((Prefix + Raw + Suffix)[..Math.Clamp(CaretOffset, 0, Prefix.Length + Raw.Length + Suffix.Length)], foreground).Width;
        DrawCaret(context, caretX, height);
    }

    private FormattedText CreateText(string text, IBrush foreground) => new(
        text.Length == 0 ? " " : text,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        new Typeface(FontFamily),
        FontSize,
        foreground);

    private void DrawCaret(DrawingContext context, double x, double textHeight)
    {
        var height = Math.Max(18, textHeight);
        var y = (Bounds.Height - height) / 2;
        context.DrawLine(new Pen(Foreground ?? Brushes.White, 1), new Point(x, y), new Point(x, y + height));
    }
}
