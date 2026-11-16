using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Moji.Core;

namespace Moji.App;
public partial class MainWindow
{
    private bool draggingNotebook;
    private WrapPanel? notebookCards;
    private NotebookGhost? notebookGhost;
    private readonly HashSet<Button> previewBooks=[];
    private record BookDragKey(string Id,bool Dashboard);
    private IEnumerable<Button> BookButtons()=>BooksPanel.Children.OfType<Button>().Concat(VM.View=="home"&&notebookCards!=null?notebookCards.Children.OfType<Button>():[]).Where(b=>b.Tag is BookDragKey&&b.IsVisible);
    private Dictionary<BookDragKey,Point> BookPositions()=>BookButtons().ToDictionary(b=>(BookDragKey)b.Tag,b=>b.TranslatePoint(new Point(),Root));
    internal void MoveNotebook(string source,string target,bool after=false)
    {
        var state=VM.State!;var next=state.MoveNotebook(source,target,after);if(next.Notebooks.SequenceEqual(state.Notebooks))return;
        var before=BookPositions();if(notebookGhost!=null){foreach(var key in before.Keys.Where(k=>k.Id==source&&k.Dashboard==notebookGhost.Dashboard).ToList())before[key]=notebookGhost.Position;}
        VM.Change(_=>next);Root.UpdateLayout();
        foreach(var button in BookButtons())if(before.TryGetValue((BookDragKey)button.Tag,out var old)){var at=button.TranslatePoint(new Point(),Root);TranslateBook(button,old.X-at.X,old.Y-at.Y,false);TranslateBook(button,0,0,true);}
    }
    private static void TranslateBook(Button button,double x,double y,bool animated)
    {
        if(button.RenderTransform is not TranslateTransform transform){transform=new TranslateTransform();button.RenderTransform=transform;}
        var fromX=transform.X;var fromY=transform.Y;transform.BeginAnimation(TranslateTransform.XProperty,null);transform.BeginAnimation(TranslateTransform.YProperty,null);transform.X=x;transform.Y=y;
        if(!animated||!SystemParameters.ClientAreaAnimation)return;
        var duration=TimeSpan.FromMilliseconds(180);var easing=new CubicEase{EasingMode=EasingMode.EaseOut};
        transform.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(fromX,x,duration){EasingFunction=easing,FillBehavior=FillBehavior.Stop});transform.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(fromY,y,duration){EasingFunction=easing,FillBehavior=FillBehavior.Stop});
    }
    internal void PreviewNotebookOrder(string source,string target,bool after,bool dashboard)
    {
        var buttons=BookButtons().Where(b=>((BookDragKey)b.Tag).Dashboard==dashboard).ToList();var moving=buttons.FirstOrDefault(b=>((BookDragKey)b.Tag).Id==source);var anchor=buttons.FirstOrDefault(b=>((BookDragKey)b.Tag).Id==target);if(moving==null||anchor==null||moving==anchor)return;
        var slots=buttons.Select(b=>VisualTreeHelper.GetOffset(b)).ToList();var order=buttons.Where(b=>b!=moving).ToList();order.Insert(order.IndexOf(anchor)+(after?1:0),moving);
        for(var i=0;i<order.Count;i++){var button=order[i];if(button==moving)continue;var original=slots[buttons.IndexOf(button)];var delta=slots[i]-original;TranslateBook(button,delta.X,delta.Y,true);previewBooks.Add(button);}
    }
    internal void CancelNotebookPreview(){foreach(var button in previewBooks)TranslateBook(button,0,0,true);previewBooks.Clear();}
    private void AddOrderActions(ContextMenu menu,Notebook book)
    {
        var books=VM.State!.LiveBooks.ToList();var index=books.FindIndex(b=>b.Id==book.Id);menu.Items.Add(new Separator());var up=MenuItem("向前移动",()=>{if(index>0)MoveNotebook(book.Id,books[index-1].Id);});up.IsEnabled=index>0;menu.Items.Add(up);var down=MenuItem("向后移动",()=>{if(index>=0&&index<books.Count-1)MoveNotebook(book.Id,books[index+1].Id,true);});down.IsEnabled=index>=0&&index<books.Count-1;menu.Items.Add(down);
    }
    private void AttachNotebookDrag(Button button,Notebook book,bool horizontal)
    {
        button.Tag=new BookDragKey(book.Id,horizontal);Point origin=default;NotebookInsertion? indicator=null;AdornerLayer? layer=null;
        void Clear(){if(indicator!=null){layer?.Remove(indicator);indicator=null;}}
        button.PreviewMouseLeftButtonDown+=(_,e)=>origin=e.GetPosition(button);
        button.PreviewMouseMove+=(_,e)=>{
            if(draggingNotebook||e.LeftButton!=MouseButtonState.Pressed)return;var point=e.GetPosition(button);if(Math.Abs(point.X-origin.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(point.Y-origin.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            draggingNotebook=true;var ghostLayer=AdornerLayer.GetAdornerLayer(Root);notebookGhost=new NotebookGhost(Root,button,horizontal,origin);notebookGhost.Move(button.TranslatePoint(point,Root));ghostLayer?.Add(notebookGhost);button.Opacity=.3;
            void Track(object sender,DragEventArgs args){notebookGhost?.Move(args.GetPosition(Root));}
            Root.PreviewDragOver+=Track;
            try{DragDrop.DoDragDrop(button,new DataObject("MojiNotebook",book.Id),DragDropEffects.Move);}
            finally{Root.PreviewDragOver-=Track;if(notebookGhost!=null)ghostLayer?.Remove(notebookGhost);notebookGhost=null;button.Opacity=1;Clear();CancelNotebookPreview();Dispatcher.BeginInvoke(()=>draggingNotebook=false,DispatcherPriority.ContextIdle);}
        };
        button.AllowDrop=true;
        button.DragOver+=(_,e)=>{if(!e.Data.GetDataPresent("MojiNotebook"))return;e.Handled=true;var source=e.Data.GetData("MojiNotebook") as string;if(source==book.Id){e.Effects=DragDropEffects.None;Clear();return;}e.Effects=DragDropEffects.Move;var p=e.GetPosition(button);var after=horizontal?p.X>button.ActualWidth/2:p.Y>button.ActualHeight/2;Clear();if(source!=null)PreviewNotebookOrder(source,book.Id,after,horizontal);layer=AdornerLayer.GetAdornerLayer(button);if(layer!=null){indicator=new NotebookInsertion(button,horizontal,after);layer.Add(indicator);}var parent=VisualTreeHelper.GetParent(button);while(parent!=null&&parent is not ScrollViewer)parent=VisualTreeHelper.GetParent(parent);if(parent is ScrollViewer scroll){var at=e.GetPosition(scroll);if(at.Y<32)scroll.LineUp();else if(at.Y>scroll.ActualHeight-32)scroll.LineDown();}};
        button.DragLeave+=(_,_)=>Clear();button.Drop+=(_,e)=>{if(!e.Data.GetDataPresent("MojiNotebook"))return;e.Handled=true;Clear();if(e.Data.GetData("MojiNotebook") is string source){var p=e.GetPosition(button);MoveNotebook(source,book.Id,horizontal?p.X>button.ActualWidth/2:p.Y>button.ActualHeight/2);}};
    }
    private sealed class NotebookGhost:Adorner
    {
        private readonly BitmapSource image;private readonly Size size;private readonly Point offset;
        internal bool Dashboard{get;}
        internal Point Position{get;private set;}
        public NotebookGhost(UIElement root,FrameworkElement source,bool dashboard,Point offset):base(root){Dashboard=dashboard;this.offset=offset;size=source.RenderSize;var bitmap=new RenderTargetBitmap(Math.Max(1,(int)size.Width),Math.Max(1,(int)size.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(source);bitmap.Freeze();image=bitmap;IsHitTestVisible=false;}
        internal void Move(Point pointer){Position=new(pointer.X-offset.X,pointer.Y-offset.Y);InvalidateVisual();}
        protected override void OnRender(DrawingContext dc){var box=new Rect(Position,size);dc.PushOpacity(.94);dc.PushTransform(new ScaleTransform(1.025,1.025,box.X+size.Width/2,box.Y+size.Height/2));dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(35,0,0,0)),null,new Rect(box.X+3,box.Y+6,box.Width,box.Height),9,9);dc.DrawImage(image,box);dc.Pop();dc.Pop();}
    }
    private sealed class NotebookInsertion:Adorner
    {
        private readonly bool horizontal,after;
        public NotebookInsertion(UIElement target,bool horizontal,bool after):base(target){this.horizontal=horizontal;this.after=after;IsHitTestVisible=false;}
        protected override void OnRender(DrawingContext dc){var bounds=AdornedElement.RenderSize;var pen=new Pen(Ui.Resource("Accent"),3);if(horizontal){var x=after?bounds.Width:0;dc.DrawLine(pen,new Point(x,6),new Point(x,bounds.Height-6));}else{var y=after?bounds.Height:0;dc.DrawLine(pen,new Point(6,y),new Point(bounds.Width-6,y));}}
    }
}
