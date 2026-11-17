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
        var source = CardPieces(overviewGrid, id).FirstOrDefault(p => p.Key == id);
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

    internal Action<RecordEntryScene>? RecordEntryPrepared { get; set; }

    private async Task RunOverviewEntry(OverviewEntry entry, EntryPiece? source, BitmapSource? overviewPixels, Rect overviewBounds)
    {
        var token = entry.Cancellation.Token;
        try
        {
            await RevealSelectedRecord(center: true);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render, token);
            if (!CurrentEntry(entry)) return;
            var target = CardPieces(noteList!, entry.Id).FirstOrDefault(p => p.Key == entry.Id);
            var left = (FrameworkElement)workspaceRoot!.Children[0];
            var leftPixels = CaptureEntryPixels(left);
            // Keep the complete overview visible while preparing the single ready
            // editor frame. No final editor background is exposed in advance.
            var editorFrame = await EditorEntryFrame(entry);
            if (!CurrentEntry(entry) || editorFrame == null || overviewPixels == null || leftPixels == null) return;
            var scene = new RecordEntryScene(overviewPixels, overviewBounds, leftPixels, BoundsInBody(left),
                editorFrame, BoundsInBody(editorHost!), source?.Pixels, source?.Bounds, target?.Pixels, target?.Bounds,
                Ui.Resource("Side"), Ui.Resource("Line")) { Width = Body.ActualWidth, Height = Body.ActualHeight };
            entry.Layer.Children.Clear(); entry.Layer.Children.Add(scene);
            scene.Measure(new Size(scene.Width, scene.Height)); scene.Arrange(new Rect(0, 0, scene.Width, scene.Height));
            RecordEntryPrepared?.Invoke(scene);
            scene.BeginAnimation(RecordEntryScene.ProgressProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(360)));
            RecordEntryStage?.Invoke("cards"); RecordEntryStage?.Invoke("editor");
            await Task.Delay(380, token);
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

    private async Task<BitmapSource?> EditorEntryFrame(OverviewEntry entry)
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
        canvas.Measure(new Size(canvas.Width, canvas.Height));
        canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height)); canvas.UpdateLayout();
        return CaptureEntryPixels(canvas);
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
    private BitmapSource? CaptureEntryPixels(FrameworkElement element, Rect? crop = null)
    {
        var area = crop ?? new Rect(element.RenderSize); if (area.Width < 1 || area.Height < 1) return null;
        var scale = Math.Min(1, Math.Min(1600 / area.Width, 1200 / area.Height));
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(element) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = area, Stretch = Stretch.Fill }, null, new Rect(0, 0, area.Width * scale, area.Height * scale));
        var pixels = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(area.Width * scale)), Math.Max(1, (int)Math.Ceiling(area.Height * scale)), 96, 96, PixelFormats.Pbgra32);
        pixels.Render(visual); pixels.Freeze(); LastRecordEntrySnapshotCount++; return pixels;
    }
}
