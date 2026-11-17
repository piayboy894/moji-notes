using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Moji.App;

// One bounded drawing, one clock, one opaque editor plane. Only the selected
// card travels independently. Old/new text is never cross-faded on the same card.
internal sealed class RecordEntryScene(BitmapSource overview, Rect overviewBounds,
    BitmapSource list, Rect listBounds, BitmapSource editor, Rect editorBounds,
    BitmapSource? sourceCard, Rect? sourceBounds, BitmapSource? targetCard, Rect? targetBounds,
    Brush background, Brush divider) : FrameworkElement
{
    internal static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(RecordEntryScene), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    internal double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    private static double Clamp(double x) => Math.Clamp(x, 0, 1);
    private static double Ease(double x) { x = Clamp(x); return 1 - Math.Pow(1-x, 3); }
    internal double EditorLeft => ActualWidth + (editorBounds.Left-ActualWidth)*Ease((Progress-.12)/.88);
    internal double OldOpacity => 1-Clamp(Progress/.28);
    internal double ListOpacity => Clamp((Progress-.76)/.24);
    internal Rect? MovingCardBounds => sourceBounds is {} from && targetBounds is {} to
        ? new Rect(from.X+(to.X-from.X)*Ease(Progress/.76), from.Y+(to.Y-from.Y)*Ease(Progress/.76),
            from.Width+(to.Width-from.Width)*Ease(Progress/.76), from.Height+(to.Height-from.Height)*Ease(Progress/.76)) : null;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        Paint(dc);
    }

    internal BitmapSource Frame(double progress)
    {
        Progress = progress;
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen()) Paint(dc);
        var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawing); return bitmap;
    }

    private void Paint(DrawingContext dc)
    {
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        dc.DrawRectangle(background, null, new Rect(RenderSize));
        var seam = EditorLeft;
        // Every card stays behind the same moving boundary. The ready pane,
        // including its solid background and separator, enters as one object.
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, Math.Max(0,seam), ActualHeight)));
        if (OldOpacity > 0)
        {
            dc.PushOpacity(OldOpacity);
            dc.PushTransform(new TranslateTransform(-48*Ease(Progress/.28), 0));
            DrawWithoutCard(dc, overview, overviewBounds, sourceBounds);
            dc.Pop(); dc.Pop();
        }
        if (ListOpacity > 0)
        {
            dc.PushOpacity(ListOpacity);
            DrawWithoutCard(dc, list, listBounds, targetBounds);
            dc.Pop();
        }
        if (MovingCardBounds is {} card && sourceCard != null && targetCard != null)
        {
            // A single opaque card, with no old/new text overlay. Its final
            // typography is swapped only after its geometry has settled.
            dc.DrawRectangle(background, null, card);
            dc.PushClip(new RectangleGeometry(card));
            // Preserve glyph proportions while the card gets shorter. Crop the
            // old footer rather than vertically crushing all its text.
            var pixels = Progress < .76 ? sourceCard : targetCard;
            var height = Progress < .76 ? card.Width * pixels.PixelHeight / pixels.PixelWidth : card.Height;
            dc.DrawImage(pixels, new Rect(card.X, card.Y, card.Width, height));
            dc.Pop();
        }
        dc.Pop();
        var pane = new Rect(seam, editorBounds.Top, editorBounds.Width, editorBounds.Height);
        dc.DrawImage(editor, pane);
        dc.DrawRectangle(divider, null, new Rect(seam, 0, 1, ActualHeight));
        dc.Pop();
    }

    private static void DrawWithoutCard(DrawingContext dc, BitmapSource pixels, Rect bounds, Rect? card)
    {
        // Exclusion, not even-odd overlapping holes: no duplicate heading,
        // inverted cuts, cropped neighbour strips or second selected card.
        if (card != null) dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude,
            new RectangleGeometry(bounds), new RectangleGeometry(card.Value)));
        dc.DrawImage(pixels, bounds);
        if (card != null) dc.Pop();
    }
}
