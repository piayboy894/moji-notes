using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Moji.App;
internal static class RectangleSelection
{
    public static void Attach(ListBox list)
    {
        Point start=default,current=default;bool pending=false,dragging=false;var baseline=new HashSet<object>();SelectionFrame? frame=null;AdornerLayer? layer=null;ScrollViewer? scroll=null;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};
        void Stop(){timer.Stop();pending=false;dragging=false;if(frame!=null){layer?.Remove(frame);frame=null;}if(list.IsMouseCaptured)list.ReleaseMouseCapture();}
        void Select(){if(!dragging)return;var offset=scroll?.VerticalOffset??0;var target=new Rect(start,new Point(current.X,current.Y+offset));if(frame!=null){frame.Bounds=new Rect(target.X,target.Y-offset,target.Width,target.Height);frame.InvalidateVisual();}ApplyRegion(list,target,baseline,offset);}

        timer.Tick+=(_,_)=>{if(!dragging||scroll==null)return;var before=scroll.VerticalOffset;if(current.Y<30)scroll.LineUp();else if(current.Y>list.ActualHeight-30)scroll.LineDown();if(before!=scroll.VerticalOffset)Select();};
        list.PreviewMouseLeftButtonDown+=(_,e)=>{var source=e.OriginalSource as DependencyObject;var element=source;var item=false;while(element!=null&&element!=list){if(element is ButtonBase or TextBoxBase or ScrollBar)return;if(element is ListBoxItem)item=true;element=VisualTreeHelper.GetParent(element);}current=e.GetPosition(list);scroll=FindScroll(list);start=new Point(current.X,current.Y+(scroll?.VerticalOffset??0));baseline=(Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Shift))!=0?list.SelectedItems.Cast<object>().ToHashSet():[];if(!item&&baseline.Count==0)list.UnselectAll();pending=true;};
        list.PreviewMouseMove+=(_,e)=>{if(!pending||e.LeftButton!=MouseButtonState.Pressed)return;current=e.GetPosition(list);if(!dragging){var delta=new Point(start.X,start.Y-(scroll?.VerticalOffset??0));if(Math.Abs(current.X-delta.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(current.Y-delta.Y)<SystemParameters.MinimumVerticalDragDistance)return;dragging=true;list.CaptureMouse();layer=AdornerLayer.GetAdornerLayer(list);if(layer!=null){frame=new SelectionFrame(list);layer.Add(frame);}timer.Start();}Select();e.Handled=true;};
        list.PreviewMouseLeftButtonUp+=(_,e)=>{if(dragging)e.Handled=true;Stop();};list.LostMouseCapture+=(_,_)=>Stop();list.Unloaded+=(_,_)=>Stop();
    }
    internal static void ApplyRegion(ListBox list,Rect target,ISet<object>? baseline=null,double offset=0){foreach(var item in list.Items){if(list.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container)continue;var bounds=container.TransformToAncestor(list).TransformBounds(new Rect(container.RenderSize));bounds.Offset(0,offset);var hit=(baseline?.Contains(item)??false)||target.IntersectsWith(bounds);if(hit&&!container.IsSelected)container.IsSelected=true;else if(!hit&&container.IsSelected)container.IsSelected=false;}}
    private static ScrollViewer? FindScroll(DependencyObject parent){if(parent is ScrollViewer scroll)return scroll;for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=FindScroll(VisualTreeHelper.GetChild(parent,i));if(child!=null)return child;}return null;}
    private sealed class SelectionFrame:Adorner
    {
        public SelectionFrame(UIElement target):base(target){IsHitTestVisible=false;}
        public Rect Bounds{get;set;}
        protected override void OnRender(DrawingContext dc){if(Bounds.IsEmpty)return;dc.PushClip(new RectangleGeometry(new Rect(AdornedElement.RenderSize)));var color=((SolidColorBrush)Ui.Resource("Accent")).Color;dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(28,color.R,color.G,color.B)),new Pen(Ui.Resource("Accent"),1),Bounds,2,2);dc.Pop();}
    }
}
