using System.Windows;
using System.Windows.Media;
using Moji.Core;

namespace Moji.App;

public sealed class NativeIcon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(NativeIcon), new FrameworkPropertyMetadata("Document", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(NativeIcon), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Brush? Foreground { get => (Brush?)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    private static readonly Dictionary<string, Geometry> Paths = new()
    {
        ["Document"] = Geometry.Parse("M5 3H14L19 8V21H5Z M14 3V8H19 M8 12H16 M8 16H16"),
        ["Book"] = Geometry.Parse("M5 3H19V20H6C2 20 2 16 6 16H19 M6 3C2 3 3 6 3 8V18 M7 3V16"),
        ["Prompt"] = Geometry.Parse("M12 2C13 9 15 11 22 12C15 13 13 15 12 22C11 15 9 13 2 12C9 11 11 9 12 2Z"),
        ["Lock"] = Geometry.Parse("M7 10V7C7 0 17 0 17 7V10 M5 10H19V21H5Z M12 14V17"),
        ["Dashboard"] = Geometry.Parse("M3 3H9V9H3Z M15 3H21V9H15Z M3 15H9V21H3Z M15 15H21V21H15Z"),
        ["Star"] = Geometry.Parse("M12 2L15 8L22 9L17 14L18 21L12 18L6 21L7 14L2 9L9 8Z"),
        ["Archive"] = Geometry.Parse("M3 3H21V8H3Z M5 8V21H19V8 M9 12H15"),
        ["Trash"] = Geometry.Parse("M3 6H21 M9 6V3H15V6 M5 6L6 21H18L19 6 M10 10V17 M14 10V17"),
        ["Search"] = Geometry.Parse("M10 3C0 3 0 17 10 17C20 17 20 3 10 3 M16 16L22 22"),
        ["Plus"] = Geometry.Parse("M12 4V20 M4 12H20"),
        ["Arrow"] = Geometry.Parse("M3 12H21 M15 6L21 12L15 18"),
        ["External"] = Geometry.Parse("M14 3H21V10 M21 3L10 14 M10 5H4V21H20V14"),
        ["Cloud"] = Geometry.Parse("M6 19C0 19 0 10 6 10C5 0 20 0 19 10C26 10 26 19 19 19Z"),
        ["User"] = Geometry.Parse("M12 2C-1 2 -1 22 12 22C25 22 25 2 12 2 M12 6C6 6 6 14 12 14C18 14 18 6 12 6 M5 19C6 13 18 13 19 19"),
        ["Settings"] = Geometry.Parse("M10 2H14L15 5L18 6L21 5L23 9L20 12L21 15L23 17L20 21L17 20L14 22H10L9 19L6 18L3 19L1 15L4 12L3 9L1 7L4 3L7 4Z M12 8C6 8 6 16 12 16C18 16 18 8 12 8")
    };
    protected override void OnRender(DrawingContext dc)
    {
        var brush = Foreground ?? Ui.Resource("Ink");
        dc.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        if (Kind == "More") { foreach (var x in new[] { 5d, 12d, 19d }) dc.DrawEllipse(brush, null, new Point(x, 12), 1.2, 1.2); }
        else { var pen = new Pen(brush, 1.65) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; dc.DrawGeometry(null, pen, Paths.GetValueOrDefault(Kind, Paths["Document"])); }
        dc.Pop();
    }
    public static string ForMode(NoteMode mode) => mode switch { NoteMode.Prompt => "Prompt", NoteMode.Password => "Lock", _ => "Document" };
}

public sealed class NotebookCover : FrameworkElement
{
    public NoteMode Mode { get; set; }
    public bool Dark { get; set; }
    private static Color C(string value) => (Color)ColorConverter.ConvertFromString(value);
    private static Brush Gradient(params string[] colors)
    { var brush = new LinearGradientBrush { StartPoint = new Point(0, .1), EndPoint = new Point(1, .9) }; for (var i = 0; i < colors.Length; i++) brush.GradientStops.Add(new(C(colors[i]), i / (double)(colors.Length - 1))); return brush; }
    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth; var h = ActualHeight; if (w < 20 || h < 20) return;
        var (top, bottom, edge, band) = Mode switch
        {
            NoteMode.Prompt => ("#F2EBE1", "#E7DBC9", "#A89A7E", "#B1A181"),
            NoteMode.Password => ("#E6EDF2", "#CBD7E1", "#82929C", "#7E98A9"),
            _ => ("#E7ECDD", "#CCD8BA", "#7C8B69", "#80965D")
        };
        var pageTop = h - 31;
        dc.DrawRoundedRectangle(Ui.Brush(edge), null, new Rect(4, pageTop - 8, w - 9, 23), 13, 13);
        dc.DrawRoundedRectangle(Gradient("#F4F1E6", "#DDDACA"), new Pen(Ui.Brush(edge), .65), new Rect(8, pageTop - 6, w - 18, 17), 9, 9);
        for (var i = 0; i < 5; i++) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(28, 80, 80, 58)), .55), new Point(17, pageTop + i * 2), new Point(w - 13, pageTop + i * 2));
        dc.DrawRoundedRectangle(Gradient(top, bottom), new Pen(Ui.Brush(edge), .65), new Rect(3, 1, w - 8, h - 25), 17, 17);
        dc.PushClip(new RectangleGeometry(new Rect(3, 1, w - 8, h - 25), 17, 17));
        var fabric = new DrawingGroup();
        using (var tile = fabric.Open())
        {
            tile.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(27, 255, 255, 245)), .65), new Point(0, 7), new Point(7, 0));
            tile.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(11, 44, 59, 34)), .5), new Point(0, 3), new Point(7, 10));
            tile.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(17, 255, 255, 255)), .5), new Point(3, 0), new Point(10, 7));
        }
        var weave = new DrawingBrush(fabric) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 7, 7), Stretch = Stretch.None };
        dc.DrawRectangle(weave, null, new Rect(3, 1, w - 8, h - 25));
        dc.DrawRectangle(new LinearGradientBrush(Color.FromArgb(55, 79, 91, 63), Colors.Transparent, new Point(0, 0), new Point(1, 0)), null, new Rect(5, 1, 16, h - 25));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(95, 255, 255, 255)), 1.7), new Point(15, 6), new Point(15, pageTop - 8));
        dc.Pop();
        dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(95, 255, 255, 255)), 1.2), new Rect(5, 3, w - 12, h - 29), 16, 16);
        var ribbon = Geometry.Parse(FormattableString.Invariant($"M {w*.13} {h-28} L {w*.13+20} {h-28} L {w*.13+20} {h+4} L {w*.13+10} {h-2} L {w*.13} {h+4} Z"));
        dc.DrawGeometry(Ui.Brush(band), null, ribbon);
        var bandX = w * .825;
        dc.DrawRectangle(Gradient(edge, band, band, edge), null, new Rect(bandX, 0, 12, h - 14));
        for (var i = 1; i < 6; i++) dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(33, 255, 255, 255)), .5), new Point(bandX + i * 1.7, 0), new Point(bandX + i * 1.7, h - 14));
        if (Dark) dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(40, 13, 27, 13)), null, new Rect(3, 1, w - 8, h - 25), 17, 17);
    }
}
