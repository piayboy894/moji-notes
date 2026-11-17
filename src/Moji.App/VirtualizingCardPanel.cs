using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Moji.App;

// Pixel scrolling with two overscan rows. Reflow happens on resize, never per
// animation frame; only visible cards get WPF containers and document previews.
public sealed class VirtualizingCardPanel : VirtualizingPanel, IScrollInfo
{
    private const double CellWidth = 272, CellHeight = 204;
    private Size viewport, extent;
    private double offset;
    private int columns = 1;
    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        base.OnItemsChanged(sender,args);
        if(args.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        { if(InternalChildren.Count>0)RemoveInternalChildRange(0,InternalChildren.Count); }
        else if(args.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Remove or System.Collections.Specialized.NotifyCollectionChangedAction.Replace or System.Collections.Specialized.NotifyCollectionChangedAction.Move)
        {
            var at=args.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Move?args.OldPosition.Index:args.Position.Index;
            if(at>=0&&at<InternalChildren.Count&&args.ItemUICount>0)RemoveInternalChildRange(at,Math.Min(args.ItemUICount,InternalChildren.Count-at));
        }
        InvalidateMeasure();
    }
    protected override Size MeasureOverride(Size available)
    {
        var owner = ItemsControl.GetItemsOwner(this); if (owner == null) return available;
        viewport = new(double.IsInfinity(available.Width) ? ScrollOwner?.ViewportWidth ?? CellWidth : available.Width, double.IsInfinity(available.Height) ? (ScrollOwner?.ActualHeight is > 0 ? ScrollOwner.ActualHeight : 600) : available.Height);
        if (!double.IsFinite(viewport.Width) || viewport.Width <= 0) viewport = new(CellWidth, viewport.Height);
        columns = Math.Max(1, (int)(viewport.Width / CellWidth));
        extent = new(viewport.Width, Math.Ceiling((double)owner.Items.Count / columns) * CellHeight);
        offset = Math.Clamp(offset, 0, Math.Max(0, extent.Height - viewport.Height)); ScrollOwner?.InvalidateScrollInfo();
        var first = Math.Max(0, ((int)(offset / CellHeight) - 1) * columns);
        var last = Math.Min(owner.Items.Count - 1, ((int)((offset + viewport.Height) / CellHeight) + 2) * columns - 1);
        var generator = ItemContainerGenerator; var position = generator.GeneratorPositionFromIndex(first);
        var childIndex = position.Offset == 0 ? position.Index : position.Index + 1;
        using (generator.StartAt(position, GeneratorDirection.Forward, true))
        {
            for (var index = first; index <= last; index++, childIndex++)
            {
                var child = (UIElement)generator.GenerateNext(out var created);
                if (created) { InsertInternalChild(childIndex, child); generator.PrepareItemContainer(child); }
                child.Measure(new Size(Math.Min(CellWidth, viewport.Width), CellHeight));
            }
        }
        for (var i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var item = generator.IndexFromGeneratorPosition(new(i, 0));
            if (item < first || item > last) { generator.Remove(new(i, 0), 1); RemoveInternalChildRange(i, 1); }
        }
        return viewport;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var item = ItemContainerGenerator.IndexFromGeneratorPosition(new(i, 0));
            InternalChildren[i].Arrange(new Rect((item % columns) * CellWidth, (item / columns) * CellHeight - offset, Math.Min(CellWidth, finalSize.Width), CellHeight));
        }
        return finalSize;
    }
    protected override void BringIndexIntoView(int index) => SetVerticalOffset(index / columns * CellHeight);
    public ScrollViewer? ScrollOwner { get; set; }
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public double ExtentWidth => extent.Width;
    public double ExtentHeight => extent.Height;
    public double ViewportWidth => viewport.Width;
    public double ViewportHeight => viewport.Height;
    public double HorizontalOffset => 0;
    public double VerticalOffset => offset;
    public void SetVerticalOffset(double value) { var next = Math.Clamp(value, 0, Math.Max(0, extent.Height - viewport.Height)); if (Math.Abs(next - offset) < .1) return; offset = next; InvalidateMeasure(); ScrollOwner?.InvalidateScrollInfo(); }
    public void SetHorizontalOffset(double value) { }
    public void LineUp() => SetVerticalOffset(offset - 32);
    public void LineDown() => SetVerticalOffset(offset + 32);
    public void MouseWheelUp() => SetVerticalOffset(offset - 96);
    public void MouseWheelDown() => SetVerticalOffset(offset + 96);
    public void PageUp() => SetVerticalOffset(offset - viewport.Height);
    public void PageDown() => SetVerticalOffset(offset + viewport.Height);
    public void LineLeft() { } public void LineRight() { } public void MouseWheelLeft() { } public void MouseWheelRight() { } public void PageLeft() { } public void PageRight() { }
    public Rect MakeVisible(Visual visual, Rect rectangle) => rectangle;
}
