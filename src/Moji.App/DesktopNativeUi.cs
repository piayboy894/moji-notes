using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;

namespace Moji.App;
internal static class DesktopNativeUi
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    public static void Decorate(Window window)
    {
        window.SourceInitialized+=(_,_)=>ApplyCaption(window);
        window.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape&&!e.Handled){window.Close();e.Handled=true;}};
    }
    public static void ApplyCaption(Window window)
    {
        var handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;
        try{int Color(string name){var c=((SolidColorBrush)Ui.Resource(name)).Color;return c.R|(c.G<<8)|(c.B<<16);}var caption=Color("Bg");var text=Color("Ink");var border=Color("Line");var rounded=2;DwmSetWindowAttribute(handle,35,ref caption,4);DwmSetWindowAttribute(handle,36,ref text,4);DwmSetWindowAttribute(handle,34,ref border,4);DwmSetWindowAttribute(handle,33,ref rounded,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
    }
    public static string? IconFor(string title)=>title.Contains("删除")||title.Contains("回收站")?"Trash":title.Contains("复制")||title.Contains("副本")?"Copy":title.Contains("收藏")?"Star":title.Contains("归档")?"Archive":title.Contains("恢复")||title.Contains("版本")?"Restore":title.Contains("置顶")?"Pin":title.Contains("记事本")?"Book":title.Contains("导出")||title.Contains("打印")?"External":title.Contains("全选")||title.Contains("选择")?"Check":null;
    public static MenuItem Item(string title,Action action,string? icon=null,bool danger=false)
    {
        var item=new MenuItem{Header=title};if((icon??IconFor(title)) is {} kind)item.Icon=new NativeIcon{Kind=kind,Width=18,Height=18};if(item.Icon is NativeIcon glyph)glyph.SetBinding(NativeIcon.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){Source=item});if(danger||title.Contains("删除")||title.Contains("回收站"))item.SetResourceReference(Control.ForegroundProperty,"Danger");item.Click+=(_,_)=>action();return item;
    }
    public static ContextMenu TextMenu(TextBox box)
    {
        var menu=new ContextMenu();
        void Add(string title,RoutedUICommand command,string keys){var item=new MenuItem{Header=title,Command=command,CommandTarget=box,InputGestureText=keys};if(IconFor(title) is {} kind)item.Icon=new NativeIcon{Kind=kind,Width=18,Height=18};menu.Items.Add(item);}
        Add("撤销",ApplicationCommands.Undo,"Ctrl+Z");Add("重做",ApplicationCommands.Redo,"Ctrl+Y");menu.Items.Add(new Separator());Add("剪切",ApplicationCommands.Cut,"Ctrl+X");Add("复制",ApplicationCommands.Copy,"Ctrl+C");Add("粘贴",ApplicationCommands.Paste,"Ctrl+V");menu.Items.Add(new Separator());Add("全选",ApplicationCommands.SelectAll,"Ctrl+A");return menu;
    }
    public static ContextMenu BrowserMenu(CoreWebView2ContextMenuRequestedEventArgs args,FrameworkElement browser,Action<string> history)
    {
        var menu=new ContextMenu{PlacementTarget=browser,Placement=PlacementMode.MousePoint};var deferral=args.GetDeferral();args.Handled=true;var done=false;
        void Complete(){if(done)return;done=true;deferral.Complete();}
        void Build(ItemCollection target,IList<CoreWebView2ContextMenuItem> source){foreach(var entry in source){
            if(entry.Kind==CoreWebView2ContextMenuItemKind.Separator){if(target.Count>0&&target[target.Count-1] is not Separator)target.Add(new Separator());continue;}
            var label=entry.Name switch{"undo"=>"撤销","redo"=>"重做","cut"=>"剪切","copy"=>"复制","paste"=>"粘贴","pasteAndMatchStyle"=>"粘贴为纯文本","selectAll"=>"全选","copyLink"=>"复制链接","copyImage"=>"复制图片","saveImageAs"=>"图片另存为","copyImageLocation"=>"复制图片地址",_=>entry.Label.Replace("&","")};
            if(entry.Name is "back" or "forward" or "reload" or "saveAs" or "print" or "viewSource" or "inspectElement" or "openLinkNewWindow" or "openLinkNewTab")continue;
            var item=Item(label,()=>{if(entry.Name is "undo" or "redo")history(entry.Name);else args.SelectedCommandId=entry.CommandId;menu.IsOpen=false;Complete();});item.IsEnabled=entry.IsEnabled;item.IsCheckable=entry.Kind is CoreWebView2ContextMenuItemKind.CheckBox or CoreWebView2ContextMenuItemKind.Radio;item.IsChecked=entry.IsChecked;if(entry.Kind==CoreWebView2ContextMenuItemKind.Submenu)Build(item.Items,entry.Children);target.Add(item);
        }if(target.Count>0&&target[target.Count-1] is Separator)target.RemoveAt(target.Count-1);}
        Build(menu.Items,args.MenuItems);menu.Closed+=(_,_)=>Complete();if(menu.Items.Count==0)Complete();return menu;
    }
}
