using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Moji.Core;

namespace Moji.App;
public partial class MainWindow
{
    private bool draggingNotebook;
    private WrapPanel? notebookCards;
    private NotebookDragSession? notebookDrag;
    private readonly HashSet<Button> previewBooks = [];
    private record BookDragKey(string Id, bool Dashboard);
    private sealed class NotebookDragSession(Button button, List<Button> buttons, Point pointer, Point origin)
    {
        internal readonly Button Button = button;
        internal readonly List<Button> Buttons = buttons;
        internal readonly Point Grab = new(pointer.X - origin.X, pointer.Y - origin.Y);
        internal readonly int Start = buttons.IndexOf(button);
        internal int Target = buttons.IndexOf(button);
        internal Point Pointer = pointer;
        internal Point LastPointer = new(double.NaN,double.NaN);
        internal double LastScroll = double.NaN;
        internal readonly Panel Container=(Panel)VisualTreeHelper.GetParent(button);
        internal readonly double Width=button.Width,Height=button.Height;
        internal readonly Thickness Margin=button.Margin;
        internal readonly Border Placeholder=new(){Width=button.ActualWidth,Height=button.ActualHeight,Margin=button.Margin,IsHitTestVisible=false};
        internal readonly Canvas Layer=new(){IsHitTestVisible=false};
        internal readonly List<Rect> Slots=buttons.Select(b=>new Rect((Point)VisualTreeHelper.GetOffset(b),b.RenderSize)).ToList();
        internal bool Ending;
        internal readonly bool HitTestVisible=button.IsHitTestVisible;
        internal readonly Cursor? PreviousCursor=Mouse.OverrideCursor;
        internal long LastFrame = Stopwatch.GetTimestamp();
        internal readonly Dictionary<Button, CacheMode?> Caches = [];
        internal readonly Dictionary<Button, Point> Destinations = [];
    }
    private IEnumerable<Button> BookButtons() => BooksPanel.Children.OfType<Button>().Concat(VM.View == "home" && notebookCards != null ? notebookCards.Children.OfType<Button>() : []).Where(b => b.Tag is BookDragKey && b.IsVisible);
    private Dictionary<BookDragKey, Point> BookPositions() => BookButtons().ToDictionary(b => (BookDragKey)b.Tag, b => b.TranslatePoint(new Point(), Root));
    private Point LayoutPoint(Button button) => ((FrameworkElement)VisualTreeHelper.GetParent(button)).TranslatePoint((Point)VisualTreeHelper.GetOffset(button), Root);

    // Reorder the existing controls. A notebook move must not rebuild either
    // surface, its editor, or the picked-up object.
    private void ReorderNotebookPanels()
    {
        var order = VM.State!.LiveBooks.Select(b => b.Id).ToList();
        foreach (var panel in new Panel?[] { BooksPanel, VM.View == "home" ? notebookCards : null }) {
            if (panel == null) continue;
            var buttons = panel.Children.OfType<Button>().Where(b => b.Tag is BookDragKey).ToDictionary(b => ((BookDragKey)b.Tag).Id);
            for (var i = 0; i < order.Count; i++) if (buttons.TryGetValue(order[i], out var button) && panel.Children.IndexOf(button) != i) { panel.Children.Remove(button); panel.Children.Insert(i, button); }
        }
    }
    internal void MoveNotebook(string source, string target, bool after = false)
    {
        var state = VM.State!; var next = state.MoveNotebook(source, target, after);
        if (next.Notebooks.SequenceEqual(state.Notebooks)) return;
        var before = BookPositions();
        VM.Change(_ => next, "notebook-order"); Root.UpdateLayout();
        foreach (var button in BookButtons()) if (before.TryGetValue((BookDragKey)button.Tag, out var old)) {
            var at = LayoutPoint(button); TranslateBook(button, old.X - at.X, old.Y - at.Y, false); TranslateBook(button, 0, 0, true);
        }
    }
    private static void TranslateBook(Button button, double x, double y, bool animated)
    {
        if (button.RenderTransform is not TranslateTransform transform) { transform = new(); button.RenderTransform = transform; }
        var fromX = transform.X; var fromY = transform.Y;
        transform.BeginAnimation(TranslateTransform.XProperty, null); transform.BeginAnimation(TranslateTransform.YProperty, null);
        transform.X = x; transform.Y = y;
        if (!animated || !SystemParameters.ClientAreaAnimation || Math.Abs(fromX - x) + Math.Abs(fromY - y) < .5) return;
        var duration = TimeSpan.FromMilliseconds(150); var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(fromX, x, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(fromY, y, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
    }
    private static void CacheBook(NotebookDragSession drag, Button button)
    { if (!drag.Caches.ContainsKey(button)) { drag.Caches[button] = button.CacheMode; button.CacheMode = new BitmapCache(); } }
    private void PreviewOrder(List<Button> buttons, Button moving, int index)
    {
        var slots = notebookDrag is {} session && ReferenceEquals(session.Buttons,buttons)?session.Slots.Select(r=>(Vector)r.Location).ToList():buttons.Select(b => VisualTreeHelper.GetOffset(b)).ToList();
        var indices=buttons.Select((b,i)=>(b,i)).ToDictionary(pair=>pair.b,pair=>pair.i);
        var order = buttons.Where(b => b != moving).ToList(); order.Insert(index, moving);
        for (var i = 0; i < order.Count; i++) {
            var button = order[i]; if (button == moving) continue;
            var delta = slots[i] - slots[indices[button]]; var target = new Point(delta.X, delta.Y);
            if (notebookDrag is { } drag) {
                if (drag.Destinations.TryGetValue(button, out var previous) && previous == target) continue;
                drag.Destinations[button] = target; CacheBook(drag, button);
            }
            TranslateBook(button, delta.X, delta.Y, true); previewBooks.Add(button);
        }
    }
    internal void PreviewNotebookOrder(string source, string target, bool after, bool dashboard)
    {
        var buttons = BookButtons().Where(b => ((BookDragKey)b.Tag).Dashboard == dashboard).ToList();
        var moving = buttons.FirstOrDefault(b => ((BookDragKey)b.Tag).Id == source);
        var anchor = buttons.FirstOrDefault(b => ((BookDragKey)b.Tag).Id == target);
        if (moving == null || anchor == null || moving == anchor) return;
        var remaining = buttons.Where(b => b != moving).ToList(); PreviewOrder(buttons, moving, remaining.IndexOf(anchor) + (after ? 1 : 0));
    }
    internal void CancelNotebookPreview() { foreach (var button in previewBooks) TranslateBook(button, 0, 0, true); previewBooks.Clear(); }
    private void AddOrderActions(ContextMenu menu, Notebook book)
    {
        menu.Items.Add(new Separator());
        void Move(int step){var books=VM.State!.LiveBooks.ToList();var index=books.FindIndex(b=>b.Id==book.Id);if(index>=0&&index+step>=0&&index+step<books.Count)MoveNotebook(book.Id,books[index+step].Id,step>0);}
        var up=MenuItem("向前移动",()=>Move(-1));var down=MenuItem("向后移动",()=>Move(1));menu.Items.Add(up);menu.Items.Add(down);
        void Refresh(){var books=VM.State!.LiveBooks.ToList();var index=books.FindIndex(b=>b.Id==book.Id);up.IsEnabled=index>0;down.IsEnabled=index>=0&&index<books.Count-1;}
        Refresh();menu.Opened+=(_,_)=>Refresh();
    }
    private void AttachNotebookDrag(Button button, Notebook book, bool dashboard)
    {
        button.Tag = new BookDragKey(book.Id, dashboard); Point origin = default;bool pressedHere=false;
        button.PreviewMouseLeftButtonDown += (_, e) => {origin = e.GetPosition(Root);pressedHere=true;};
        button.PreviewMouseLeftButtonUp += (_,_)=>pressedHere=false;
        button.PreviewMouseMove += (_, e) => {
            if (!pressedHere || draggingNotebook || e.LeftButton != MouseButtonState.Pressed) return;
            var pointer = e.GetPosition(Root);
            if (Math.Abs(pointer.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(pointer.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            pressedHere=false;BeginNotebookDrag(button, origin, true); if (notebookDrag != null) notebookDrag.Pointer = pointer; e.Handled = true;
        };
    }
    internal Button? PickedUpNotebook => notebookDrag?.Button;
    internal Button NotebookButton(string id,bool dashboard)=>BookButtons().First(b=>b.Tag is BookDragKey key&&key.Id==id&&key.Dashboard==dashboard);
    internal void BeginNotebookDrag(string id, bool dashboard, Point pointer) => BeginNotebookDrag(NotebookButton(id,dashboard), pointer, false);
    private void BeginNotebookDrag(Button button, Point pointer, bool capture)
    {
        if (draggingNotebook) return;
        CancelNotebookPreview(); var dashboard = ((BookDragKey)button.Tag).Dashboard;
        var buttons = BookButtons().Where(b => ((BookDragKey)b.Tag).Dashboard == dashboard).ToList();
        var origin=button.TranslatePoint(new Point(),Root);
        var drag = new NotebookDragSession(button, buttons, pointer, origin); notebookDrag = drag; draggingNotebook = true;
        var index=drag.Container.Children.IndexOf(button);drag.Container.Children.Remove(button);drag.Container.Children.Insert(index,drag.Placeholder);
        button.Width=drag.Slots[drag.Start].Width;button.Height=drag.Slots[drag.Start].Height;button.Margin=new Thickness(0);button.RenderTransform=new TranslateTransform(origin.X,origin.Y);
        drag.Layer.Children.Add(button);Panel.SetZIndex(drag.Layer,1000);Root.Children.Add(drag.Layer);
        button.IsHitTestVisible=false;CacheBook(drag, button); Panel.SetZIndex(button, 100); Mouse.OverrideCursor=Cursors.SizeAll;
        Root.PreviewMouseMove += TrackNotebookPointer; Root.PreviewMouseLeftButtonUp += DropNotebookPointer; Root.LostMouseCapture += NotebookCaptureLost;
        CompositionTarget.Rendering += RenderNotebookDrag;
        if (capture&&!Mouse.Capture(Root, CaptureMode.SubTree))AbortNotebookDrag();
    }
    private void TrackNotebookPointer(object sender, MouseEventArgs e)
    { if (notebookDrag is { Ending: false } drag) { drag.Pointer = e.GetPosition(Root); e.Handled = true; } }
    private async void DropNotebookPointer(object sender, MouseButtonEventArgs e)
    { if (notebookDrag is { Ending: false } drag) { drag.Pointer = e.GetPosition(Root); e.Handled = true; await EndNotebookDrag(true); } }
    private void NotebookCaptureLost(object sender, MouseEventArgs e) { if (notebookDrag is { Ending: false } && Mouse.Captured!=Root) _ = EndNotebookDrag(false); }
    internal void UpdateNotebookDrag(Point pointer) { if (notebookDrag is { Ending: false } drag) { drag.Pointer = pointer; RenderNotebookDrag(this, EventArgs.Empty); } }
    private void RenderNotebookDrag(object? sender, EventArgs e)
    {
        if (notebookDrag is not { Ending: false } drag) return;
        var now = Stopwatch.GetTimestamp(); var elapsed = Math.Min(.04, (double)(now - drag.LastFrame) / Stopwatch.Frequency); drag.LastFrame = now;
        var parent = (DependencyObject)drag.Container;
        while (parent != null && parent is not ScrollViewer) parent = VisualTreeHelper.GetParent(parent);
        if (parent is ScrollViewer scroll) {
            var top = scroll.TranslatePoint(new Point(), Root); var edge = Math.Min(40, scroll.ActualHeight / 4); var y = drag.Pointer.Y - top.Y;
            if (drag.Pointer.X >= top.X && drag.Pointer.X <= top.X + scroll.ActualWidth) {
                var speed = y < edge ? -Math.Clamp((edge - y) / edge, 0, 1) : y > scroll.ActualHeight - edge ? Math.Clamp((y - scroll.ActualHeight + edge) / edge, 0, 1) : 0;
                if (speed != 0) scroll.ScrollToVerticalOffset(Math.Clamp(scroll.VerticalOffset + speed * elapsed * 580, 0, scroll.ScrollableHeight));
            }
        }
        var offset=parent is ScrollViewer currentScroll?currentScroll.VerticalOffset:0;
        if(drag.Pointer==drag.LastPointer && offset==drag.LastScroll)return;
        drag.LastPointer=drag.Pointer;drag.LastScroll=offset;
        var containerAt=drag.Container.TranslatePoint(new Point(),Root);
        TranslateBook(drag.Button, drag.Pointer.X - drag.Grab.X, drag.Pointer.Y - drag.Grab.Y, false);
        var center = new Point(drag.Pointer.X - drag.Grab.X + drag.Button.ActualWidth / 2, drag.Pointer.Y - drag.Grab.Y + drag.Button.ActualHeight / 2);
        var dashboard = ((BookDragKey)drag.Button.Tag).Dashboard;
        double Distance(int index) { var slot=drag.Slots[index]; var dx=center.X-containerAt.X-slot.X-slot.Width/2;var dy=center.Y-containerAt.Y-slot.Y-slot.Height/2;return dashboard?Math.Sqrt(dx*dx+dy*dy):Math.Abs(dy); }
        var nearest = Enumerable.Range(0, drag.Buttons.Count).MinBy(Distance);
        if (nearest != drag.Target && Distance(nearest) + 7 < Distance(drag.Target)) { drag.Target = nearest; PreviewOrder(drag.Buttons, drag.Button, nearest); }
    }
    private void DetachNotebookInput()
    { Root.PreviewMouseMove -= TrackNotebookPointer; Root.PreviewMouseLeftButtonUp -= DropNotebookPointer; Root.LostMouseCapture -= NotebookCaptureLost; CompositionTarget.Rendering -= RenderNotebookDrag; if (Mouse.Captured == Root) Mouse.Capture(null); }
    internal async Task EndNotebookDrag(bool commit)
    {
        if (notebookDrag is not { Ending: false } drag) return;
        try {
        if(commit)RenderNotebookDrag(this,EventArgs.Empty);drag.Ending = true; DetachNotebookInput();
        var slots = drag.Slots.Select(slot=>drag.Container.TranslatePoint(slot.TopLeft,Root)).ToList(); var bounds = new Rect(slots[0], drag.Slots[0].Size);
        for(var i=0;i<slots.Count;i++)bounds.Union(new Rect(slots[i],drag.Slots[i].Size));
        if(((BookDragKey)drag.Button.Tag).Dashboard&&notebookCards?.Children.OfType<Button>().FirstOrDefault(b=>b.Name=="AddNotebookCard") is {} add)bounds.Union(new Rect(add.TranslatePoint(new Point(),Root),add.RenderSize));
        var parent=(DependencyObject)drag.Container;while(parent!=null&&parent is not ScrollViewer)parent=VisualTreeHelper.GetParent(parent);
        if(parent is ScrollViewer viewport)bounds.Intersect(new Rect(viewport.TranslatePoint(new Point(),Root),viewport.RenderSize));bounds.Inflate(12,12);
        commit &= bounds.Contains(drag.Pointer);
        if (!commit) { drag.Target = drag.Start; CancelNotebookPreview(); }
        var to = slots[drag.Target]; TranslateBook(drag.Button, to.X, to.Y, true);
        if (SystemParameters.ClientAreaAnimation) await Task.Delay(160);
        if (notebookDrag != drag) return;
        RestoreLiftedNotebook(drag,to);
        if (commit && drag.Target != drag.Start) MoveNotebook(((BookDragKey)drag.Button.Tag).Id, ((BookDragKey)drag.Buttons[drag.Target].Tag).Id, drag.Target > drag.Start);
        foreach (var b in drag.Buttons) TranslateBook(b, 0, 0, false);
        ResetNotebookDrag(drag);
        } finally {
            if(notebookDrag==drag){if(drag.Button.Parent==drag.Layer)RestoreLiftedNotebook(drag,drag.Container.TranslatePoint(drag.Slots[drag.Start].TopLeft,Root));foreach(var button in drag.Buttons)TranslateBook(button,0,0,false);ResetNotebookDrag(drag);}
        }
    }
    private void RestoreLiftedNotebook(NotebookDragSession drag,Point at)
    {
        if(drag.Button.Parent!=drag.Layer)return;
        var origin=drag.Container.TranslatePoint(drag.Slots[drag.Start].TopLeft,Root);
        drag.Layer.Children.Remove(drag.Button);Root.Children.Remove(drag.Layer);
        var index=drag.Container.Children.IndexOf(drag.Placeholder);if(index>=0)drag.Container.Children.Remove(drag.Placeholder);else index=Math.Min(drag.Start,drag.Container.Children.Count);
        drag.Button.Width=drag.Width;drag.Button.Height=drag.Height;drag.Button.Margin=drag.Margin;
        drag.Container.Children.Insert(index,drag.Button);TranslateBook(drag.Button,at.X-origin.X,at.Y-origin.Y,false);Root.UpdateLayout();
    }
    private void ResetNotebookDrag(NotebookDragSession drag)
    { foreach (var (button, cache) in drag.Caches) button.CacheMode = cache; Panel.SetZIndex(drag.Button, 0); drag.Button.IsHitTestVisible=drag.HitTestVisible;Mouse.OverrideCursor=drag.PreviousCursor; previewBooks.Clear(); notebookDrag = null; draggingNotebook = false; }
    private void AbortNotebookDrag()
    { if (notebookDrag is not { } drag) return; drag.Ending = true; DetachNotebookInput();RestoreLiftedNotebook(drag,drag.Container.TranslatePoint(drag.Slots[drag.Start].TopLeft,Root)); foreach (var b in drag.Buttons) TranslateBook(b, 0, 0, false); ResetNotebookDrag(drag); }
}
