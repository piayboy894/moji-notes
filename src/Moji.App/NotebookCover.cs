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
    private static readonly Dictionary<string, Geometry> Paths = IconPaths.Data.ToDictionary(p => p.Key, p => { var geometry = Geometry.Parse(p.Value); geometry.Freeze(); return geometry; });
    protected override void OnRender(DrawingContext dc)
    {
        var brush = Foreground ?? Ui.Resource("Ink");
        var size = Math.Min(ActualWidth,ActualHeight); dc.PushTransform(new TranslateTransform((ActualWidth-size)/2,(ActualHeight-size)/2));
        dc.PushTransform(new ScaleTransform(size/24,size/24));
        var pen = new Pen(brush, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        dc.DrawGeometry(null, pen, Paths.GetValueOrDefault(Kind,Paths["Document"])); dc.Pop();
        dc.Pop();
    }
    public static string ForNotebook(Notebook book)=>IconPaths.Data.ContainsKey(book.Icon)?book.Icon:ForMode(book.Mode);
    public static string ForMode(NoteMode mode) => mode switch { NoteMode.Prompt => "Prompt", NoteMode.Password => "Lock", _ => "Document" };
}

public sealed class NotebookCover : FrameworkElement
{
    public NoteMode Mode { get; set; }
    public bool Dark { get; set; }
    public string CoverColor { get; set; } = "#75876D";
    public string CoverColorEnd { get; set; } = "";
    private static Color C(string value) => (Color)ColorConverter.ConvertFromString(value);
    private static Brush Gradient(params string[] colors)
    { var brush = new LinearGradientBrush { StartPoint = new Point(0, .1), EndPoint = new Point(1, .9) }; for (var i = 0; i < colors.Length; i++) brush.GradientStops.Add(new(C(colors[i]), i / (double)(colors.Length - 1))); return brush; }
    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth; var h = ActualHeight; if (w < 20 || h < 20) return;
        var chosen=NotebookAppearance.Parse(CoverColor);var top=NotebookAppearance.Mix(chosen,Colors.White,.78).ToString();var bottom=NotebookAppearance.Mix(chosen,Colors.White,.58).ToString();var edge=NotebookAppearance.Mix(chosen,Colors.Black,.15).ToString();var band=chosen.ToString();
        if(!string.IsNullOrEmpty(CoverColorEnd)){var last=NotebookAppearance.Parse(CoverColorEnd);top=NotebookAppearance.Paper(chosen).ToString();bottom=NotebookAppearance.Paper(last).ToString();band=NotebookAppearance.Mix(NotebookAppearance.Mix(chosen,last,.5),Colors.Black,.18).ToString();edge=NotebookAppearance.Mix(NotebookAppearance.Parse(band),Colors.Black,.15).ToString();}
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
