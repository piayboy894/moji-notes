using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Moji.Core;

namespace Moji.App;
public partial class MainWindow
{
    private bool draggingNotebook;
    internal void MoveNotebook(string source,string target,bool after=false)
    {
        var state=VM.State!;var next=state.MoveNotebook(source,target,after);if(!next.Notebooks.SequenceEqual(state.Notebooks))VM.Change(_=>next);
    }
    private void AddOrderActions(ContextMenu menu,Notebook book)
    {
        var books=VM.State!.LiveBooks.ToList();var index=books.FindIndex(b=>b.Id==book.Id);menu.Items.Add(new Separator());var up=MenuItem("向前移动",()=>{if(index>0)MoveNotebook(book.Id,books[index-1].Id);});up.IsEnabled=index>0;menu.Items.Add(up);var down=MenuItem("向后移动",()=>{if(index>=0&&index<books.Count-1)MoveNotebook(book.Id,books[index+1].Id,true);});down.IsEnabled=index>=0&&index<books.Count-1;menu.Items.Add(down);
    }
    private void AttachNotebookDrag(Button button,Notebook book,bool horizontal)
    {
        Point origin=default;NotebookInsertion? indicator=null;AdornerLayer? layer=null;
        void Clear(){if(indicator!=null){layer?.Remove(indicator);indicator=null;}}
        button.PreviewMouseLeftButtonDown+=(_,e)=>origin=e.GetPosition(button);
        button.PreviewMouseMove+=(_,e)=>{if(draggingNotebook||e.LeftButton!=MouseButtonState.Pressed)return;var point=e.GetPosition(button);if(Math.Abs(point.X-origin.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(point.Y-origin.Y)<SystemParameters.MinimumVerticalDragDistance)return;draggingNotebook=true;try{DragDrop.DoDragDrop(button,new DataObject("MojiNotebook",book.Id),DragDropEffects.Move);}finally{Clear();Dispatcher.BeginInvoke(()=>draggingNotebook=false,DispatcherPriority.ContextIdle);}};
        button.AllowDrop=true;
        button.DragOver+=(_,e)=>{if(!e.Data.GetDataPresent("MojiNotebook"))return;e.Handled=true;var source=e.Data.GetData("MojiNotebook") as string;if(source==book.Id){e.Effects=DragDropEffects.None;Clear();return;}e.Effects=DragDropEffects.Move;var p=e.GetPosition(button);var after=horizontal?p.X>button.ActualWidth/2:p.Y>button.ActualHeight/2;Clear();layer=AdornerLayer.GetAdornerLayer(button);if(layer!=null){indicator=new NotebookInsertion(button,horizontal,after);layer.Add(indicator);}var parent=VisualTreeHelper.GetParent(button);while(parent!=null&&parent is not ScrollViewer)parent=VisualTreeHelper.GetParent(parent);if(parent is ScrollViewer scroll){var at=e.GetPosition(scroll);if(at.Y<32)scroll.LineUp();else if(at.Y>scroll.ActualHeight-32)scroll.LineDown();}};
        button.DragLeave+=(_,_)=>Clear();button.Drop+=(_,e)=>{if(!e.Data.GetDataPresent("MojiNotebook"))return;e.Handled=true;Clear();if(e.Data.GetData("MojiNotebook") is string source){var p=e.GetPosition(button);MoveNotebook(source,book.Id,horizontal?p.X>button.ActualWidth/2:p.Y>button.ActualHeight/2);}};
    }
    private sealed class NotebookInsertion:Adorner
    {
        private readonly bool horizontal,after;
        public NotebookInsertion(UIElement target,bool horizontal,bool after):base(target){this.horizontal=horizontal;this.after=after;IsHitTestVisible=false;}
        protected override void OnRender(DrawingContext dc){var bounds=AdornedElement.RenderSize;var pen=new Pen(Ui.Resource("Accent"),3);if(horizontal){var x=after?bounds.Width:0;dc.DrawLine(pen,new Point(x,6),new Point(x,bounds.Height-6));}else{var y=after?bounds.Height:0;dc.DrawLine(pen,new Point(6,y),new Point(bounds.Width-6,y));}}
    }
}
