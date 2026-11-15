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
    private bool Confirm(string title,string text)
    { var content=new StackPanel{Margin=new Thickness(27)};content.Children.Add(Ui.Text(title,19,null,true));content.Children.Add(Ui.Spaced(Ui.Text(text,12,"#929D86"),new Thickness(0,18,0,25)));Window? dialog=null;var cancel=Ui.Button("取消",()=>dialog!.DialogResult=false);var accept=Ui.Button("确认",()=>dialog!.DialogResult=true,true);var buttons=Ui.Row(cancel,Ui.Spaced(accept,new Thickness(10,0,0,0)));buttons.HorizontalAlignment=HorizontalAlignment.Right;content.Children.Add(buttons);dialog=Ui.Dialog(this,"墨记",content,460);return dialog.ShowDialog()==true; }
    private string? AskPassword(string title)
    {var pw=new PasswordBox{MinWidth=300};var content=Ui.Stack(Ui.Field(title,pw));content.Margin=new Thickness(27);Window? dialog=null;var button=Ui.Button("解密并继续",()=>dialog!.DialogResult=true,true);button.HorizontalAlignment=HorizontalAlignment.Right;content.Children.Add(button);dialog=Ui.Dialog(this,title,content,420);dialog.Loaded+=(_,_)=>pw.Focus();var result=dialog.ShowDialog()==true?pw.Password:null;pw.Clear();return result;}
    public void AddNote(string? notebookId = null)
    {
        if (VM.State == null) return;
        notebookId ??= VM.State.LiveBooks.FirstOrDefault(b => b.Id == VM.View)?.Id;
        if (notebookId == null) { ChooseNotebook(); return; }
        var note = NoteCreation.InNotebook(VM.State, notebookId);
        VM.Change(w => w with { Notes = [note, ..w.Notes] }); Navigate(note.NotebookId, note.Id, true); titleBox?.Focus(); titleBox?.SelectAll();
    }
    private void ChooseNotebook()
    {
        var panel = new StackPanel { Margin = new Thickness(22) }; Window? dialog = null;
        foreach (var book in VM.State!.LiveBooks) {
            var button = Ui.Button("", () => { dialog!.Close(); AddNote(book.Id); });
            button.Content = Ui.Stack(Ui.Text(book.Name,14,null,true), Ui.Text(RecordTools.ModeName(book.Mode),11));
            button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Margin = new Thickness(0,0,0,9); panel.Children.Add(button);
        }
        panel.Children.Add(Ui.Button("新建记事本", () => { dialog!.Close(); NotebookDialog(); }));
        dialog = Ui.Dialog(this,"选择记事本",new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 500 },450); dialog.ShowDialog();
    }
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
    {if(!Confirm("删除「"+book.Name+"」？","其中的记录会移到回收站，之后仍可恢复。"))return;VM.Change(v=>v with{Notebooks=v.Notebooks.Select(b=>b.Id==book.Id?b with{DeletedAt=Clock.Now,UpdatedAt=Clock.Now,Revision=Clock.Id}:b).ToList(),Notes=v.Notes.Select(n=>n.NotebookId==book.Id&&n.DeletedAt==null?n.Apply(n with{DeletedAt=Clock.Now}):n).ToList()});Navigate("home");}
    private void SearchDialog()
    {
        var input=Ui.Input("","搜索记录");input.FontSize=17;var list=new ListBox{MaxHeight=470};Window? dialog=null;
        void Open(){if(list.SelectedItem is ListBoxItem item&&item.Tag is Note note){dialog!.Close();Navigate(note.NotebookId,note.Id);}}
        void Search(){list.Items.Clear();foreach(var note in VM.State!.LiveNotes.Where(n=>RecordTools.Matches(n,input.Text,VM.State.Notebooks.First(b=>b.Id==n.NotebookId).Mode)).OrderByDescending(n=>n.UpdatedAt).Take(80)){var book=VM.State.Notebooks.First(b=>b.Id==note.NotebookId);list.Items.Add(new ListBoxItem{Tag=note,Content=Ui.Stack(Ui.Text(note.Title,13),Ui.Spaced(Ui.Text(book.Name+"    "+RecordTools.Relative(note.UpdatedAt),10,"#9AA68C"),new Thickness(22,6,0,0)))});}if(list.Items.Count>0)list.SelectedIndex=0;}
        input.TextChanged+=(_,_)=>Search();input.KeyDown+=(_,e)=>{if(e.Key==Key.Down){e.Handled=true;list.Focus();}if(e.Key==Key.Enter){e.Handled=true;Open();}};list.MouseDoubleClick+=(_,_)=>Open();list.KeyDown+=(_,e)=>{if(e.Key==Key.Enter)Open();};var panel=Ui.Stack(input,list);panel.Margin=new Thickness(20);dialog=Ui.Dialog(this,"搜索",panel,700);Search();dialog.Loaded+=(_,_)=>input.Focus();dialog.ShowDialog();
    }
    private void InsertVariable()
    {
        if(CurrentNote() is not {} note || textEditor==null)return;
        var name=Ui.Input();var status=Ui.Text("",11);var panel=Ui.Stack(Ui.Field("变量名称",name),status);panel.Margin=new Thickness(25);Window? dialog=null;
        panel.Children.Add(Ui.Button("插入",()=>{try{var variable=PromptVariables.Named(note,name.Text);if(!note.Variables.Any(v=>v.Id==variable.Id))VM.EditNote(note.Id,n=>n with{Variables=[..n.Variables,variable]});
            textEditor.Selection.Text="";_ = new InlineUIContainer(MarkdownDocument.VariableChip(variable),textEditor.CaretPosition){Tag="image:moji-variable:"+variable.Id,BaselineAlignment=BaselineAlignment.Center};SaveRichText();dialog!.Close();textEditor.Focus();
        }catch(Exception error){status.Text=UserMessages.For(error);}},true));dialog=Ui.Dialog(this,"插入变量",panel,420);dialog.Loaded+=(_,_)=>name.Focus();dialog.ShowDialog();
    }
    private void PromptDialog(string id)
    {
        var note=VM.State!.Notes.First(n=>n.Id==id);var variables=PromptVariables.Used(note);var values=new Dictionary<string,string>();
        var panel=new StackPanel{Margin=new Thickness(24)};
        foreach(var variable in variables){var input=Ui.Input("",null,true);input.MinHeight=70;input.TextChanged+=(_,_)=>values[variable.Id]=input.Text;panel.Children.Add(Ui.Field(variable.Name,input));}
        panel.Children.Add(Ui.Button("复制提示词",()=>{if(variables.Any(v=>!values.TryGetValue(v.Id,out var value)||string.IsNullOrWhiteSpace(value))){Toast("请填写全部变量");return;}Copy(PromptVariables.PlainText(note,values,markdown=>{var document=MarkdownDocument.Render(markdown);return new TextRange(document.ContentStart,document.ContentEnd).Text.TrimEnd();}));},true));
        Ui.Dialog(this,"使用提示词",new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=650},600).ShowDialog();
    }
    private void HistoryDialog(string id)
    {
        var note=VM.State!.Notes.First(n=>n.Id==id);var grid=new Grid{Margin=new Thickness(20)};grid.ColumnDefinitions.Add(new(){Width=new GridLength(220)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});var list=new ListBox();var preview=new RichTextBox{IsReadOnly=true,BorderThickness=new Thickness(0),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};preview.Margin=new Thickness(15,0,0,15);foreach(var version in note.History)list.Items.Add(new ListBoxItem{Content=Ui.Stack(Ui.Text(DateTimeOffset.FromUnixTimeMilliseconds(version.At).LocalDateTime.ToString("MM-dd HH:mm:ss"),12),Ui.Text(version.Title,10,"#9AA68C")),Tag=version});if(note.History.Count==0)list.Items.Add(new ListBoxItem{Content="编辑后会自动保留历史版本",IsEnabled=false});list.SelectionChanged+=(_,_)=>{if(list.SelectedItem is ListBoxItem{Tag:NoteVersion h})preview.Document=MarkdownDocument.Render("# "+h.Title+"\n\n"+h.Content,15,h.Variables);};grid.Children.Add(list);var right=new DockPanel();var restore=Ui.Button("恢复此版本",()=>{},true);restore.IsEnabled=VM.State.Notebooks.First(b=>b.Id==note.NotebookId).Mode!=NoteMode.Password||editingCredentialId==id;DockPanel.SetDock(restore,Dock.Bottom);right.Children.Add(restore);right.Children.Add(preview);Grid.SetColumn(right,1);grid.Children.Add(right);var dialog=Ui.Dialog(this,"版本历史 · 最近 40 个版本",grid,850,550);restore.Click+=(_,_)=>{if(list.SelectedItem is not ListBoxItem{Tag:NoteVersion h})return;VM.EditNote(id,n=>n with{Title=h.Title,Content=h.Content,Username=h.Username,Password=h.Password,Url=h.Url,Otp=h.Otp,Tags=[..h.Tags],Variables=[..h.Variables]},true);dialog.Close();renderedId="";RenderEditor(CurrentNote()!);Toast("历史版本已恢复");};if(list.Items.Count>0)list.SelectedIndex=0;dialog.ShowDialog();
    }
    private void PasswordGenerator(Action<string> apply)
    {
        var generated=Ui.Input(CryptoService.GeneratePassword());generated.FontFamily=new FontFamily("Consolas");generated.FontSize=18;generated.IsReadOnly=true;var length=new Slider{Minimum=12,Maximum=64,Value=20,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(0,8,0,10)};var symbols=new CheckBox{Content="包含特殊符号",IsChecked=true};var label=Ui.Text("密码长度 · 20",11);void Generate(){label.Text="密码长度 · "+(int)length.Value;generated.Text=CryptoService.GeneratePassword((int)length.Value,symbols.IsChecked==true);}length.ValueChanged+=(_,_)=>Generate();symbols.Checked+=(_,_)=>Generate();symbols.Unchecked+=(_,_)=>Generate();Window? dialog=null;var content=Ui.Stack(generated,Ui.Spaced(label,new Thickness(0,24,0,4)),length,symbols,Ui.Spaced(Ui.Row(Ui.Button("重新生成",Generate),Ui.Spaced(Ui.Button("复制",()=>Copy(generated.Text,true)),new Thickness(8,0,8,0)),Ui.Button("使用此密码",()=>{apply(generated.Text);dialog!.Close();},true)),new Thickness(0,20,0,0)));content.Margin=new Thickness(25);dialog=Ui.Dialog(this,"生成强密码",content,590);dialog.ShowDialog();
    }
    private void FindReplaceDialog()
    {
        if(textEditor==null)return;var editor=textEditor;var find=Ui.Input(editor.Selection.Text);var replace=Ui.Input();var matchCase=new CheckBox{Content="区分大小写"};var status=Ui.Text("",11);int cursor=0;
        void Run(bool all){try {if(find.Text.Length==0)return;var map=RichDocument.TextMap(editor.Document);var pattern=new Regex(Regex.Escape(find.Text),matchCase.IsChecked==true?RegexOptions.None:RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(200));
            if(all){var matches=pattern.Matches(map.Text).Cast<Match>().ToList();editor.BeginChange();try{foreach(var match in matches.AsEnumerable().Reverse())new TextRange(map.Positions[match.Index],map.Positions[match.Index+match.Length]).Text=replace.Text;}finally{editor.EndChange();}status.Text=$"已替换 {matches.Count} 处";}
            else {var match=pattern.Match(map.Text,Math.Min(cursor,map.Text.Length));if(!match.Success)match=pattern.Match(map.Text);if(!match.Success){status.Text="没有找到匹配内容";return;}editor.Selection.Select(map.Positions[match.Index],map.Positions[match.Index+match.Length]);cursor=match.Index+match.Length;editor.Selection.Start.Paragraph?.BringIntoView();status.Text="已选中匹配内容";}
        }catch(Exception error){status.Text=UserMessages.For(error);}}
        find.TextChanged+=(_,_)=>cursor=0;
        var panel=Ui.Stack(Ui.Field("查找",find),Ui.Field("替换为",replace),matchCase,Ui.Spaced(Ui.Row(Ui.Button("下一个",()=>Run(false)),Ui.Button("替换所选",()=>{if(string.Equals(editor.Selection.Text,find.Text,matchCase.IsChecked==true?StringComparison.Ordinal:StringComparison.OrdinalIgnoreCase))editor.Selection.Text=replace.Text;}),Ui.Button("全部替换",()=>Run(true),true)),new Thickness(0,15,0,15)),status);panel.Margin=new Thickness(25);var dialog=Ui.Dialog(this,"查找与替换",panel,570);dialog.Loaded+=(_,_)=>find.Focus();dialog.ShowDialog();
    }
    private void InsertLink()
    {
        if(textEditor==null)return;var editor=textEditor;var existing=ActiveLink();var url=Ui.Input(existing?.NavigateUri?.AbsoluteUri??"https://");var label=Ui.Input(existing!=null?new TextRange(existing.ContentStart,existing.ContentEnd).Text:editor.Selection.Text);var panel=Ui.Stack(Ui.Field("显示文字",label),Ui.Field("链接地址",url));panel.Margin=new Thickness(25);Window? dialog=null;
        panel.Children.Add(Ui.Button("插入",()=>{if(!Uri.TryCreate(url.Text,UriKind.Absolute,out var uri)||uri.Scheme is not("http" or "https"))return;editor.BeginChange();try{if(existing!=null){existing.NavigateUri=uri;existing.Inlines.Clear();existing.Inlines.Add(new Run(label.Text.Length>0?label.Text:uri.Host));}else{editor.Selection.Text="";_ = new Hyperlink(new Run(label.Text.Length>0?label.Text:uri.Host),editor.CaretPosition){NavigateUri=uri,Foreground=Ui.Resource("Accent")};}}finally{editor.EndChange();}SaveRichText();dialog!.Close();editor.Focus();},true));dialog=Ui.Dialog(this,"插入链接",panel);dialog.ShowDialog();
    }
    private void InsertImage()
    {
        var open=new OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg;*.gif;*.bmp"};if(open.ShowDialog(this)!=true)return;
        try{var file=new FileInfo(open.FileName);if(file.Length>2_000_000)throw new InvalidDataException("单张图片请控制在 2 MB 以内");var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.StreamSource=new MemoryStream(File.ReadAllBytes(file.FullName));bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.EndInit();bitmap.Freeze();EmbedBitmap(bitmap);}catch(Exception error){Toast(UserMessages.For(error));}
    }
    private void EmbedBitmap(BitmapSource bitmap)
    {
        if(textEditor==null)return;try{var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);if(stream.Length>2_000_000)throw new InvalidDataException("图片请控制在 2 MB 以内");var uri="data:image/png;base64,"+Convert.ToBase64String(stream.ToArray());textEditor.Selection.Text="";_ = new InlineUIContainer(new Image{Source=bitmap,MaxWidth=640,MaxHeight=450,Stretch=Stretch.Uniform},textEditor.CaretPosition){Tag="image:"+uri};SaveRichText();}catch(Exception error){Toast(UserMessages.For(error));}
    }
    private void ExportNote(Note note){var save=new SaveFileDialog{Filter="Markdown|*.md|普通文本|*.txt|富文本|*.rtf",FileName=RecordTools.SafeName(note.Title)+".md"};if(save.ShowDialog(this)!=true)return;if(save.FilterIndex==3){var doc=MarkdownDocument.Render(note.Content,15,note.Variables);using var stream=File.Create(save.FileName);new TextRange(doc.ContentStart,doc.ContentEnd).Save(stream,DataFormats.Rtf);}else File.WriteAllText(save.FileName,save.FilterIndex==2?RichDocument.PlainText(note):PromptVariables.Fill(note,note.Variables.ToDictionary(v=>v.Id,v=>RichDocument.Escape(v.Name))));Toast("记录已导出");}
    private void PrintNote(Note note){var dialog=new PrintDialog();if(dialog.ShowDialog()!=true)return;var document=MarkdownDocument.Render("# "+RichDocument.Escape(note.Title)+"\n\n"+note.Content,15,note.Variables);document.PageWidth=dialog.PrintableAreaWidth;document.PageHeight=dialog.PrintableAreaHeight;document.ColumnWidth=dialog.PrintableAreaWidth;dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator,note.Title);}
    private async Task ImportFiles()
    {
        var open=new OpenFileDialog{Filter="文本与墨记备份|*.md;*.markdown;*.txt;*.moji;*.mojibak;*.json",Multiselect=true};if(open.ShowDialog(this)!=true)return;
        try{foreach(var path in open.FileNames){if(new FileInfo(path).Length>30_000_000)throw new InvalidDataException("导入文件请控制在 30 MB 以内");if(new[]{".moji",".mojibak",".json"}.Contains(Path.GetExtension(path).ToLowerInvariant())){var text=await File.ReadAllTextAsync(path); Workspace backup;
            using(var document=System.Text.Json.JsonDocument.Parse(text)){if(document.RootElement.TryGetProperty("format",out var format)&&format.GetString()=="moji-encrypted"){var envelope=Json.Deserialize<EncryptedEnvelope>(text);var password=AskPassword("旧备份密码");if(password==null)continue;using var key=await Task.Run(()=>CryptoService.Derive(password,envelope.Salt,envelope.Iterations));backup=CryptoService.Decrypt<Workspace>(envelope,key);}else backup=Json.Deserialize<Workspace>(text);}backup.Validate();if(!Confirm("读取这份存档？","将替换本机全部记事本和记录。"))continue;await VM.ReplaceDataAsync(GameSaves.Capture(backup));}else{var book=VM.State!.LiveBooks.FirstOrDefault(b=>b.Id==VM.View&&b.Mode!=NoteMode.Password)??ChooseImportNotebook();if(book==null)continue;var note=new Note{NotebookId=book.Id,Title=Path.GetFileNameWithoutExtension(path),Content=Path.GetExtension(path).Equals(".txt",StringComparison.OrdinalIgnoreCase)?RichDocument.Escape(await File.ReadAllTextAsync(path)):await File.ReadAllTextAsync(path)};VM.Change(v=>v with{Notes=[..v.Notes,note]});Navigate(book.Id,note.Id);}}Toast("导入完成");}catch(Exception error){Toast(UserMessages.For(error));}
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
    private void ExportAllMarkdown(){var save=new SaveFileDialog{Filter="Markdown 压缩包|*.zip",FileName=$"墨记-文本集-{DateTime.Now:yyyy-MM-dd}.zip"};if(save.ShowDialog(this)!=true)return;try{if(File.Exists(save.FileName))File.Delete(save.FileName);RecordTools.ExportMarkdown(VM.State!,save.FileName);Toast("文本集已导出，不包含密码库");}catch(Exception e){Toast(UserMessages.For(e));}}
    private void OpenUrl(string value){if(Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme is "https" or "http")Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});else Toast("请填写有效的 http 或 https 地址");}
}
