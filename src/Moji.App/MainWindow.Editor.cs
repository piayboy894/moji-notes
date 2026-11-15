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
        var tools=Ui.Row(sort,Ui.Icon("Check",()=> { if(noteList!=null){noteList.SelectionMode=noteList.SelectionMode==SelectionMode.Extended?SelectionMode.Multiple:SelectionMode.Extended;Toast("按 Ctrl 或 Shift 可多选记录");} },"批量选择"),Ui.Icon("Trash",()=>DeleteSelection(),"删除所选记录")); header.Children.Add(tools); if(VM.View=="trash"){var clear=Ui.Button("清空回收站",EmptyTrash);clear.Name="EmptyTrash";clear.IsEnabled=VM.State!.Notes.Any(n=>n.DeletedAt!=null&&n.PurgedAt==null);header.Children.Add(clear);} DockPanel.SetDock(header,Dock.Top);left.Children.Add(header);
        noteList = new ListBox { ItemsSource=noteRows, SelectionMode=SelectionMode.Extended };
        var template = "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel><TextBlock Text='{Binding Title}' FontSize='16' FontWeight='Bold' TextTrimming='CharacterEllipsis'/><TextBlock Text='{Binding Snippet}' FontSize='12' Foreground='{DynamicResource Sub}' Margin='0,10,0,12' TextWrapping='Wrap' MaxHeight='40'/><TextBlock Text='{Binding Meta}' FontSize='11' Foreground='{DynamicResource Sub}'/></StackPanel></DataTemplate>";
        noteList.ItemTemplate=(DataTemplate)XamlReader.Parse(template); noteList.SelectionChanged+=(_,_)=> { if(noteList.SelectedItem is NoteRow row){if(VM.SelectedId!=row.Note.Id)editingCredentialId="";VM.SelectedId=row.Note.Id;RenderEditor(row.Note);} };
        noteList.PreviewMouseMove+=(_,e)=> { if(e.LeftButton==MouseButtonState.Pressed && noteList.SelectedItem is NoteRow row && Keyboard.Modifiers==ModifierKeys.Alt) DragDrop.DoDragDrop(noteList,new DataObject("MojiNote",row.Note.Id),DragDropEffects.Move); };
        noteList.ContextMenu = new ContextMenu(); noteList.ContextMenuOpening += (_,_) => noteList.ContextMenu = BuildListMenu();left.Children.Add(noteList);
        root.Children.Add(new GridSplitter{Width=5,HorizontalAlignment=HorizontalAlignment.Stretch,Background=Ui.Resource("Line"),ResizeDirection=GridResizeDirection.Columns});Grid.SetColumn(root.Children[^1],1);
        editorHost=new ContentControl();Grid.SetColumn(editorHost,2);root.Children.Add(editorHost);Body.Content=root;renderedId="";RefreshNoteList();
    }
    private void RefreshNoteList()
    {
        if(noteList==null)return;var selected=VM.SelectedId;noteRows.Clear();foreach(var n in VM.Filtered())noteRows.Add(new(n));var row=noteRows.FirstOrDefault(n=>n.Note.Id==selected)??noteRows.FirstOrDefault();
        if(row!=null){noteList.SelectedItem=row;if(VM.SelectedId!=row.Note.Id)editingCredentialId="";VM.SelectedId=row.Note.Id;RenderEditor(row.Note);}else{VM.SelectedId="";renderedId="";titleBox=null;editorHost!.Content=new Border{Background=Ui.Resource("Panel"),Child=Ui.Spaced(Ui.Stack(Ui.Text("暂无记录",20,"#99A58B")),new Thickness(60,100,60,30))};}
    }
    public async void RenderEditor(Note note)
    {
        if(editorHost==null || renderedId==note.Id)return;
        var request=++editorRequest;var previous=sharedEditor;sharedEditor=null;
        if(previous!=null){previous.Deactivate();editorRelease=previous.Flush();try{await editorRelease;}catch{Toast("编辑内容暂时未能保存，请重试");return;}}
        if(request!=editorRequest)return;
        note=VM.State!.Notes.FirstOrDefault(n=>n.Id==note.Id)??note;
        renderedId=note.Id;loadingEditor=true;titleBox=null;
        if(VM.View=="trash"){var panel=new StackPanel{Margin=new Thickness(50,75,50,30)};panel.Children.Add(new NativeIcon { Kind="Trash", Width=42,Height=42 });panel.Children.Add(Ui.Spaced(Ui.Text(note.Title,25,null,true),new Thickness(0,24,0,15)));panel.Children.Add(Ui.Text("删除于 "+DateTimeOffset.FromUnixTimeMilliseconds(note.DeletedAt!.Value).LocalDateTime.ToString("yyyy-MM-dd HH:mm"),11,"#96A188"));panel.Children.Add(Ui.Spaced(Ui.Row(Ui.Button("恢复记录",()=>RestoreNotes([note.Id]),true),Ui.Spaced(Ui.Button("永久删除",()=>Purge([note.Id])),new Thickness(10,0,0,0))),new Thickness(0,25,0,20)));editorHost.Content=new Border{Background=Ui.Resource("Panel"),Child=panel};loadingEditor=false;return;}
        var book=VM.State!.Notebooks.First(b=>b.Id==note.NotebookId);var container=new DockPanel{Background=Ui.Resource("Panel")};
        var top=new DockPanel{Margin=new Thickness(27,15,22,15)};Button? favorite=null;favorite=Ui.Icon("Star",()=>{VM.EditNote(note.Id,n=>n with{Favorite=!n.Favorite});favorite!.Background=CurrentNote()!.Favorite?Ui.Resource("AccentLight"):Ui.Resource("Panel");},"收藏");var actions=Ui.Row(favorite,Ui.Icon("Restore",()=>HistoryDialog(note.Id),"版本历史"),Ui.Icon("Focus",ToggleFocus,"专注模式"));
        if (book.Mode == NoteMode.Password) {
            var edit = Ui.Button(editingCredentialId == note.Id ? "完成" : "编辑密码", () => {
                editingCredentialId = editingCredentialId == note.Id ? "" : note.Id; renderedId = ""; RenderEditor(CurrentNote()!); _ = VM.SaveAsync();
            }, true); edit.Name = "EditCredential"; edit.Margin = new Thickness(0,0,10,0); actions.Children.Insert(0,edit);
        }
        var more=Ui.Icon("More",()=>{},"更多操作");more.ToolTip="更多操作";more.Click+=(_,_)=> {more.ContextMenu=NoteMenu(note.Id);more.ContextMenu.IsOpen=true;};actions.Children.Add(more);DockPanel.SetDock(actions,Dock.Right);top.Children.Add(actions);top.Children.Add(Ui.Text(book.Name,10,NotebookAppearance.Ink(book.Color)));DockPanel.SetDock(top,Dock.Top);container.Children.Add(top);
        var heading=new StackPanel{Margin=new Thickness(28,8,28,22)};
        var titleEpoch=VM.WorkspaceEpoch;
        var recordTitle=Ui.Input(note.Title,"记录标题");titleBox=recordTitle;recordTitle.IsReadOnly=book.Mode==NoteMode.Password&&editingCredentialId!=note.Id;recordTitle.Name="RecordTitle";recordTitle.FontSize=25;recordTitle.FontWeight=FontWeights.SemiBold;recordTitle.Background=Brushes.Transparent;recordTitle.BorderThickness=new Thickness(0);recordTitle.Padding=new Thickness(0,13,0,12);recordTitle.AcceptsReturn=false;recordTitle.AcceptsTab=false;
        recordTitle.GotKeyboardFocus+=(_,_)=>cachedEditor?.ClearSelectionAvailability();
        recordTitle.PreviewKeyDown+=async(_,e)=>{if(e.Key is Key.Enter or Key.Tab && Keyboard.Modifiers==ModifierKeys.None){e.Handled=true;if(VM.WorkspaceEpoch!=titleEpoch||VM.SelectedId!=note.Id)return;if(book.Mode!=NoteMode.Password&&sharedEditor!=null)await sharedEditor.Command("focus");else recordTitle.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));}};
        recordTitle.TextChanged+=(_,_)=>{if(!loadingEditor&&!recordTitle.IsReadOnly&&VM.WorkspaceEpoch==titleEpoch&&VM.State?.Notes.Any(n=>n.Id==note.Id&&n.PurgedAt==null)==true)VM.EditNote(note.Id,n=>n with{Title=recordTitle.Text.Replace("\r\n"," ").Replace('\r',' ').Replace('\n',' ')});};heading.Children.Add(recordTitle);
        DockPanel.SetDock(heading,Dock.Top);container.Children.Add(heading);
        if(book.Mode==NoteMode.Password){container.Children.Add(editingCredentialId == note.Id ? CredentialView(note) : CredentialReadView(note));editorHost.Content=container;loadingEditor=false;return;}
        if(cachedEditor==null)WarmEditor();
        var activeEditor=cachedEditor!;sharedEditor=activeEditor;
        ParkEditor();
        if(book.Mode==NoteMode.Prompt){var convert=Ui.Button("转化为变量",()=>_ = activeEditor.Command("convertVariable"));convert.Name="ConvertVariable";convert.Focusable=false;convert.IsEnabled=activeEditor.CanConvert;activeEditor.SelectionChanged=value=>convert.IsEnabled=value;var bar=Ui.Row(convert);bar.Margin=new Thickness(22,6,22,6);DockPanel.SetDock(bar,Dock.Top);container.Children.Add(bar);}
        else activeEditor.SelectionChanged=null;
        var pendingEditor=activeEditor;editorHost.Content=container;loadingEditor=false;
        try{await pendingEditor.Show(note);await pendingEditor.Ready;if(request!=editorRequest)return;
            if(pendingEditor.Parent is Panel parent)parent.Children.Remove(pendingEditor);
            container.Children.Add(pendingEditor);
        }catch(OperationCanceledException){}catch(Exception){if(request==editorRequest)Toast("编辑器未能加载，请重新打开记录");}
    }
    private void WarmEditor()
    {
        if(cachedEditor==null){
            cachedEditor=new SharedEditorView(VM);
            cachedEditor.ActionRequested+=action=>{switch(action){case "new":AddNote();break;case "cloud":ShowCloudSaves();break;case "search":SearchDialog();break;case "save":if(CurrentNote() is {} current)VM.Checkpoint(current.Id);_ = VM.SaveAsync();break;case "focus":ToggleFocus();break;case "image-error":Toast("图片无法插入，请换一张图片重试");break;}};
        }
        ParkEditor();_ = cachedEditor!.Preload();
    }
    private void ParkEditor()
    {
        if(cachedEditor==null||cachedEditor.Parent==EditorWarmHost)return;
        if(cachedEditor.Parent is Panel parent)parent.Children.Remove(cachedEditor);
        EditorWarmHost.Children.Add(cachedEditor);
    }
    internal Task PreloadedEditor=>cachedEditor?.Preloaded??Task.FromException(new InvalidOperationException("Editor was not preloaded"));
    internal async Task<SharedEditorView> ReadyEditor(){for(var i=0;i<100&&sharedEditor==null;i++)await Task.Delay(50);var editor=sharedEditor??throw new InvalidOperationException("Editor not created");await editor.Ready.WaitAsync(TimeSpan.FromMinutes(2));return editor;}
    private UIElement CredentialReadView(Note note)
    {
        var fields = new StackPanel { Margin = new Thickness(50,25,50,35), MaxWidth = 650 };
        TextBox Value(string value, string name, bool multi = false) { var box = Ui.Input(value,null,multi); box.Name = name; box.IsReadOnly = true; return box; }
        fields.Children.Add(Ui.Field("账号 / 邮箱", InputActions(Value(note.Username,"AccountValue"),Ui.Small("复制",()=>Copy(note.Username)))));
        fields.Children.Add(Ui.Field("密码",InputActions(Value(note.Password,"PasswordValue"),Ui.Small("复制",()=>Copy(note.Password,true)))));
        fields.Children.Add(Ui.Field("网站",InputActions(Value(note.Url,"WebsiteValue"),Ui.Small("打开",()=>OpenUrl(note.Url)))));
        fields.Children.Add(Ui.Field("备注",Value(note.Content,"MemoValue",true)));
        return new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
    private UIElement CredentialView(Note note)
    {
        var fields=new StackPanel{Margin=new Thickness(50,25,50,35),MaxWidth=650};
        var user=Ui.Input(note.Username);user.TextChanged+=(_,_)=>{if(!loadingEditor)VM.EditNote(note.Id,n=>n with{Username=user.Text});};fields.Children.Add(Ui.Field("账号 / 邮箱",InputActions(user,Ui.Small("复制",()=>Copy(user.Text)))));
        var pw=Ui.Input(note.Password);pw.Name="PasswordEdit";pw.TextChanged+=(_,_)=>{if(!loadingEditor)VM.EditNote(note.Id,n=>n with{Password=pw.Text});};
        fields.Children.Add(Ui.Field("密码",InputActions(pw,Ui.Small("复制",()=>Copy(pw.Text,true)),Ui.Small("生成",()=>PasswordGenerator(value=>pw.Text=value)))));
        var url=Ui.Input(note.Url);url.TextChanged+=(_,_)=>{if(!loadingEditor)VM.EditNote(note.Id,n=>n with{Url=url.Text});};fields.Children.Add(Ui.Field("网站",InputActions(url,Ui.Small("打开",()=>OpenUrl(url.Text)))));
        var memo=Ui.Input(note.Content,null,true);memo.MinHeight=110;memo.Margin=new Thickness(0,7,0,0);memo.TextChanged+=(_,_)=>{if(!loadingEditor)VM.EditNote(note.Id,n=>n with{Content=memo.Text});};fields.Children.Add(Ui.Spaced(Ui.Field("备注",memo),new Thickness(0,22,0,0)));return new ScrollViewer{Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
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
            Add("清空回收站", EmptyTrash, VM.State.Notes.Any(n=>n.DeletedAt!=null&&n.PurgedAt==null));
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
    private void EmptyTrash()=>Purge(VM.State!.Notes.Where(n=>n.DeletedAt!=null&&n.PurgedAt==null).Select(n=>n.Id).ToList(),true);
    private void Purge(List<string> ids,bool all=false){if(ids.Count==0)return;if(!Confirm(all?"清空回收站？":"永久删除所选记录？",$"将永久删除 {ids.Count} 条记录，正文、密码与历史版本都会清除，无法恢复。"))return;VM.Change(v=>v with{Notes=v.Notes.Select(n=>ids.Contains(n.Id)?n with{Title="",Content="",Username="",Password="",Url="",Otp="",Tags=[],Variables=[],History=[],DeletedAt=n.DeletedAt??Clock.Now,PurgedAt=Clock.Now,Revision=Clock.Id,UpdatedAt=Clock.Now}:n).ToList()});Toast("记录已永久删除");}
}
