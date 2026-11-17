using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace Moji.App;

public partial class MainWindow
{
    // The HWND browser remains parked during this short composition. Only the
    // visible cards and one ready editor frame move; the live page is not resized
    // every animation frame and is never duplicated or continuously captured.
    private sealed record EntryPiece(string Key, Rect Bounds, BitmapSource Pixels);
    private sealed class OverviewEntry(string id, long epoch, Grid owner, Canvas layer)
    {
        internal readonly string Id = id;
        internal readonly long Epoch = epoch;
        internal readonly Grid Owner = owner;
        internal readonly Canvas Layer = layer;
        internal readonly CancellationTokenSource Cancellation = new();
    }
    private OverviewEntry? overviewEntry;
    private Task recordEntryCompletion = Task.CompletedTask;
    private int revealVersion;
    internal Task RecordEntryCompletion => recordEntryCompletion;
    internal bool? RecordMotionOverride { get; set; }
    internal int LastRecordEntrySnapshotCount { get; private set; }
    internal bool RecordEntryActive => overviewEntry != null;
    internal Action<string>? RecordEntryStage { get; set; }

    private void BeginOverviewEntry(string id)
    {
        CancelOverviewEntry();
        if (!(RecordMotionOverride ?? SystemParameters.ClientAreaAnimation) || !inOverview || overviewGrid?.IsVisible != true || Body.Parent is not Grid owner || Body.ActualWidth < 100)
        { inOverview = false; VM.SelectedId = id; RenderWorkspace(); recordEntryCompletion = RevealSelectedRecord(center: true); return; }

        LastRecordEntrySnapshotCount = 0;
        var source = CardPieces(overviewGrid, id).ToList();
        AddPiece(source, "heading", overviewHeading, trimWidth: true);
        AddPiece(source, "search", overviewSearch);
        if (overviewRoot?.Children[0] is Panel header && header.Children.Count > 1 && header.Children[1] is Panel filters)
            AddPiece(source, "sort", filters.Children.OfType<ComboBox>().FirstOrDefault());
        var layer = new Canvas { Name = "RecordEntryLayer", Background = Ui.Resource("Bg"), ClipToBounds = true, IsHitTestVisible = false };
        Grid.SetRow(layer, Grid.GetRow(Body)); Panel.SetZIndex(layer, 20); owner.Children.Add(layer);
        var overviewPixels = CaptureEntryPixels(overviewRoot!);
        var overviewBounds = BoundsInBody(overviewRoot!);
        if(overviewPixels!=null){var image=new Image{Source=overviewPixels,Width=overviewBounds.Width,Height=overviewBounds.Height,Stretch=Stretch.Fill};Canvas.SetLeft(image,overviewBounds.X);Canvas.SetTop(image,overviewBounds.Y);layer.Children.Add(image);}
        var entry = new OverviewEntry(id, VM.WorkspaceEpoch, owner, layer); overviewEntry = entry;
        inOverview = false; VM.SelectedId = id;
        RenderWorkspace(); editorHost!.IsHitTestVisible = false; workspaceRoot!.Children[0].IsHitTestVisible = false;
        recordEntryCompletion = RunOverviewEntry(entry, source, overviewPixels, overviewBounds);
    }

    private async Task RunOverviewEntry(OverviewEntry entry, List<EntryPiece> source, BitmapSource? overviewPixels, Rect overviewBounds)
    {
        var token = entry.Cancellation.Token;
        try
        {
            await RevealSelectedRecord(center: true);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render, token);
            if (!CurrentEntry(entry)) return;
            var target = CardPieces(noteList!, entry.Id).ToList();
            AddPiece(target, "heading", listHeading, trimWidth: true); AddPiece(target, "search", recordSearch); AddPiece(target, "sort", recordSort);
            entry.Layer.Children.Clear();
            var leftWidth = workspaceRoot!.ColumnDefinitions[0].ActualWidth;
            var side = new Border { Width = Math.Max(1, Body.ActualWidth), Height = Body.ActualHeight, Background = Ui.Resource("Side"), RenderTransform = new ScaleTransform(1, 1) };
            entry.Layer.Children.Add(side);
            Animate((ScaleTransform)side.RenderTransform, ScaleTransform.ScaleXProperty, 1, leftWidth / side.Width, 240);
            if(overviewPixels!=null)
            {
                var image=new Image{Source=overviewPixels,Width=overviewBounds.Width,Height=overviewBounds.Height,Stretch=Stretch.Fill,RenderTransform=new TranslateTransform()};
                var clip=new GeometryGroup{FillRule=FillRule.EvenOdd};clip.Children.Add(new RectangleGeometry(new Rect(0,0,overviewBounds.Width,overviewBounds.Height)));
                foreach(var piece in source){var hole=piece.Bounds;hole.Offset(-overviewBounds.X,-overviewBounds.Y);clip.Children.Add(new RectangleGeometry(hole));}clip.Freeze();image.Clip=clip;
                Canvas.SetLeft(image,overviewBounds.X);Canvas.SetTop(image,overviewBounds.Y);entry.Layer.Children.Add(image);
                Animate((TranslateTransform)image.RenderTransform,TranslateTransform.XProperty,0,-70,160);image.BeginAnimation(OpacityProperty,new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(160)));
            }
            var targetByKey = target.ToDictionary(p => p.Key);
            foreach (var piece in source)
            {
                if (targetByKey.Remove(piece.Key, out var end)) PlacePiece(entry.Layer, piece, end, 240, 0, piece.Key == entry.Id);
                else PlacePiece(entry.Layer, piece, piece with { Bounds = new Rect(piece.Bounds.X - Math.Min(380, Body.ActualWidth * .4), piece.Bounds.Y, piece.Bounds.Width, piece.Bounds.Height) }, 160, 0, disappear: true);
            }
            foreach (var piece in targetByKey.Values)
                PlacePiece(entry.Layer, piece with { Bounds = new Rect(piece.Bounds.X + 38, piece.Bounds.Y, piece.Bounds.Width, piece.Bounds.Height) }, piece, 190, 50, appear: true);

            var leftPieces = entry.Layer.Children.Cast<UIElement>().ToArray();
            var started = System.Diagnostics.Stopwatch.StartNew();
            var right = new Border { Width = editorHost!.ActualWidth, Height = editorHost.ActualHeight, Background = Ui.Resource("Panel") };
            var rightBounds = BoundsInBody(editorHost); Canvas.SetLeft(right, rightBounds.X); Canvas.SetTop(right, rightBounds.Y); entry.Layer.Children.Add(right);
            RecordEntryStage?.Invoke("cards");
            var openFrame = EditorEntryFrame(entry);
            // Let the real list become interactive as soon as its cards settle,
            // even when a recovering renderer needs longer to prepare its frame.
            var settle = SettleLeft();
            async Task SettleLeft()
            {
                try { await Task.Delay(250, token); } catch(OperationCanceledException) { return; }
                if (!CurrentEntry(entry)) return;
                workspaceRoot!.Children[0].IsHitTestVisible = true;
                entry.Layer.Background = null;
                foreach (var child in leftPieces) entry.Layer.Children.Remove(child);
            }
            var editorFrame = await openFrame;
            await Task.Delay(Math.Max(0, 140-(int)started.ElapsedMilliseconds),token);
            if (!CurrentEntry(entry)) return;
            if(editorFrame==null)return;
            var moving = new Grid { Width = rightBounds.Width, Height = rightBounds.Height, Background = Ui.Resource("Panel"), RenderTransform = new TranslateTransform(rightBounds.Width, 0) };
            moving.Children.Add(editorFrame); Canvas.SetLeft(moving, rightBounds.X); Canvas.SetTop(moving, rightBounds.Y); entry.Layer.Children.Add(moving);
            Animate((TranslateTransform)moving.RenderTransform, TranslateTransform.XProperty, rightBounds.Width, 0, 210);
            RecordEntryStage?.Invoke("editor");
            await Task.WhenAll(settle,Task.Delay(220, token));
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { AppDiagnostics.Write("record-entry", error.GetType().Name); }
        finally { if (ReferenceEquals(overviewEntry, entry)) CancelOverviewEntry(revealEditor: true); }
    }

    private bool CurrentEntry(OverviewEntry entry) => ReferenceEquals(overviewEntry, entry) && VM.WorkspaceEpoch == entry.Epoch && VM.SelectedId == entry.Id && !inOverview;
    private void CancelOverviewEntry(bool revealEditor = false)
    {
        var entry = overviewEntry; if (entry == null) return;
        overviewEntry = null; entry.Cancellation.Cancel(); entry.Cancellation.Dispose(); entry.Owner.Children.Remove(entry.Layer); entry.Layer.Children.Clear();
        if (editorHost != null) editorHost.IsHitTestVisible = true;
        if (workspaceRoot != null) workspaceRoot.Children[0].IsHitTestVisible = true;
        if (revealEditor && !inOverview && VM.SelectedId == entry.Id && VM.WorkspaceEpoch == entry.Epoch && sharedEditor is { } editor && editor.NoteId==entry.Id && editor.Ready.IsCompletedSuccessfully && recordBody != null && recordContext == entry.Id)
        {
            if (editor.Parent is Panel parent && !ReferenceEquals(parent, recordBody)) parent.Children.Remove(editor);
            if (editor.Parent == null) recordBody.Children.Add(editor);
        }
    }

    private async Task<FrameworkElement?> EditorEntryFrame(OverviewEntry entry)
    {
        var token = entry.Cancellation.Token;
        await editorTransition.WaitAsync(token);
        if (!CurrentEntry(entry)) throw new OperationCanceledException();
        BitmapSource? body = null;
        if (sharedEditor is { } editor)
        {
            await editor.Ready.WaitAsync(token);
            var stream = new MemoryStream(); var capture = editor.Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            try
            {
                await capture.WaitAsync(TimeSpan.FromMilliseconds(800), token);
                stream.Position = 0;
                var pixels = new BitmapImage(); pixels.BeginInit(); pixels.CacheOption = BitmapCacheOption.OnLoad; pixels.DecodePixelWidth = Math.Min(1400, Math.Max(100, (int)recordBody!.ActualWidth)); pixels.StreamSource = stream; pixels.EndInit(); pixels.Freeze(); body = pixels; LastRecordEntrySnapshotCount++;
            }
            catch (TimeoutException) { AppDiagnostics.Write("record-entry-preview", "timeout"); }
            finally
            {
                if (capture.IsCompleted) stream.Dispose();
                else _ = capture.ContinueWith(t => { _ = t.Exception; stream.Dispose(); }, TaskScheduler.Default);
            }
        }
        if (!CurrentEntry(entry)) throw new OperationCanceledException();
        if(sharedEditor!=null&&body==null)return null;
        editorHost!.UpdateLayout();
        var canvas = new Canvas { Width = editorHost.ActualWidth, Height = editorHost.ActualHeight, ClipToBounds = true, Background = Ui.Resource("Panel") };
        if (CaptureEntryPixels(editorHost) is { } chrome) canvas.Children.Add(new Image { Source = chrome, Width = canvas.Width, Height = canvas.Height, Stretch = Stretch.Fill });
        if (body != null && recordBody != null)
        {
            var at = recordBody.TranslatePoint(new Point(), editorHost);
            var image = new Image { Source = body, Width = recordBody.ActualWidth, Height = recordBody.ActualHeight, Stretch = Stretch.Fill };
            Canvas.SetLeft(image, at.X); Canvas.SetTop(image, at.Y); canvas.Children.Add(image);
        }
        return canvas;
    }

    private async Task RevealSelectedRecord(bool center = false)
    {
        var version = ++revealVersion; var id = VM.SelectedId; var view = VM.View;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        if (version != revealVersion || inOverview || VM.View != view || VM.SelectedId != id || noteList?.IsVisible != true || selectingRecords) return;
        var row = noteRows.FirstOrDefault(r => r.Note.Id == id); if (row == null) return;
        noteList.UpdateLayout();
        var scroll = EntryDescendant<ScrollViewer>(noteList);
        var item = noteList.ItemContainerGenerator.ContainerFromItem(row) as ListBoxItem;
        if (!center && item != null && scroll != null)
        {
            var at = item.TranslatePoint(new Point(), scroll).Y;
            if (at >= 0 && at + item.ActualHeight <= scroll.ViewportHeight) return;
        }
        noteList.ScrollIntoView(row); noteList.UpdateLayout();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        if (version != revealVersion || inOverview || VM.SelectedId != id || noteList?.IsVisible != true) return;
        item = noteList.ItemContainerGenerator.ContainerFromItem(row) as ListBoxItem; scroll = EntryDescendant<ScrollViewer>(noteList);
        if (center && item != null && scroll != null)
        {
            var top = item.TranslatePoint(new Point(), scroll).Y;
            scroll.ScrollToVerticalOffset(scroll.VerticalOffset + top - Math.Max(0, (scroll.ViewportHeight - item.ActualHeight) / 2)); noteList.UpdateLayout();
        }
    }

    private static T? EntryDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T found) return found;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) if (EntryDescendant<T>(VisualTreeHelper.GetChild(root, i)) is { } value) return value;
        return null;
    }
    private static IEnumerable<ListBoxItem> EntryContainers(DependencyObject root)
    {
        if (root is ListBoxItem item) { yield return item; yield break; }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in EntryContainers(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private Rect BoundsInBody(FrameworkElement element) => element.TransformToAncestor(Body).TransformBounds(new Rect(element.RenderSize));
    private IEnumerable<EntryPiece> CardPieces(ListBox list, string selectedId)
    {
        var viewport = BoundsInBody(list);
        foreach (var item in EntryContainers(list).OrderByDescending(c => (c.DataContext as NoteRow)?.Note.Id==selectedId).Take(28))
        {
            if (item.DataContext is not NoteRow row) continue;
            var full = BoundsInBody(item); var visible = Rect.Intersect(full, viewport);
            if (visible.IsEmpty || visible.Width < 4 || visible.Height < 4) continue;
            var crop = new Rect(visible.X - full.X, visible.Y - full.Y, visible.Width, visible.Height);
            if (CaptureEntryPixels(item, crop) is { } pixels) yield return new(row.Note.Id, visible, pixels);
        }
    }
    private void AddPiece(List<EntryPiece> pieces, string key, FrameworkElement? element, bool trimWidth = false)
    {
        if (element == null || !element.IsVisible) return;
        var bounds = BoundsInBody(element);
        if (trimWidth) bounds.Width = Math.Min(bounds.Width, element.DesiredSize.Width);
        if (CaptureEntryPixels(element, new Rect(0, 0, bounds.Width, bounds.Height)) is { } pixels) pieces.Add(new(key, bounds, pixels));
    }
    private BitmapSource? CaptureEntryPixels(FrameworkElement element, Rect? crop = null)
    {
        var area = crop ?? new Rect(element.RenderSize); if (area.Width < 1 || area.Height < 1) return null;
        var scale = Math.Min(1, Math.Min(1600 / area.Width, 1200 / area.Height));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(element) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = area, Stretch = Stretch.Fill }, null, new Rect(0, 0, area.Width * scale, area.Height * scale));
        var pixels = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(area.Width * scale)), Math.Max(1, (int)Math.Ceiling(area.Height * scale)), 96, 96, PixelFormats.Pbgra32);
        pixels.Render(visual); pixels.Freeze(); LastRecordEntrySnapshotCount++; return pixels;
    }
    private static void Animate(Animatable target, DependencyProperty property, double from, double to, int milliseconds, int delay = 0)
    {
        target.BeginAnimation(property, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds)) { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }
    private static void PlacePiece(Canvas canvas, EntryPiece from, EntryPiece to, int duration, int delay, bool selected = false, bool disappear = false, bool appear = false)
    {
        var layer = new Grid { Width = to.Bounds.Width, Height = to.Bounds.Height, IsHitTestVisible = false };
        Canvas.SetLeft(layer, to.Bounds.X); Canvas.SetTop(layer, to.Bounds.Y); Panel.SetZIndex(layer, selected ? 5 : 1);
        var scale = new ScaleTransform(from.Bounds.Width / to.Bounds.Width, from.Bounds.Height / to.Bounds.Height); var move = new TranslateTransform(from.Bounds.X - to.Bounds.X, from.Bounds.Y - to.Bounds.Y);
        var transforms = new TransformGroup(); transforms.Children.Add(scale); transforms.Children.Add(move); layer.RenderTransform = transforms;
        var old = new Image { Source = from.Pixels, Stretch = Stretch.Fill }; var next = new Image { Source = to.Pixels, Stretch = Stretch.Fill, Opacity = appear ? 1 : 0 };
        layer.Children.Add(old); if (!ReferenceEquals(from.Pixels, to.Pixels)) layer.Children.Add(next); canvas.Children.Add(layer);
        if (duration == 0) return;
        Animate(scale, ScaleTransform.ScaleXProperty, scale.ScaleX, 1, duration, delay); Animate(scale, ScaleTransform.ScaleYProperty, scale.ScaleY, 1, duration, delay);
        Animate(move, TranslateTransform.XProperty, move.X, 0, duration, delay); Animate(move, TranslateTransform.YProperty, move.Y, 0, duration, delay);
        if (disappear) layer.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(duration)));
        else if (appear) layer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(duration)) { BeginTime = TimeSpan.FromMilliseconds(delay) });
        else if (!ReferenceEquals(from.Pixels, to.Pixels)) { old.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(duration))); next.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(duration))); }
    }
}
