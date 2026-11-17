using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Moji.Core;

namespace Moji.App;
public partial class MainWindow
{
    private bool Confirm(string title,string text,string acceptText="确认",bool danger=false)
    {
        var layout=new DockPanel{LastChildFill=true};var footer=new Border{Background=Ui.Resource("Side"),Padding=new Thickness(24,16,24,16),BorderBrush=Ui.Resource("Line"),BorderThickness=new Thickness(0,1,0,0)};DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
        var body=new StackPanel{Margin=new Thickness(28,26,28,28)};var heading=Ui.Row();if(danger){var icon=new Border{Background=Ui.Resource("DangerSoft"),CornerRadius=new CornerRadius(10),Padding=new Thickness(10),Margin=new Thickness(0,0,14,0),Child=new NativeIcon{Kind="Trash",Width=22,Height=22,Foreground=Ui.Resource("Danger")}};heading.Children.Add(icon);}var headingText=Ui.Text(title,20,null,true);headingText.MaxWidth=danger?356:420;heading.Children.Add(headingText);body.Children.Add(heading);var detail=Ui.Text(text,13);detail.Foreground=Ui.Resource("Sub");detail.LineHeight=23;body.Children.Add(Ui.Spaced(detail,new Thickness(0,16,0,0)));layout.Children.Add(body);
        var owner=Application.Current.Windows.OfType<Window>().LastOrDefault(w=>w.IsActive)??this;var dialog=Ui.Dialog(owner,"墨记",layout,490);var cancel=Ui.Button("取消",()=>dialog.DialogResult=false);cancel.IsCancel=true;cancel.IsDefault=danger;cancel.MinWidth=86;var accept=Ui.Button(acceptText,()=>dialog.DialogResult=true,true);if(danger)accept.Style=(Style)FindResource("DangerButton");accept.MinWidth=110;accept.IsDefault=!danger;var buttons=Ui.Row(cancel,Ui.Spaced(accept,new Thickness(10,0,0,0)));buttons.HorizontalAlignment=HorizontalAlignment.Right;footer.Child=buttons;dialog.Loaded+=(_,_)=>{if(danger)cancel.Focus();else accept.Focus();};return dialog.ShowDialog()==true;
    }
    private string? AskPassword(string title)
    {var pw=new PasswordBox{MinWidth=300};var content=Ui.Stack(Ui.Field(title,pw));content.Margin=new Thickness(27);Window? dialog=null;var button=Ui.Button("解密并继续",()=>dialog!.DialogResult=true,true);button.HorizontalAlignment=HorizontalAlignment.Right;content.Children.Add(button);dialog=Ui.Dialog(this,title,content,420);dialog.Loaded+=(_,_)=>pw.Focus();var result=dialog.ShowDialog()==true?pw.Password:null;pw.Clear();return result;}
    public void AddNote()
    {
        if (!CanCreateRecord) return;
        var note = NoteCreation.InNotebook(VM.State!, VM.View);
        VM.Change(w => w with { Notes = [note, ..w.Notes] },"new-note"); Navigate(note.NotebookId, note.Id, true);_ = FocusNewRecord(note.Id);
    }
    private async Task FocusNewRecord(string id){await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Input);if(VM.SelectedId==id){titleBox?.Focus();titleBox?.SelectAll();}}
    private void NotebookDialog(Notebook? book=null)
    {
        var name=Ui.Input(book?.Name??""); name.Name="NotebookName";
        var modes=new ComboBox{ItemsSource=new[]{"普通文本","提示词","账号密码"},SelectedIndex=(int)(book?.Mode??NoteMode.Text),IsEnabled=book==null,Name="NotebookMode"};
        var chosenColor=book?.Color??"#75876D"; var chosenEnd=book?.ColorEnd??"";
        var chosenIcon=book==null?"Book":NativeIcon.ForNotebook(book); var gradient=chosenEnd.Length>0;
        var palette=new WrapPanel(); var icons=new WrapPanel(); var custom=new StackPanel{Orientation=Orientation.Horizontal};
        var preview=new Border{Height=62,CornerRadius=new CornerRadius(10),Padding=new Thickness(16),Margin=new Thickness(0,0,0,20)};
        Button? solidTab=null,gradientTab=null;
        void Refresh(){
            preview.Background=gradient?NotebookAppearance.Swatch(chosenColor,chosenEnd):new SolidColorBrush(NotebookAppearance.Mix(NotebookAppearance.Parse(chosenColor),Colors.White,.75));
            var sample=Ui.Row(new NativeIcon{Kind=chosenIcon,Width=26,Height=26,Foreground=Ui.Brush(NotebookAppearance.Ink(chosenColor))},Ui.Spaced(Ui.Text(string.IsNullOrWhiteSpace(name.Text)?"新记事本":name.Text,15,"#293226",true),new Thickness(12,0,0,0)));preview.Child=sample;
            foreach(Button button in icons.Children)button.Background=button.Tag?.ToString()==chosenIcon?Ui.Resource("AccentLight"):Brushes.Transparent;
            foreach(Button button in palette.Children)button.BorderThickness=button.Tag?.ToString()==chosenColor+"|"+chosenEnd?new Thickness(2):new Thickness(0);
            if(solidTab!=null)solidTab.Background=!gradient?Ui.Resource("AccentLight"):Brushes.Transparent;
            if(gradientTab!=null)gradientTab.Background=gradient?Ui.Resource("AccentLight"):Brushes.Transparent;
        }
        void RenderPalette(){
            palette.Children.Clear();custom.Children.Clear();var presets=gradient?NotebookAppearance.Gradients:NotebookAppearance.Solids;var index=0;
            foreach(var preset in presets){var value=preset;var button=Ui.Button("",()=>{chosenColor=value.Start;chosenEnd=value.End;Refresh();},tip:value.Name);
                button.Content=new Border{Width=47,Height=32,CornerRadius=new CornerRadius(7),Background=NotebookAppearance.Swatch(value.Start,value.End)};button.Width=59;button.Height=46;button.Padding=new Thickness(4);button.Margin=new Thickness(2);button.Tag=value.Start+"|"+value.End;button.Name=gradient?"NotebookGradient"+index:"NotebookColor"+value.Start.TrimStart('#');palette.Children.Add(button);index++;
            }
            custom.Children.Add(Ui.Button(gradient?"起始色":"自定义颜色",()=>PickNotebookColor(chosenColor,value=>{chosenColor=value;Refresh();})));
            if(gradient)custom.Children.Add(Ui.Spaced(Ui.Button("结束色",()=>PickNotebookColor(chosenEnd,value=>{chosenEnd=value;Refresh();})),new Thickness(8,0,0,0)));
            Refresh();
        }
        solidTab=Ui.Button("纯色",()=>{gradient=false;chosenEnd="";RenderPalette();});
        gradientTab=Ui.Button("渐变",()=>{if(!gradient){gradient=true;chosenColor=NotebookAppearance.Gradients[0].Start;chosenEnd=NotebookAppearance.Gradients[0].End;}RenderPalette();});
        foreach(var(icon,label)in NotebookAppearance.Icons){var value=icon;var button=Ui.Icon(icon,()=>{chosenIcon=value;Refresh();},label);button.Tag=icon;icons.Children.Add(button);}
        name.TextChanged+=(_,_)=>Refresh();
        var content=Ui.Stack(preview,Ui.Field("记事本名称",name),Ui.Field("记录模式",modes),Ui.Field("封面颜色",Ui.Stack(Ui.Spaced(Ui.Row(solidTab,gradientTab),new Thickness(0,0,0,8)),palette,Ui.Spaced(custom,new Thickness(2,8,0,0)))),Ui.Field("图标",icons));
        Window? dialog=null;
        var save=Ui.Button(book==null?"创建记事本":"保存修改",()=>{
            if(string.IsNullOrWhiteSpace(name.Text))return;
            if(book==null){var created=new Notebook{Name=name.Text.Trim(),Mode=(NoteMode)modes.SelectedIndex,Color=chosenColor,ColorEnd=chosenEnd,Icon=chosenIcon};VM.Change(v=>v with{Notebooks=[..v.Notebooks,created]});dialog!.Close();Navigate(created.Id);}
            else{VM.Change(v=>v with{Notebooks=v.Notebooks.Select(b=>b.Id==book.Id?b with{Name=name.Text.Trim(),Color=chosenColor,ColorEnd=chosenEnd,Icon=chosenIcon,UpdatedAt=Clock.Now,Revision=Clock.Id}:b).ToList()});dialog!.Close();}
        },true);
        var buttons=Ui.Row();if(book!=null)buttons.Children.Add(Ui.Button("删除记事本",()=>{dialog!.Close();DeleteNotebook(book);}));buttons.Children.Add(Ui.Spaced(save,new Thickness(12,0,0,0)));buttons.HorizontalAlignment=HorizontalAlignment.Right;buttons.Margin=new Thickness(0,12,0,0);
        var root=new DockPanel{Margin=new Thickness(24)};DockPanel.SetDock(buttons,Dock.Bottom);root.Children.Add(buttons);root.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        RenderPalette();dialog=Ui.Dialog(this,book==null?"新建记事本":"编辑记事本",root,540,Math.Min(800,SystemParameters.WorkArea.Height-60));dialog.Loaded+=(_,_)=>name.Focus();dialog.ShowDialog();
    }
    private void PickNotebookColor(string current,Action<string> apply)
    {
        var initial=NotebookAppearance.Parse(current);var preview=new Border{Height=52,CornerRadius=new CornerRadius(8),Background=new SolidColorBrush(initial)};var panel=Ui.Stack(preview);panel.Margin=new Thickness(24);var sliders=new[]{new Slider{Value=initial.R},new Slider{Value=initial.G},new Slider{Value=initial.B}};
        string Value()=>Color.FromRgb((byte)sliders[0].Value,(byte)sliders[1].Value,(byte)sliders[2].Value).ToString();
        for(var i=0;i<sliders.Length;i++){var slider=sliders[i];slider.Minimum=0;slider.Maximum=255;slider.TickFrequency=1;slider.IsSnapToTickEnabled=true;slider.ValueChanged+=(_,_)=>preview.Background=Ui.Brush(Value());panel.Children.Add(Ui.Field(new[]{"红色","绿色","蓝色"}[i],slider));}
        Window? dialog=null;panel.Children.Add(Ui.Button("使用此颜色",()=>{apply(Value());dialog!.Close();},true));dialog=Ui.Dialog(this,"自定义颜色",panel,390);dialog.ShowDialog();
    }
    private void DeleteNotebook(Notebook book)
    {if(!Confirm("删除「"+book.Name+"」？","其中的记录会移到回收站，之后仍可恢复。","删除记事本",true))return;VM.Change(v=>v with{Notebooks=v.Notebooks.Select(b=>b.Id==book.Id?b with{DeletedAt=Clock.Now,UpdatedAt=Clock.Now,Revision=Clock.Id}:b).ToList(),Notes=v.Notes.Select(n=>n.NotebookId==book.Id&&n.DeletedAt==null?n.Apply(n with{DeletedAt=Clock.Now}):n).ToList()});Navigate("home");}
    private void HistoryDialog(string id)
    {
        var note=VM.State!.Notes.First(n=>n.Id==id);var grid=new Grid{Margin=new Thickness(20)};grid.ColumnDefinitions.Add(new(){Width=new GridLength(220)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});var list=new ListBox();var preview=new RichTextBox{IsReadOnly=true,BorderThickness=new Thickness(0),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};preview.Margin=new Thickness(15,0,0,15);foreach(var version in note.History)list.Items.Add(new ListBoxItem{Content=Ui.Stack(Ui.Text(DateTimeOffset.FromUnixTimeMilliseconds(version.At).LocalDateTime.ToString("MM-dd HH:mm:ss"),12),Ui.Text(version.Title,10,"Sub")),Tag=version});if(note.History.Count==0)list.Items.Add(new ListBoxItem{Content="编辑后会自动保留历史版本",IsEnabled=false});list.SelectionChanged+=(_,_)=>{if(list.SelectedItem is ListBoxItem{Tag:NoteVersion h})preview.Document=new FlowDocument(new Paragraph(new Run(h.Title+"\n\n"+RichDocument.PlainText(new Note{Content=h.Content,Variables=h.Variables}))));};grid.Children.Add(list);var right=new DockPanel();var restore=Ui.Button("恢复此版本",()=>{},true);restore.IsEnabled=VM.State.Notebooks.First(b=>b.Id==note.NotebookId).Mode!=NoteMode.Password||editingCredentialId==id;DockPanel.SetDock(restore,Dock.Bottom);right.Children.Add(restore);right.Children.Add(preview);Grid.SetColumn(right,1);grid.Children.Add(right);var dialog=Ui.Dialog(this,"版本历史 · 最近 40 个版本",grid,850,550);restore.Click+=(_,_)=>{if(list.SelectedItem is not ListBoxItem{Tag:NoteVersion h})return;VM.EditNote(id,n=>n with{Title=h.Title,Content=h.Content,Username=h.Username,Password=h.Password,Url=h.Url,Otp=h.Otp,Tags=[..h.Tags],Variables=[..h.Variables],Fields=[..h.Fields]},true);dialog.Close();renderedId="";RenderEditor(CurrentNote()!);Toast("历史版本已恢复");};if(list.Items.Count>0)list.SelectedIndex=0;dialog.ShowDialog();
    }
    private void PasswordGenerator(Action<string> apply)
    {
        var generated=Ui.Input(CryptoService.GeneratePassword());generated.FontFamily=new FontFamily("Consolas");generated.FontSize=18;generated.IsReadOnly=true;var length=new Slider{Minimum=12,Maximum=64,Value=20,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(0,8,0,10)};var symbols=new CheckBox{Content="包含特殊符号",IsChecked=true};var label=Ui.Text("密码长度 · 20",11);void Generate(){label.Text="密码长度 · "+(int)length.Value;generated.Text=CryptoService.GeneratePassword((int)length.Value,symbols.IsChecked==true);}length.ValueChanged+=(_,_)=>Generate();symbols.Checked+=(_,_)=>Generate();symbols.Unchecked+=(_,_)=>Generate();Window? dialog=null;var content=Ui.Stack(generated,Ui.Spaced(label,new Thickness(0,24,0,4)),length,symbols,Ui.Spaced(Ui.Row(Ui.Button("重新生成",Generate),Ui.Spaced(Ui.Button("复制",()=>Copy(generated.Text,true)),new Thickness(8,0,8,0)),Ui.Button("使用此密码",()=>{apply(generated.Text);dialog!.Close();},true)),new Thickness(0,20,0,0)));content.Margin=new Thickness(25);dialog=Ui.Dialog(this,"生成强密码",content,590);dialog.ShowDialog();
    }
    private void ExportNote(Note note){var save=new SaveFileDialog{Filter="普通文本|*.txt|HTML 文档|*.html",FileName=RecordTools.SafeName(note.Title)+".txt"};if(save.ShowDialog(this)!=true)return;File.WriteAllText(save.FileName,save.FilterIndex==1?RichDocument.PlainText(note):"<!doctype html><meta charset=\"utf-8\"><title>"+System.Net.WebUtility.HtmlEncode(note.Title)+"</title>"+EditorContent.Html(PromptVariables.Display(note)));Toast("记录已导出");}
    private async void PrintNote(Note note){if(sharedEditor!=null){await sharedEditor.Ready;sharedEditor.Browser.CoreWebView2.ShowPrintUI(Microsoft.Web.WebView2.Core.CoreWebView2PrintDialogKind.System);}}

    private async Task ImportFiles()
    {
        var open=new OpenFileDialog{Filter="文本与墨记备份|*.md;*.markdown;*.txt;*.moji;*.mojibak;*.json",Multiselect=true};if(open.ShowDialog(this)!=true)return;
        try{foreach(var path in open.FileNames){if(new FileInfo(path).Length>30_000_000)throw new InvalidDataException("导入文件请控制在 30 MB 以内");if(new[]{".moji",".mojibak",".json"}.Contains(Path.GetExtension(path).ToLowerInvariant())){var text=await File.ReadAllTextAsync(path); Workspace backup;
            using(var document=System.Text.Json.JsonDocument.Parse(text)){if(document.RootElement.TryGetProperty("format",out var format)&&format.GetString()=="moji-encrypted"){var envelope=Json.Deserialize<EncryptedEnvelope>(text);var password=AskPassword("旧备份密码");if(password==null)continue;using var key=await Task.Run(()=>CryptoService.Derive(password,envelope.Salt,envelope.Iterations));backup=CryptoService.Decrypt<Workspace>(envelope,key);}else backup=Json.Deserialize<Workspace>(text);}backup.Validate();if(!Confirm("读取这份存档？","将替换本机全部记事本和记录。"))continue;await VM.ReplaceDataAsync(GameSaves.Capture(backup));}else{var book=VM.State!.LiveBooks.FirstOrDefault(b=>b.Id==VM.View&&b.Mode!=NoteMode.Password)??ChooseImportNotebook();if(book==null)continue;var note=new Note{NotebookId=book.Id,Title=Path.GetFileNameWithoutExtension(path),Content=await File.ReadAllTextAsync(path)};VM.Change(v=>v with{Notes=[..v.Notes,note]});Navigate(book.Id,note.Id);}}Toast("导入完成");}catch(Exception error){Toast(UserMessages.For(error));}
    }
    private Notebook? ChooseImportNotebook()
    {
        Notebook? selected=null;Window? dialog=null;var panel=new StackPanel{Margin=new Thickness(22)};
        foreach(var book in VM.State!.LiveBooks.Where(b=>b.Mode!=NoteMode.Password))panel.Children.Add(Ui.Button(book.Name,()=>{selected=book;dialog!.Close();}));
        if(panel.Children.Count==0)panel.Children.Add(Ui.Text("请先新建文本或提示词记事本",13));
        dialog=Ui.Dialog(this,"导入到记事本",panel,450);dialog.ShowDialog();return selected;
    }
    private async Task ExportBackup()
    {
        if(VM.State==null)return;var save=new SaveFileDialog{Filter="墨记存档|*.moji",FileName=$"墨记-存档-{DateTime.Now:yyyy-MM-dd}.moji"};if(save.ShowDialog(this)!=true)return;
        try{await File.WriteAllTextAsync(save.FileName,Json.Serialize(GameSaves.Capture(VM.State)));Toast("存档已导出");}catch(Exception e){Toast(UserMessages.For(e));}
    }
    private void ExportAllMarkdown(){var save=new SaveFileDialog{Filter="文本压缩包|*.zip",FileName=$"墨记-文本集-{DateTime.Now:yyyy-MM-dd}.zip"};if(save.ShowDialog(this)!=true)return;try{if(File.Exists(save.FileName))File.Delete(save.FileName);RecordTools.ExportTexts(VM.State!,save.FileName);Toast("文本集已导出，不包含密码库");}catch(Exception e){Toast(UserMessages.For(e));}}
    private void OpenUrl(string value){if(Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme is "https" or "http")Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});else Toast("请填写有效的 http 或 https 地址");}
}
