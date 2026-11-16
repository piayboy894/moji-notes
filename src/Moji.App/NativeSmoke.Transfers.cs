using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Moji.Core;

namespace Moji.App;
internal static partial class NativeSmoke
{
    private static async Task CheckNotebookDrag(MainWindow window,WorkspaceViewModel vm,List<string> checks,string output,string first,string second)
    {
        window.Navigate("home");await Render();var root=FindNamed<Grid>(window,"Root")!;
        var order=vm.State!.LiveBooks.Select(b=>b.Id).ToArray();
        var covers=FindAll<NotebookCover>(window).ToList();var cover=covers.First();var color=cover.CoverColor;
        byte[] Pixels(FrameworkElement element){var visual=new DrawingVisual();using(var drawing=visual.RenderOpen())drawing.DrawRectangle(new VisualBrush(element),null,new Rect(0,0,element.ActualWidth,element.ActualHeight));var bitmap=new RenderTargetBitmap((int)element.ActualWidth,(int)element.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);if(!pixels.Where((_,i)=>i%4==3).Any(a=>a==255))throw new Exception("Cover capture was empty");return pixels;}
        vm.Change(w=>w with{Preferences=w.Preferences with{Theme="light"}},"edit");window.Navigate("home");await Render();var light=Pixels(FindAll<NotebookCover>(window).First(c=>c.CoverColor==color));
        vm.Change(w=>w with{Preferences=w.Preferences with{Theme="dark"}},"edit");window.Navigate("home");await Render();var dark=Pixels(FindAll<NotebookCover>(window).First(c=>c.CoverColor==color));
        if(!light.SequenceEqual(dark))throw new Exception("Dark theme still dims notebook cover pixels");
        vm.Change(w=>w with{Preferences=w.Preferences with{Theme="light"}},"edit");window.Navigate("home");await Render();checks.Add("opaque notebook covers retain identical pixels across light and dark themes");
        var moving=window.NotebookButton(first,true);var target=window.NotebookButton(second,true);var sourceIndex=Array.IndexOf(order,first);var targetIndex=Array.IndexOf(order,second);var neighbourSlot=window.NotebookButton(order[targetIndex+(targetIndex>sourceIndex?-1:1)],true).TranslatePoint(new Point(),root);
        var origin=moving.TranslatePoint(new Point(moving.ActualWidth/2,moving.ActualHeight/2),root);var goal=target.TranslatePoint(new Point(target.ActualWidth/2,target.ActualHeight/2),root);var oldTarget=target.TranslatePoint(new Point(),root);var oldMoving=moving.TranslatePoint(new Point(),root);
        window.BeginNotebookDrag(first,true,origin);if(!ReferenceEquals(window.PickedUpNotebook,moving)||moving.Opacity!=1)throw new Exception("Drag must pick up the original opaque notebook");
        for(var i=0;i<22;i++){window.UpdateNotebookDrag(goal);await Task.Delay(10);}
        var neighbour=target.TranslatePoint(new Point(),root);if((neighbour-neighbourSlot).Length>2)throw new Exception("Neighbour animation is restarted while pointer stays in one slot");
        if(!vm.State.LiveBooks.Select(b=>b.Id).SequenceEqual(order)||!moving.IsLoaded)throw new Exception("Drag preview changed data or rebuilt the picked-up control");
        Capture(window,Path.Combine(output,"notebook-single-object-drag.png"));await window.EndNotebookDrag(false);await Render();if(window.PickedUpNotebook!=null||!vm.State.LiveBooks.Select(b=>b.Id).SequenceEqual(order)||moving.CacheMode!=null)throw new Exception("Canceled drag did not restore order or release its render cache");
        window.BeginNotebookDrag(first,true,origin);window.UpdateNotebookDrag(goal);await Task.Delay(160);await window.EndNotebookDrag(true);await Render();if(!moving.IsLoaded||!FindAll<Button>(window).Contains(moving)||vm.State.LiveBooks.ElementAt(targetIndex).Id!=first)throw new Exception("Drop rebuilt the notebook instead of settling the original control");
        window.MoveNotebook(first,second);await Render();checks.Add("notebook dragging moves one original opaque control; neighbours settle without repeated restarts, cancel restores and drop preserves controls");
    }
    private static Button AncestorButton(DependencyObject element){for(var p=VisualTreeHelper.GetParent(element);p!=null;p=VisualTreeHelper.GetParent(p))if(p is Button button)return button;throw new Exception("Notebook button missing");}
    [DllImport("user32.dll")] private static extern bool GetKeyboardState([Out] byte[] state);
    [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] state);
    private static void Chord(byte key)
    {
        var target=Keyboard.FocusedElement as UIElement??throw new Exception("Shortcut target has no native focus");
        var saved=new byte[256];if(!GetKeyboardState(saved))throw new Exception("Cannot read test keyboard state");var pressed=(byte[])saved.Clone();pressed[0x11]=pressed[0xA2]=pressed[key]=0x80;
        try{if(!SetKeyboardState(pressed))throw new Exception("Cannot set test keyboard modifiers");
            var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(target),Environment.TickCount,KeyInterop.KeyFromVirtualKey(key)){RoutedEvent=Keyboard.PreviewKeyDownEvent};target.RaiseEvent(args);
            if(!args.Handled){args.RoutedEvent=Keyboard.KeyDownEvent;target.RaiseEvent(args);if(!args.Handled&&key==0x43&&target is TextBox text)ApplicationCommands.Copy.Execute(null,text);}
        }finally{SetKeyboardState(saved);}
    }
    private static async Task Until(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(8);while(!ready()){if(DateTime.UtcNow>end)throw new Exception("Native record keyboard action timed out");await Task.Delay(40);}}
    private static async Task CheckRecordClipboard(MainWindow window,WorkspaceViewModel vm,List<string> checks)
    {
        var saved=vm.State!;var view=vm.View;var selected=vm.SelectedId;
        var a=new Notebook{Name="Transfer source",Mode=NoteMode.Password};var b=new Notebook{Name="Transfer destination",Mode=NoteMode.Password};var incompatible=new Notebook{Name="Text destination",Mode=NoteMode.Text};
        var n=new Note{NotebookId=a.Id,Title="Transfer one",Password="test-only",Fields=[new(){Name="Region",Value="Stored"}],UpdatedAt=10};var n2=new Note{NotebookId=a.Id,Title="Transfer two",UpdatedAt=11};
        vm.Change(w=>w with{Notebooks=[..w.Notebooks,a,b,incompatible],Notes=[..w.Notes,n,n2]});window.Navigate(a.Id,n.Id);await Render();window.Activate();Clipboard.Clear();
        var list=FindNamed<ListBox>(window,"RecordList")!;list.Focus();Chord(0x43);await Until(()=>Clipboard.ContainsData("Moji.RecordClipboard"));
        window.Navigate(b.Id);await Render();list.Focus();Chord(0x56);await Until(()=>vm.State!.LiveNotes.Any(note=>note.NotebookId==b.Id));var copy=vm.State!.LiveNotes.First(note=>note.NotebookId==b.Id);
        if(copy.Id==n.Id||copy.Fields.Single().Value!="Stored"||copy.Password!=n.Password)throw new Exception("Ctrl+C/V did not copy the complete record");
        window.Navigate(a.Id,n.Id);await Render();FindNamed<Button>(window,"SelectRecords")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));FindNamed<Button>(window,"SelectAllRecords")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));list.Focus();Clipboard.Clear();Chord(0x58);await Until(()=>Clipboard.ContainsData("Moji.RecordClipboard"));if(vm.State.Notes.First(note=>note.Id==n.Id).NotebookId!=a.Id)throw new Exception("Cut deleted before paste");
        window.Navigate(incompatible.Id);await Render();list.Focus();Chord(0x56);await Until(()=>FindNamed<TextBlock>(window,"ToastText")!.Text.Contains("类型不兼容"));if(vm.State.Notes.First(note=>note.Id==n.Id).NotebookId!=a.Id||vm.State.Notes.First(note=>note.Id==n2.Id).NotebookId!=a.Id)throw new Exception("Incompatible paste partially moved records");
        window.Navigate(b.Id);await Render();list.Focus();Chord(0x56);await Until(()=>vm.State!.Notes.First(note=>note.Id==n.Id).NotebookId==b.Id);if(vm.State.Notes.First(note=>note.Id==n2.Id).NotebookId!=b.Id||vm.State.Notes.First(note=>note.Id==n.Id).UpdatedAt!=10||Clipboard.ContainsData("Moji.RecordClipboard"))throw new Exception("Batch cut did not preserve edit times or consume the pending cut");
        window.Navigate(b.Id,n.Id);await Render();var password=FindAll<TextBox>(window).First(box=>box.Text=="test-only");password.Focus();password.SelectAll();Chord(0x43);await Until(()=>Clipboard.ContainsText()&&Clipboard.GetText()=="test-only");if(Clipboard.ContainsData("Moji.RecordClipboard"))throw new Exception("Text copy was hijacked by record shortcuts");
        vm.Change(_=>saved);window.Navigate(view,selected);await Render();var editor=await window.ReadyEditor();var hijacked=await editor.Browser.CoreWebView2.ExecuteScriptAsync("['c','x','v'].some(key=>{const e=new KeyboardEvent('keydown',{key,ctrlKey:true,bubbles:true,cancelable:true});document.dispatchEvent(e);return e.defaultPrevented})");if(hijacked!="false")throw new Exception("Record shortcuts leaked into the browser text editor");checks.Add("native Ctrl+C/V and multi-record Ctrl+X/V routing preserve complete records, reject incompatible targets atomically, and leave text copy intact");
    }
}
