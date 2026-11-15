using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Moji.Core;

namespace Moji.App;
public partial class MainWindow
{
    private void RenderWorkspace()
    {
        var root = new Grid(); root.ColumnDefinitions.Add(new() { Width = new GridLength(focusMode ? 0 : 285) }); root.ColumnDefinitions.Add(new() { Width = new GridLength(5) }); root.ColumnDefinitions.Add(new() { Width = new GridLength(1,GridUnitType.Star) });
        var left = new DockPanel { Background = Ui.Resource("Side") }; root.Children.Add(left);
        var header = new StackPanel { Margin = new Thickness(19,23,19,10) }; var title = new DockPanel(); var options = Ui.Icon("More", () => { }, "更多操作"); options.Name = "ListOptions"; options.Click += (_,_) => { options.ContextMenu = BuildListMenu(); options.ContextMenu.PlacementTarget = options; options.ContextMenu.IsOpen = true; }; DockPanel.SetDock(options,Dock.Right); title.Children.Add(options); title.Children.Add(Ui.Text(ViewTitle(),17,null,true)); header.Children.Add(title);
        var search = Ui.Input(VM.Search,"搜索记录"); search.Padding=new Thickness(34,10,12,10); search.FontSize=11; search.TextChanged += (_,_)=> { VM.Search=search.Text; RefreshNoteList(); }; var searchRow=new Grid{Margin=new Thickness(0,17,0,10)}; searchRow.Children.Add(search); searchRow.Children.Add(new NativeIcon{Kind="Search",Width=15,Height=15,Foreground=Ui.Resource("Sub"),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,0,0),IsHitTestVisible=false}); header.Children.Add(searchRow);
        var sort = new ComboBox { ItemsSource = new[] { "最近编辑", "创建时间", "名称顺序" }, SelectedIndex = VM.Sort=="created"?1:VM.Sort=="name"?2:0, FontSize=10, MinWidth=110, BorderThickness=new Thickness(0), Background=Brushes.Transparent }; sort.SelectionChanged+=(_,_)=> { VM.Sort=sort.SelectedIndex==1?"created":sort.SelectedIndex==2?"name":"updated";RefreshNoteList();};
        var tools=Ui.Row(sort,Ui.Icon("Check",()=> { if(noteList!=null){noteList.SelectionMode=noteList.SelectionMode==SelectionMode.Extended?SelectionMode.Multiple:SelectionMode.Extended;Toast("按 Ctrl 或 Shift 可多选记录");} },"批量选择"),Ui.Icon("Trash",()=>DeleteSelection(),"删除所选记录"),Ui.Icon("Plus",()=>AddNote(),"新建记录")); header.Children.Add(tools); DockPanel.SetDock(header,Dock.Top);left.Children.Add(header);
        noteList = new ListBox { ItemsSource=noteRows, SelectionMode=SelectionMode.Extended };
        var template = "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel><TextBlock Text='{Binding Title}' FontSize='12' FontWeight='Medium' TextTrimming='CharacterEllipsis'/><TextBlock Text='{Binding Snippet}' FontSize='10' Foreground='{DynamicResource Sub}' Margin='0,10,0,12' TextWrapping='Wrap' MaxHeight='40'/><TextBlock Text='{Binding Meta}' FontSize='9' Foreground='{DynamicResource Sub}'/></StackPanel></DataTemplate>";
        noteList.ItemTemplate=(DataTemplate)XamlReader.Parse(template); noteList.SelectionChanged+=(_,_)=> { if(noteList.SelectedItem is NoteRow row){if(VM.SelectedId!=row.Note.Id)editingCredentialId="";VM.SelectedId=row.Note.Id;RenderEditor(row.Note);} };
        noteList.PreviewMouseMove+=(_,e)=> { if(e.LeftButton==MouseButtonState.Pressed && noteList.SelectedItem is NoteRow row && Keyboard.Modifiers==ModifierKeys.Alt) DragDrop.DoDragDrop(noteList,new DataObject("MojiNote",row.Note.Id),DragDropEffects.Move); };
        noteList.ContextMenu = new ContextMenu(); noteList.ContextMenuOpening += (_,_) => noteList.ContextMenu = BuildListMenu();left.Children.Add(noteList);
        root.Children.Add(new GridSplitter{Width=5,HorizontalAlignment=HorizontalAlignment.Stretch,Background=Ui.Resource("Line"),ResizeDirection=GridResizeDirection.Columns});Grid.SetColumn(root.Children[^1],1);
        editorHost=new ContentControl();Grid.SetColumn(editorHost,2);root.Children.Add(editorHost);Body.Content=root;renderedId="";RefreshNoteList();
    }
    private void RefreshNoteList()
    {
        if(noteList==null)return;var selected=VM.SelectedId;noteRows.Clear();foreach(var n in VM.Filtered())noteRows.Add(new(n));var row=noteRows.FirstOrDefault(n=>n.Note.Id==selected)??noteRows.FirstOrDefault();
        if(row!=null){noteList.SelectedItem=row;if(VM.SelectedId!=row.Note.Id)editingCredentialId="";VM.SelectedId=row.Note.Id;RenderEditor(row.Note);}else{VM.SelectedId="";renderedId="";textEditor=null;titleBox=null;otpText=null;editorHost!.Content=new Border{Background=Ui.Resource("Panel"),Child=Ui.Spaced(Ui.Stack(Ui.Text("暂无记录",20,"#99A58B"),Ui.Spaced(Ui.Button("+  新建记录",()=>AddNote()),new Thickness(0,25,0,0))),new Thickness(60,100,60,30))};}
    }
    public void RenderEditor(Note note)
    {
        if(editorHost==null || renderedId==note.Id)return;renderedId=note.Id;loadingEditor=true;textEditor=null;titleBox=null;otpText=null;
        if(VM.View=="trash"){var panel=new StackPanel{Margin=new Thickness(50,75,50,30)};panel.Children.Add(new NativeIcon { Kind="Trash", Width=42,Height=42 });panel.Children.Add(Ui.Spaced(Ui.Text(note.Title,25,null,true),new Thickness(0,24,0,15)));panel.Children.Add(Ui.Text("删除于 "+DateTimeOffset.FromUnixTimeMilliseconds(note.DeletedAt!.Value).LocalDateTime.ToString("yyyy-MM-dd HH:mm"),11,"#96A188"));panel.Children.Add(Ui.Spaced(Ui.Row(Ui.Button("恢复记录",()=>RestoreNotes([note.Id]),true),Ui.Spaced(Ui.Button("永久删除",()=>Purge([note.Id])),new Thickness(10,0,0,0))),new Thickness(0,25,0,20)));editorHost.Content=new Border{Background=Ui.Resource("Panel"),Child=panel};loadingEditor=false;return;}
        var book=VM.State!.Notebooks.First(b=>b.Id==note.NotebookId);var container=new DockPanel{Background=Ui.Resource("Panel")};
        var top=new DockPanel{Margin=new Thickness(27,15,22,15)};var actions=Ui.Row(Ui.Icon("Star",()=> {VM.EditNote(note.Id,n=>n with{Favorite=!n.Favorite});renderedId="";RenderEditor(CurrentNote()!);},"收藏"),Ui.Icon("Restore",()=>HistoryDialog(note.Id),"版本历史"),Ui.Icon("Focus",ToggleFocus,"专注模式"));
        if (book.Mode == NoteMode.Password) {
            var edit = Ui.Button(editingCredentialId == note.Id ? "完成" : "编辑密码", () => {
                editingCredentialId = editingCredentialId == note.Id ? "" : note.Id; renderedId = ""; RenderEditor(CurrentNote()!); _ = VM.SaveAsync();
            }, true); edit.Name = "EditCredential"; edit.Margin = new Thickness(0,0,10,0); actions.Children.Insert(0,edit);
        }
        var more=Ui.Icon("More",()=>{},"更多操作");more.ToolTip="更多操作";more.Click+=(_,_)=> {more.ContextMenu=NoteMenu(note.Id);more.ContextMenu.IsOpen=true;};actions.Children.Add(more);DockPanel.SetDock(actions,Dock.Right);top.Children.Add(actions);top.Children.Add(Ui.Text(book.Name,10,book.Color));DockPanel.SetDock(top,Dock.Top);container.Children.Add(top);
        var heading=new StackPanel{Margin=new Thickness(28,8,28,22)};
        titleBox=Ui.Input(note.Title,"记录标题");titleBox.IsReadOnly=book.Mode==NoteMode.Password&&editingCredentialId!=note.Id;titleBox.Name="RecordTitle";titleBox.FontSize=25;titleBox.FontWeight=FontWeights.SemiBold;titleBox.Background=Brushes.Transparent;titleBox.BorderThickness=new Thickness(0);titleBox.Padding=new Thickness(0,13,0,12);titleBox.TextChanged+=(_,_)=>{if(!loadingEditor&&!titleBox.IsReadOnly)VM.EditNote(note.Id,n=>n with{Title=titleBox.Text});};heading.Children.Add(titleBox);
        DockPanel.SetDock(heading,Dock.Top);container.Children.Add(heading);
        if(book.Mode==NoteMode.Password){container.Children.Add(editingCredentialId == note.Id ? CredentialView(note) : CredentialReadView(note));editorHost.Content=container;loadingEditor=false;return;}
        var toolbar = new WrapPanel { Margin = new Thickness(14,5,14,5) };
        void Tool(string icon,string label,Action action) { var button=Ui.Icon(icon,action,label); button.Focusable=false; toolbar.Children.Add(button); }
        Tool("Heading","标题",()=>SetHeading()); Tool("Bold","加粗",()=>EditingCommands.ToggleBold.Execute(null,textEditor)); Tool("Italic","斜体",()=>EditingCommands.ToggleItalic.Execute(null,textEditor));
        Tool("Strike","删除线",()=>{if(textEditor!=null){var current=textEditor.Selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;textEditor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty,current?.Any(t=>t.Location==TextDecorationLocation.Strikethrough)==true?new TextDecorationCollection():TextDecorations.Strikethrough);}});
        Tool("Bullet","项目列表",()=>EditingCommands.ToggleBullets.Execute(null,textEditor)); Tool("Ordered","编号列表",()=>EditingCommands.ToggleNumbering.Execute(null,textEditor));
        Tool("Checklist","待办事项",InsertTask);Tool("Quote","引用",InsertQuote);
        Tool("Link","插入链接",InsertLink); Tool("Image","插入图片",InsertImage); Tool("Table","插入表格",InsertTable);
        Tool("Undo","撤销",()=>RichUndo()); Tool("Redo","重做",()=>RichUndo(true)); Tool("Find","查找与替换",FindReplaceDialog);
        var tools=new Border { Child=toolbar, Background=Ui.Resource("Side"), BorderBrush=Ui.Resource("Line"), BorderThickness=new Thickness(0,1,0,1) }; DockPanel.SetDock(tools,Dock.Top); container.Children.Add(tools);
        if(book.Mode==NoteMode.Prompt){var promptBar=new DockPanel{Margin=new Thickness(22,6,22,6)};var use=Ui.Button("使用提示词",()=>PromptDialog(note.Id),true);var insert=Ui.Button("插入变量",InsertVariable);DockPanel.SetDock(insert,Dock.Left);promptBar.Children.Add(insert);DockPanel.SetDock(use,Dock.Right);promptBar.Children.Add(use);container.Children.Add(promptBar);DockPanel.SetDock(promptBar,Dock.Top);}
        tableTools=BuildTableTools();DockPanel.SetDock(tableTools,Dock.Top);container.Children.Add(tableTools);
        editorStatus=Ui.Text("",11); editorStatus.Margin=new Thickness(24,8,24,8); DockPanel.SetDock(editorStatus,Dock.Bottom);container.Children.Add(editorStatus);
        textEditor=new RichTextBox { Document=MarkdownDocument.Render(note.Content,VM.State.Preferences.FontSize,note.Variables), IsReadOnly=false, IsDocumentEnabled=true, AcceptsTab=true, BorderThickness=new Thickness(0), Background=Ui.Resource("Panel"), Foreground=Ui.Resource("Ink"), VerticalScrollBarVisibility=ScrollBarVisibility.Auto, UndoLimit=100, Name="RichNoteEditor" };
        textEditor.TextChanged+=(_,_)=>SaveRichText();textEditor.SelectionChanged+=(_,_)=>UpdateRichSelection();WireTasks();
        DataObject.AddPastingHandler(textEditor,(_,e)=>{if(e.DataObject.GetDataPresent(DataFormats.Bitmap)&&e.DataObject.GetData(DataFormats.Bitmap) is System.Windows.Media.Imaging.BitmapSource bitmap){e.CancelCommand();EmbedBitmap(bitmap);}});
        textEditor.AddHandler(CheckBox.CheckedEvent,new RoutedEventHandler((_,_)=>SaveRichText())); textEditor.AddHandler(CheckBox.UncheckedEvent,new RoutedEventHandler((_,_)=>SaveRichText()));
        container.Children.Add(textEditor);editorHost.Content=container;loadingEditor=false;UpdateEditorStatus();
    }
    private void SaveRichText()
    {
        if(loadingEditor||textEditor==null||CurrentNote() is not {} note)return;
        var content=RichDocument.Serialize(textEditor.Document);if(content!=note.Content){if(!richUndo.ContainsKey(note.Id))richUndo[note.Id]=[];var history=richUndo[note.Id];history.Add(note.Content);if(history.Count>100)history.RemoveAt(0);richRedo.Remove(note.Id);VM.EditNote(note.Id,n=>n with{Content=content});}UpdateEditorStatus();
    }
    private void UpdateEditorStatus(){if(editorStatus!=null&&textEditor!=null)editorStatus.Text=$"{new TextRange(textEditor.Document.ContentStart,textEditor.Document.ContentEnd).Text.Count(c=>!char.IsWhiteSpace(c)):N0} 字";}
    private void SetHeading()
    {
        if(textEditor==null)return;var p=textEditor.CaretPosition.Paragraph;if(p==null)return;
        textEditor.BeginChange();try { var heading=p.Tag is not int;p.Tag=heading?2:null;p.FontSize=VM.State!.Preferences.FontSize*(heading?1.4:1);p.FontWeight=heading?FontWeights.SemiBold:FontWeights.Normal; }finally{textEditor.EndChange();}SaveRichText();textEditor.Focus();
    }
    private void InsertTable()
    {
        if(textEditor==null)return;var rows=new ComboBox{ItemsSource=Enumerable.Range(1,20),SelectedItem=3};var cols=new ComboBox{ItemsSource=Enumerable.Range(1,10),SelectedItem=2};var panel=Ui.Stack(Ui.Field("行数",rows),Ui.Field("列数",cols));panel.Margin=new Thickness(24);Window? dialog=null;
        panel.Children.Add(Ui.Button("插入",()=>{var table=new Table{CellSpacing=0,Margin=new Thickness(0,12,0,12)};var group=new TableRowGroup();for(var i=0;i<(int)rows.SelectedItem;i++){var row=new TableRow();for(var j=0;j<(int)cols.SelectedItem;j++)row.Cells.Add(NewCell());group.Rows.Add(row);}table.RowGroups.Add(group);
            textEditor.BeginChange();try{var p=textEditor.CaretPosition.Paragraph;if(p?.Parent==textEditor.Document)textEditor.Document.Blocks.InsertAfter(p,table);else textEditor.Document.Blocks.Add(table);textEditor.Document.Blocks.Add(new Paragraph());textEditor.CaretPosition=group.Rows[0].Cells[0].ContentStart;}finally{textEditor.EndChange();}SaveRichText();dialog!.Close();textEditor.Focus();},true));dialog=Ui.Dialog(this,"插入表格",panel,420);dialog.ShowDialog();
    }
    private UIElement CredentialReadView(Note note)
    {
        var fields = new StackPanel { Margin = new Thickness(50,25,50,35), MaxWidth = 650 };
        TextBox Value(string value, string name, bool multi = false) { var box = Ui.Input(value,null,multi); box.Name = name; box.IsReadOnly = true; return box; }
        fields.Children.Add(Ui.Field("账号 / 邮箱", InputActions(Value(note.Username,"AccountValue"),Ui.Small("复制",()=>Copy(note.Username)))));
        var password = Value("••••••••", "PasswordValue");
        var reveal = Ui.Small("显示", () => { password.Text = password.Text == "••••••••" ? note.Password : "••••••••"; if(password.Text == note.Password) { var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) }; timer.Tick += (_,_) => { timer.Stop(); password.Text = "••••••••"; }; timer.Start(); } });
        fields.Children.Add(Ui.Field("密码",InputActions(password,reveal,Ui.Small("复制",()=>Copy(note.Password,true)))));
        fields.Children.Add(Ui.Field("网站",InputActions(Value(note.Url,"WebsiteValue"),Ui.Small("打开",()=>OpenUrl(note.Url)))));
        if (note.Otp.Length > 0) { otpText = Ui.Text("",23); fields.Children.Add(Ui.Field("动态验证码",InputActions(otpText,Ui.Small("复制",()=>{try{Copy(RecordTools.Totp(note.Otp),true);}catch(Exception e){Toast(e.Message);}})))); }
        fields.Children.Add(Ui.Field("备注",Value(note.Content,"MemoValue",true)));
        return new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
    private UIElement CredentialView(Note note)
    {
        var fields=new StackPanel{Margin=new Thickness(50,25,50,35),MaxWidth=650};
        var user=Ui.Input(note.Username);user.TextChanged+=(_,_)=>{if(!loadingEditor)VM.EditNote(note.Id,n=>n with{Username=user.Text});};fields.Children.Add(Ui.Field("账号 / 邮箱",InputActions(user,Ui.Small("复制",()=>Copy(user.Text)))));
        var pw=new PasswordBox{Password=note.Password};var revealed=Ui.Input(note.Password);revealed.Visibility=Visibility.Collapsed;var pwStack=new Grid();pwStack.Children.Add(pw);pwStack.Children.Add(revealed);bool updating=false;
        pw.PasswordChanged+=(_,_)=>{if(loadingEditor||updating)return;updating=true;revealed.Text=pw.Password;updating=false;VM.EditNote(note.Id,n=>n with{Password=pw.Password});};revealed.TextChanged+=(_,_)=>{if(loadingEditor||updating)return;updating=true;pw.Password=revealed.Text;updating=false;VM.EditNote(note.Id,n=>n with{Password=revealed.Text});};
        var reveal=Ui.Small("显示",()=>{var show=pw.Visibility==Visibility.Visible;pw.Visibility=show?Visibility.Collapsed:Visibility.Visible;revealed.Visibility=show?Visibility.Visible:Visibility.Collapsed;if(show){var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(20)};timer.Tick+=(_,_)=>{timer.Stop();pw.Visibility=Visibility.Visible;revealed.Visibility=Visibility.Collapsed;};timer.Start();}},"20 秒后自动隐藏");
        fields.Children.Add(Ui.Field("密码",InputActions(pwStack,reveal,Ui.Small("复制",()=>Copy(pw.Password,true)),Ui.Small("生成",()=>PasswordGenerator(value=>{pw.Password=value;})))));
        var strength=Ui.Text("密码强度："+new[]{"较弱","一般","良好","很强","很强"}[RecordTools.Strength(note.Password)],10,"#9AA88B");strength.Margin=new Thickness(0,-9,0,21);fields.Children.Add(strength);pw.PasswordChanged+=(_,_)=>strength.Text="密码强度："+new[]{"较弱","一般","良好","很强","很强"}[RecordTools.Strength(pw.Password)];
        var url=Ui.Input(note.Url);url.TextChanged+=(_,_)=>{if(!loadingEditor)VM.EditNote(note.Id,n=>n with{Url=url.Text});};fields.Children.Add(Ui.Field("网站",InputActions(url,Ui.Small("打开",()=>OpenUrl(url.Text)))));
        var otp=new PasswordBox{Password=note.Otp};otp.PasswordChanged+=(_,_)=>{if(!loadingEditor)VM.EditNote(note.Id,n=>n with{Otp=otp.Password});};fields.Children.Add(Ui.Field("动态验证码密钥（TOTP，可选）",otp));otpText=Ui.Text("",23,"#8AA273");fields.Children.Add(InputActions(otpText,Ui.Small("复制",()=>{try{Copy(RecordTools.Totp(otp.Password),true);}catch(Exception e){Toast(e.Message);}})));var memo=Ui.Input(note.Content,null,true);memo.MinHeight=110;memo.Margin=new Thickness(0,7,0,0);memo.TextChanged+=(_,_)=>{if(!loadingEditor)VM.EditNote(note.Id,n=>n with{Content=memo.Text});};fields.Children.Add(Ui.Spaced(Ui.Field("备注",memo),new Thickness(0,22,0,0)));fields.Children.Add(Ui.Small("密码健康检查",PasswordAudit));return new ScrollViewer{Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    }
    private static DockPanel InputActions(UIElement input,params Button[] buttons){var panel=new DockPanel();foreach(var b in buttons.Reverse()){DockPanel.SetDock(b,Dock.Right);panel.Children.Add(b);}panel.Children.Add(input);return panel;}
    private ContextMenu NoteMenu(string id)
    {
        var note=VM.State!.Notes.First(n=>n.Id==id);var menu=new ContextMenu();menu.Items.Add(MenuItem(note.Pinned?"取消置顶":"置顶记录",()=>{VM.EditNote(id,n=>n with{Pinned=!n.Pinned});RefreshNoteList();}));menu.Items.Add(MenuItem("创建副本",()=>{var copy=note with{Id=Clock.Id,Title=note.Title+" · 副本",History=[],CreatedAt=Clock.Now,UpdatedAt=Clock.Now,Revision=Clock.Id};VM.Change(v=>v with{Notes=[copy,..v.Notes]});Navigate(copy.NotebookId,copy.Id);}));menu.Items.Add(MenuItem(note.Archived?"取消归档":"归档记录",()=>{VM.EditNote(id,n=>n with{Archived=!n.Archived});RefreshNoteList();}));
        var move=new MenuItem{Header="移动到记事本"};var mode=VM.State.Notebooks.First(b=>b.Id==note.NotebookId).Mode;foreach(var book in VM.State.LiveBooks.Where(b=>b.Mode==mode&&b.Id!=note.NotebookId))move.Items.Add(MenuItem(book.Name,()=>{VM.EditNote(id,n=>n with{NotebookId=book.Id});Navigate(book.Id,id);}));menu.Items.Add(move);
        if(mode!=NoteMode.Password){menu.Items.Add(MenuItem("导出",()=>ExportNote(note)));menu.Items.Add(MenuItem("打印 / 保存 PDF",()=>PrintNote(note)));menu.Items.Add(MenuItem("复制全文",()=>Copy(RichDocument.PlainText(note))));}menu.Items.Add(MenuItem("保存当前版本",()=>{VM.Checkpoint(id);Toast("版本已保存");}));menu.Items.Add(new Separator());menu.Items.Add(MenuItem("移到回收站",()=>TrashNotes([id])));return menu;
    }
    internal ContextMenu BuildListMenu()
    {
        var menu = new ContextMenu(); var selected = SelectedIds().Count > 0;
        void Add(string label, Action action, bool enabled = true) { var item = MenuItem(label, action); item.IsEnabled = enabled; menu.Items.Add(item); }
        var book = VM.State!.LiveBooks.FirstOrDefault(b => b.Id == VM.View);
        if (book != null) Add("编辑记事本", () => NotebookDialog(book));
        Add("全选", () => noteList?.SelectAll(), noteRows.Count > 0);
        Add("取消选择", () => noteList?.UnselectAll(), selected);
        menu.Items.Add(new Separator());
        if (VM.View == "trash") {
            Add("恢复所选", RestoreSelection, selected); Add("永久删除所选", DeleteSelection, selected);
            Add("清空回收站", () => Purge(VM.Filtered().Select(n => n.Id).ToList()), noteRows.Count > 0);
        } else {
            Add(VM.View == "star" ? "取消收藏所选" : "收藏所选", () => Batch(n => n with { Favorite = VM.View != "star" }), selected);
            Add(VM.View == "archive" ? "取消归档所选" : "归档所选", () => Batch(n => n with { Archived = VM.View != "archive" }), selected);
            Add("移到回收站", DeleteSelection, selected);
        }
        return menu;
    }
    private List<string> SelectedIds()=>noteList?.SelectedItems.Cast<NoteRow>().Select(n=>n.Note.Id).ToList()??[];
    private void Batch(Func<Note,Note> change){var ids=SelectedIds();VM.Change(v=>v with{Notes=v.Notes.Select(n=>ids.Contains(n.Id)?n.Apply(change(n)):n).ToList()});}
    private void DeleteSelection(){var ids=SelectedIds();if(ids.Count==0)return;if(VM.View=="trash")Purge(ids);else TrashNotes(ids);}
    private void TrashNotes(List<string> ids){VM.Change(v=>v with{Notes=v.Notes.Select(n=>ids.Contains(n.Id)?n.Apply(n with{DeletedAt=Clock.Now}):n).ToList()});Toast("已移到回收站");}
    private void RestoreSelection()=>RestoreNotes(SelectedIds());
    private void RestoreNotes(List<string> ids){VM.Change(v=>{var books=v.Notes.Where(n=>ids.Contains(n.Id)).Select(n=>n.NotebookId).ToHashSet();return v with{Notebooks=v.Notebooks.Select(b=>books.Contains(b.Id)&&b.DeletedAt!=null?b with{DeletedAt=null,Revision=Clock.Id,UpdatedAt=Clock.Now}:b).ToList(),Notes=v.Notes.Select(n=>ids.Contains(n.Id)?n.Apply(n with{DeletedAt=null}):n).ToList()};});Toast("记录已恢复");}
    private void Purge(List<string> ids){if(!Confirm("永久删除所选记录？","正文、密码与历史版本都会清除，无法从回收站恢复。"))return;VM.Change(v=>v with{Notes=v.Notes.Select(n=>ids.Contains(n.Id)?n with{Title="",Content="",Username="",Password="",Url="",Otp="",Tags=[],Variables=[],History=[],DeletedAt=n.DeletedAt??Clock.Now,PurgedAt=Clock.Now,Revision=Clock.Id,UpdatedAt=Clock.Now}:n).ToList()});Toast("记录已永久删除");}
}
