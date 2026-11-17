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
    private Grid? workspaceRoot, recordBody;
    private DockPanel? recordSurface;
    private TextBlock? listHeading, trashCount, recordBook;
    private StackPanel? trashActions, variableActions;
    private Button? clearTrashButton, recordFavorite, convertVariable;
    private TextBox? recordTitle,recordSearch;
    private bool refreshingNotes, refreshingTitle,selectingRecords;
    private Grid? recordTools;
    private StackPanel? recordSelectionTools;
    private TextBlock? recordSelectionCount;
    private Button? recordSelectAll,recordSelectionDelete;
    private long recordEpoch=-1;
    private string recordContext="";
    private Task editorTransition=Task.CompletedTask;
    private void RenderWorkspace()
    {
        if(workspaceRoot!=null){if(recordSearch!.Text!=VM.Search){refreshingNotes=true;recordSearch.Text=VM.Search;refreshingNotes=false;}workspaceRoot.ColumnDefinitions[0].Width=new GridLength(focusMode?0:285);Body.Content=workspaceRoot;listHeading!.Text=ViewTitle();UpdateTrashActions();RefreshNoteList();return;}
        var root=new Grid();workspaceRoot=root;root.ColumnDefinitions.Add(new(){Width=new GridLength(focusMode?0:285)});root.ColumnDefinitions.Add(new(){Width=new GridLength(5)});root.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var left=new DockPanel{Background=Ui.Resource("Side")};left.SetResourceReference(Panel.BackgroundProperty,"Side");root.Children.Add(left);
        var header=new StackPanel{Margin=new Thickness(18,22,18,12)};var title=new DockPanel();listHeading=Ui.Text(ViewTitle(),18,null,true);title.Children.Add(listHeading);header.Children.Add(title);
        var search=Ui.Input(VM.Search,"搜索记录");recordSearch=search;search.Name="RecordSearch";search.Padding=new Thickness(34,10,12,10);search.FontSize=13;search.TextChanged+=(_,_)=>{if(refreshingNotes)return;VM.Search=search.Text;RefreshNoteList();};var searchRow=new Grid{Margin=new Thickness(0,16,0,10)};searchRow.Children.Add(search);searchRow.Children.Add(new NativeIcon{Kind="Search",Width=17,Height=17,Foreground=Ui.Resource("Sub"),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,0,0),IsHitTestVisible=false});header.Children.Add(searchRow);
        var sort=new ComboBox{Name="RecordSort",ItemsSource=new[]{"最近编辑","创建时间","名称顺序"},SelectedIndex=VM.Sort=="created"?1:VM.Sort=="name"?2:0,FontSize=12,MinWidth=120};sort.SelectionChanged+=(_,_)=>{VM.Sort=sort.SelectedIndex==1?"created":sort.SelectedIndex==2?"name":"updated";RefreshNoteList();};
        var tools=new Grid();recordTools=tools;tools.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});tools.ColumnDefinitions.Add(new(){Width=GridLength.Auto});tools.Children.Add(sort);
        var select=Ui.Small("选择",()=>SetRecordSelection(true));select.Name="SelectRecords";select.Margin=new Thickness(8,0,0,0);Grid.SetColumn(select,1);tools.Children.Add(select);header.Children.Add(tools);
        recordSelectionCount=Ui.Text("已选 0",11);recordSelectAll=Ui.Small("全选",()=>{if(noteList==null)return;if(noteList.SelectedItems.Count==noteRows.Count)noteList.UnselectAll();else noteList.SelectAll();});recordSelectAll.Name="SelectAllRecords";recordSelectionDelete=Ui.Small("删除",DeleteSelection);recordSelectionDelete.Name="DeleteSelectedRecords";recordSelectionDelete.SetResourceReference(Control.ForegroundProperty,"Danger");var cancel=Ui.Small("取消",()=>SetRecordSelection(false));cancel.Name="CancelRecordSelection";var bar=new DockPanel();var batchActions=Ui.Row(recordSelectAll,recordSelectionDelete,cancel);DockPanel.SetDock(batchActions,Dock.Right);bar.Children.Add(batchActions);bar.Children.Add(recordSelectionCount);recordSelectionTools=Ui.Stack(bar);recordSelectionTools.Visibility=Visibility.Collapsed;header.Children.Add(recordSelectionTools);
        trashCount=Ui.Text("",12);trashCount.Foreground=Ui.Resource("Sub");clearTrashButton=Ui.Button("清空回收站",EmptyTrash);clearTrashButton.Name="EmptyTrash";clearTrashButton.Style=(Style)FindResource("QuietDangerButton");clearTrashButton.FontSize=12;clearTrashButton.Padding=new Thickness(8,6,8,6);clearTrashButton.MinHeight=32;var trashRow=new DockPanel();DockPanel.SetDock(clearTrashButton,Dock.Right);trashRow.Children.Add(clearTrashButton);trashRow.Children.Add(trashCount);trashActions=Ui.Stack(trashRow);trashActions.Margin=new Thickness(0,12,0,0);header.Children.Add(trashActions);UpdateTrashActions();DockPanel.SetDock(header,Dock.Top);left.Children.Add(header);
        noteList=new ListBox{Name="RecordList",SelectionMode=SelectionMode.Extended,ItemsSource=noteRows,Margin=new Thickness(3,0,3,9)};
        var template="""
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:local="clr-namespace:Moji.App;assembly=Moji.Notes" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
            <CheckBox Name="RecordCheckbox" IsChecked="{Binding IsSelected,RelativeSource={RelativeSource AncestorType=ListBoxItem},Mode=TwoWay}" AutomationProperties.Name="选择记录" Margin="0,0,10,0" VerticalAlignment="Top"><CheckBox.Style><Style TargetType="CheckBox" BasedOn="{StaticResource {x:Type CheckBox}}"><Setter Property="Visibility" Value="Collapsed"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><Border x:Name="box" Width="20" Height="20" Background="{DynamicResource Panel}" BorderBrush="{DynamicResource Outline}" BorderThickness="1" CornerRadius="5"><Path x:Name="tick" Visibility="Collapsed" Data="M 3 9 L 7 13 L 15 4" Stroke="{DynamicResource RecordSelected}" StrokeThickness="1.8"/></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="box" Property="Background" Value="{DynamicResource RecordSelectedInk}"/><Setter TargetName="box" Property="BorderBrush" Value="{DynamicResource RecordSelectedInk}"/><Setter TargetName="tick" Property="Visibility" Value="Visible"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter><Style.Triggers><DataTrigger Binding="{Binding IsSelecting}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style></CheckBox.Style></CheckBox>
            <StackPanel Grid.Column="1"><TextBlock Text="{Binding Title}" FontSize="17" FontWeight="SemiBold" TextTrimming="CharacterEllipsis" Foreground="{Binding Foreground,RelativeSource={RelativeSource AncestorType=ListBoxItem}}"/><TextBlock Text="{Binding Snippet}" FontSize="12" TextWrapping="Wrap" MaxHeight="40" Margin="0,8,0,0" Style="{StaticResource RecordSecondary}"/>
              <DockPanel Margin="0,6,0,0"><Button Name="RecordPin" Style="{StaticResource RecordPinButton}" DockPanel.Dock="Right" Tag="{Binding}" ToolTip="{Binding PinLabel}" AutomationProperties.Name="{Binding PinLabel}" Width="28" Height="28" MinWidth="28" MinHeight="28" Padding="5" Margin="4,0,0,0" BorderThickness="0" Focusable="False"><local:NativeIcon Kind="{Binding PinIcon}" Width="17" Height="17" Foreground="{Binding Foreground,RelativeSource={RelativeSource AncestorType=Button}}"/></Button><TextBlock Text="{Binding Meta}" FontSize="10" VerticalAlignment="Center" Style="{StaticResource RecordSecondary}"/></DockPanel>
            </StackPanel>
          </Grid>
          <DataTemplate.Triggers><DataTrigger Binding="{Binding IsSelecting}" Value="True"><Setter TargetName="RecordPin" Property="Visibility" Value="Collapsed"/></DataTrigger></DataTemplate.Triggers>
        </DataTemplate>
        """;
        noteList.ItemTemplate=(DataTemplate)XamlReader.Parse(template);
        noteList.AddHandler(Button.ClickEvent,new RoutedEventHandler((_,e)=>{if(e.OriginalSource is Button{Tag:NoteRow row}button){if(button.Name=="RecordPin"){e.Handled=true;ToggleRecordPin(row.Note.Id);}}}));
        noteList.SelectionChanged+=(_,_)=>{if(refreshingNotes)return;if(selectingRecords){UpdateRecordSelectionUi();return;}if(noteList.SelectedItem is NoteRow row){if(VM.SelectedId!=row.Note.Id)editingCredentialId="";VM.SelectedId=row.Note.Id;RenderEditor(row.Note);}};
        noteList.PreviewMouseRightButtonDown+=(_,e)=>{var source=e.OriginalSource as DependencyObject;while(source!=null&&source is not ListBoxItem)source=VisualTreeHelper.GetParent(source);if(source is ListBoxItem item&&!item.IsSelected){noteList.SelectedItem=item.DataContext;item.Focus();}};
        AttachRecordDrag(noteList);
        noteList.ContextMenu=new ContextMenu();noteList.ContextMenuOpening+=(_,_)=>noteList.ContextMenu=BuildListMenu();left.Children.Add(noteList);
        editorHost=new ContentControl();Grid.SetColumn(editorHost,2);root.Children.Add(editorHost);Body.Content=root;RefreshNoteList();
    }
    private void SetRecordSelection(bool active)
    {
        selectingRecords=active;if(noteList==null)return;refreshingNotes=true;
        try{noteList.UnselectAll();noteList.SelectionMode=active?SelectionMode.Multiple:SelectionMode.Extended;foreach(var row in noteRows)row.SetSelecting(active);if(!active)noteList.SelectedItem=noteRows.FirstOrDefault(r=>r.Note.Id==VM.SelectedId);}finally{refreshingNotes=false;}
        UpdateRecordSelectionUi();
    }
    private void UpdateRecordSelectionUi()
    {
        if(recordTools==null||recordSelectionTools==null||noteList==null)return;recordTools.Visibility=selectingRecords?Visibility.Collapsed:Visibility.Visible;recordSelectionTools.Visibility=selectingRecords?Visibility.Visible:Visibility.Collapsed;recordSelectionCount!.Text="已选 "+noteList.SelectedItems.Count;recordSelectionDelete!.IsEnabled=noteList.SelectedItems.Count>0;recordSelectAll!.Content=noteList.SelectedItems.Count>0&&noteList.SelectedItems.Count==noteRows.Count?"取消全选":"全选";
    }
    private void UpdateTrashActions(){if(trashActions==null)return;var count=VM.State!.Notes.Count(n=>n.DeletedAt!=null&&n.PurgedAt==null);trashActions.Visibility=VM.View=="trash"?Visibility.Visible:Visibility.Collapsed;trashCount!.Text=$"{count} 条记录";clearTrashButton!.IsEnabled=count>0;}
    private void RefreshNoteList()
    {
        if(inOverview){RefreshOverview();return;}if(noteList==null||refreshingNotes)return;var selected=VM.SelectedId;var batch=selectingRecords?SelectedIds().ToHashSet():[];NoteRow? row;
        refreshingNotes=true;try{var existing=noteRows.ToDictionary(r=>r.Note.Id);noteRows.Clear();foreach(var n in VM.Filtered()){var added=existing.TryGetValue(n.Id,out var old)?old:new NoteRow(n);if(!ReferenceEquals(added.Note,n))added.Replace(n);added.SetSelecting(selectingRecords);noteRows.Add(added);}row=noteRows.FirstOrDefault(n=>n.Note.Id==selected)??noteRows.FirstOrDefault();if(selectingRecords){foreach(var item in noteRows.Where(r=>batch.Contains(r.Note.Id)))noteList.SelectedItems.Add(item);}else noteList.SelectedItem=row;}finally{refreshingNotes=false;}
        UpdateRecordSelectionUi();if(selectingRecords)return;
        if(row!=null){if(VM.SelectedId!=row.Note.Id)editingCredentialId="";VM.SelectedId=row.Note.Id;RenderEditor(row.Note);}else{VM.SelectedId="";renderedId="";recordContext="";titleBox=null;sharedEditor?.Deactivate();sharedEditor=null;ParkEditor();editorHost!.Content=new Border{Background=Ui.Resource("Panel"),Child=Ui.Spaced(Ui.Stack(Ui.Text("暂无记录",20)),new Thickness(40,70,40,30))};}
    }
    public void RenderEditor(Note note)=>editorTransition=RenderEditorCore(note);
    private async Task RenderEditorCore(Note note)
    {
        if(editorHost==null)return;
        var request=++editorRequest;var previous=sharedEditor;
        if(previous!=null){editorRelease=previous.Flush();try{await editorRelease;}catch{Toast("编辑内容暂时未能保存，请重试");return;}}
        if(request!=editorRequest)return;note=VM.State!.Notes.FirstOrDefault(n=>n.Id==note.Id)??note;
        var book=VM.State.Notebooks.First(b=>b.Id==note.NotebookId);
        if(VM.View=="trash"){
            sharedEditor?.Deactivate();sharedEditor=null;ParkEditor();recordContext="";titleBox=null;renderedId=note.Id;
            var panel=new StackPanel{Margin=new Thickness(36,45,36,30),MaxWidth=650,HorizontalAlignment=HorizontalAlignment.Left};panel.Children.Add(new NativeIcon{Kind="Trash",Width=32,Height=32,Foreground=Ui.Resource("Sub"),HorizontalAlignment=HorizontalAlignment.Left});panel.Children.Add(Ui.Spaced(Ui.Text(note.Title,25,null,true),new Thickness(0,22,0,12)));panel.Children.Add(Ui.Text("删除于 "+DateTimeOffset.FromUnixTimeMilliseconds(note.DeletedAt!.Value).LocalDateTime.ToString("yyyy-MM-dd HH:mm"),12));var purge=Ui.Button("永久删除",()=>Purge([note.Id]));purge.Style=(Style)FindResource("QuietDangerButton");panel.Children.Add(Ui.Spaced(Ui.Row(Ui.Button("恢复记录",()=>RestoreNotes([note.Id]),true),Ui.Spaced(purge,new Thickness(10,0,0,0))),new Thickness(0,24,0,0)));editorHost.Content=new Border{Background=Ui.Resource("Panel"),Child=panel};return;
        }
        if(book.Mode==NoteMode.Password){sharedEditor?.Deactivate();sharedEditor=null;ParkEditor();RenderCredentialRecord(note,book);return;}
        EnsureRecordSurface();titleBox=recordTitle;recordContext=note.Id;recordEpoch=VM.WorkspaceEpoch;renderedId=note.Id;UpdateRecordChrome(note,book);editorHost.Content=recordSurface;
        if(cachedEditor==null)WarmEditor();var editor=cachedEditor!;sharedEditor=editor;editor.SelectionChanged=value=>convertVariable!.IsEnabled=value;
        var attached=ReferenceEquals(editor.Parent,recordBody);
        if(!attached){await Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.Render);EditorWarmHost.Width=Math.Max(100,recordBody!.ActualWidth);EditorWarmHost.Height=Math.Max(100,recordBody.ActualHeight);}
        try{await editor.Show(note);await editor.Ready;if(request!=editorRequest)return;
            if(!ReferenceEquals(editor.Parent,recordBody)){if(editor.Parent is Panel parent)parent.Children.Remove(editor);recordBody!.Children.Add(editor);}
        }catch(OperationCanceledException){}catch(Exception){if(request==editorRequest){if(editor.Parent is Panel parent)parent.Children.Remove(editor);recordBody!.Children.Clear();recordBody.Children.Add(editor);Toast("编辑器未能加载，请重试");}}
    }
    private void EnsureRecordSurface()
    {
        if(recordSurface!=null)return;
        recordSurface=new DockPanel{Background=Ui.Resource("Panel"),Name="RecordSurface"};recordSurface.SetResourceReference(Panel.BackgroundProperty,"Panel");var top=new DockPanel{Margin=new Thickness(28,15,22,15),Height=44};
        recordFavorite=Ui.Icon("Star",()=>{if(CurrentNote() is {} n){VM.EditNote(n.Id,v=>v with{Favorite=!v.Favorite});recordFavorite!.Background=CurrentNote()!.Favorite?Ui.Resource("AccentLight"):Brushes.Transparent;}},"收藏");
        var more=Ui.Icon("More",()=>{},"更多操作");more.Click+=(_,_)=>{if(CurrentNote() is {} n){more.ContextMenu=NoteMenu(n.Id);more.ContextMenu.PlacementTarget=more;more.ContextMenu.IsOpen=true;}};
        var actions=Ui.Row(recordFavorite,Ui.Icon("Restore",()=>{if(CurrentNote() is {} n)HistoryDialog(n.Id);},"版本历史"),Ui.Icon("Focus",ToggleFocus,"专注模式"),more);DockPanel.SetDock(actions,Dock.Right);top.Children.Add(actions);recordBook=Ui.Text("",11);recordBook.TextWrapping=TextWrapping.NoWrap;recordBook.TextTrimming=TextTrimming.CharacterEllipsis;top.Children.Add(OverviewBackButton(recordBook));DockPanel.SetDock(top,Dock.Top);recordSurface.Children.Add(top);
        recordTitle=Ui.Input("","记录标题");recordTitle.Name="RecordTitle";recordTitle.FontSize=25;recordTitle.FontWeight=FontWeights.SemiBold;recordTitle.Background=Brushes.Transparent;recordTitle.BorderThickness=new Thickness(0);recordTitle.Padding=new Thickness(0,13,0,12);recordTitle.Margin=new Thickness(28,8,28,20);recordTitle.AcceptsReturn=false;recordTitle.AcceptsTab=false;
        recordTitle.GotKeyboardFocus+=(_,_)=>cachedEditor?.ClearSelectionAvailability();recordTitle.PreviewKeyDown+=async(_,e)=>{if(e.Key is Key.Enter or Key.Tab&&Keyboard.Modifiers==ModifierKeys.None){e.Handled=true;if(sharedEditor!=null)await sharedEditor.Command("focus");}};
        recordTitle.TextChanged+=(_,_)=>{if(!refreshingTitle&&recordEpoch==VM.WorkspaceEpoch&&recordContext==VM.SelectedId&&VM.State?.Notes.Any(n=>n.Id==recordContext&&n.PurgedAt==null)==true)VM.EditNote(recordContext,n=>n with{Title=recordTitle.Text.Replace("\r\n"," ").Replace('\r',' ').Replace('\n',' ')});};DockPanel.SetDock(recordTitle,Dock.Top);recordSurface.Children.Add(recordTitle);
        convertVariable=Ui.Button("转化为变量",()=>{if(sharedEditor!=null)_ = sharedEditor.Command("convertVariable");});convertVariable.Name="ConvertVariable";convertVariable.Focusable=false;convertVariable.IsEnabled=false;convertVariable.FontSize=12;convertVariable.Padding=new Thickness(12,7,12,7);convertVariable.MinHeight=34;
        variableActions=Ui.Row(convertVariable);variableActions.Height=42;variableActions.VerticalAlignment=VerticalAlignment.Top;variableActions.Margin=new Thickness(28,0,28,8);DockPanel.SetDock(variableActions,Dock.Top);recordSurface.Children.Add(variableActions);
        recordBody=new Grid{Background=Ui.Resource("Panel"),Name="RecordBodyHost"};recordBody.SetResourceReference(Panel.BackgroundProperty,"Panel");recordSurface.Children.Add(recordBody);
    }
    private void UpdateRecordChrome(Note note,Notebook book)
    {
        refreshingTitle=true;try{if(recordTitle!.Text!=note.Title)recordTitle.Text=note.Title;}finally{refreshingTitle=false;}
        recordBook!.Text=book.Name;recordBook.ToolTip=book.Name;recordBook.Foreground=new SolidColorBrush(NotebookAppearance.Parse(book.Color));recordFavorite!.Background=note.Favorite?Ui.Resource("AccentLight"):Brushes.Transparent;variableActions!.Visibility=book.Mode==NoteMode.Prompt?Visibility.Visible:Visibility.Collapsed;
    }
    private void RenderCredentialRecord(Note note,Notebook book)
    {
        recordContext="";renderedId=note.Id;loadingEditor=true;var container=new DockPanel{Background=Ui.Resource("Panel")};var top=new DockPanel{Margin=new Thickness(28,15,22,15),Height=44};var edit=Ui.Button(editingCredentialId==note.Id?"完成":"编辑密码",()=>{editingCredentialId=editingCredentialId==note.Id?"":note.Id;RenderEditor(CurrentNote()!);_ = VM.SaveAsync();},true);edit.Name="EditCredential";var favorite=Ui.Icon("Star",()=>{VM.EditNote(note.Id,n=>n with{Favorite=!n.Favorite});RenderEditor(CurrentNote()!);},"收藏");favorite.Background=note.Favorite?Ui.Resource("AccentLight"):Brushes.Transparent;var more=Ui.Icon("More",()=>{},"更多操作");more.Click+=(_,_)=>{more.ContextMenu=NoteMenu(note.Id);more.ContextMenu.PlacementTarget=more;more.ContextMenu.IsOpen=true;};var actions=Ui.Row(edit,favorite,Ui.Icon("Restore",()=>HistoryDialog(note.Id),"版本历史"),Ui.Icon("Focus",ToggleFocus,"专注模式"),more);DockPanel.SetDock(actions,Dock.Right);top.Children.Add(actions);top.Children.Add(OverviewBackButton(Ui.Text(book.Name,11)));DockPanel.SetDock(top,Dock.Top);container.Children.Add(top);
        var title=Ui.Input(note.Title,"记录标题");titleBox=title;var epoch=VM.WorkspaceEpoch;title.Name="RecordTitle";title.FontSize=25;title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(28,8,28,20);title.Padding=new Thickness(0,13,0,12);title.Background=Brushes.Transparent;title.BorderThickness=new Thickness(0);title.IsReadOnly=editingCredentialId!=note.Id;title.TextChanged+=(_,_)=>{if(!loadingEditor&&!title.IsReadOnly&&VM.WorkspaceEpoch==epoch)VM.EditNote(note.Id,n=>n with{Title=title.Text.Replace('\n',' ').Replace('\r',' ')});};title.PreviewKeyDown+=(_,e)=>{if(e.Key is Key.Enter or Key.Tab&&Keyboard.Modifiers==ModifierKeys.None){e.Handled=true;title.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));}};DockPanel.SetDock(title,Dock.Top);container.Children.Add(title);container.Children.Add(editingCredentialId==note.Id?CredentialView(note):CredentialReadView(note));editorHost!.Content=container;loadingEditor=false;
    }
    private void WarmEditor()
    {
        if(cachedEditor==null){cachedEditor=new SharedEditorView(VM);cachedEditor.ActionRequested+=action=>{switch(action){case "retry":if(CurrentNote() is {} retry)RenderEditor(retry);break;case "new":AddNote();break;case "cloud":ShowCloudSaves();break;case "search":FocusGlobalSearch();break;case "save":if(CurrentNote() is {} current)VM.Checkpoint(current.Id);_ = VM.SaveAsync();break;case "focus":ToggleFocus();break;case "image-error":Toast("图片无法插入，请换一张图片重试");break;}};recordBody?.Children.Clear();}
        ParkEditor();_ = cachedEditor.Preload();
    }
    private void ParkEditor(){if(cachedEditor==null||cachedEditor.Parent==EditorWarmHost)return;if(cachedEditor.Parent is Panel parent)parent.Children.Remove(cachedEditor);EditorWarmHost.Children.Add(cachedEditor);}
    internal Task PreloadedEditor=>cachedEditor?.Preloaded??Task.FromException(new InvalidOperationException("Editor was not preloaded"));
    internal async Task<SharedEditorView> ReadyEditor(){await editorTransition;var editor=sharedEditor??throw new InvalidOperationException("Editor not created");await editor.Ready.WaitAsync(TimeSpan.FromMinutes(2));return editor;}
    private UIElement CredentialReadView(Note note)
    {
        var fields = new StackPanel { Margin = new Thickness(28,12,28,35), MaxWidth = 800,HorizontalAlignment=HorizontalAlignment.Left };
        var scroll=new ScrollViewer{Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Ui.FitWidth(fields,scroll,widthProperty:"ViewportWidth");
        TextBox Value(string value, string name, bool multi = false) { var box = Ui.Input(value,null,multi); box.Name = name; box.IsReadOnly = true; return box; }
        fields.Children.Add(Ui.Field("账号 / 邮箱", InputActions(Value(note.Username,"AccountValue"),Ui.Small("复制",()=>Copy(note.Username)))));
        fields.Children.Add(Ui.Field("密码",InputActions(Value(note.Password,"PasswordValue"),Ui.Small("复制",()=>Copy(note.Password,true)))));
        fields.Children.Add(Ui.Field("网站",InputActions(Value(note.Url,"WebsiteValue"),Ui.Small("打开",()=>OpenUrl(note.Url)))));
        CredentialCustomFields(fields,note,false);
        fields.Children.Add(Ui.Field("备注",Value(note.Content,"MemoValue",true)));
        return scroll;
    }
    private UIElement CredentialView(Note note)
    {
        var fieldsEpoch=VM.WorkspaceEpoch;var fields=new StackPanel{Margin=new Thickness(28,12,28,35),MaxWidth=800,HorizontalAlignment=HorizontalAlignment.Left};
        var scroll=new ScrollViewer{Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Ui.FitWidth(fields,scroll,widthProperty:"ViewportWidth");
        var user=Ui.Input(note.Username);user.TextChanged+=(_,_)=>{if(!loadingEditor&&fieldsEpoch==VM.WorkspaceEpoch)VM.EditNote(note.Id,n=>n with{Username=user.Text});};fields.Children.Add(Ui.Field("账号 / 邮箱",InputActions(user,Ui.Small("复制",()=>Copy(user.Text)))));
        var pw=Ui.Input(note.Password);pw.Name="PasswordEdit";pw.TextChanged+=(_,_)=>{if(!loadingEditor&&fieldsEpoch==VM.WorkspaceEpoch)VM.EditNote(note.Id,n=>n with{Password=pw.Text});};
        fields.Children.Add(Ui.Field("密码",InputActions(pw,Ui.Small("复制",()=>Copy(pw.Text,true)),Ui.Small("生成",()=>PasswordGenerator(value=>pw.Text=value)))));
        var url=Ui.Input(note.Url);url.TextChanged+=(_,_)=>{if(!loadingEditor&&fieldsEpoch==VM.WorkspaceEpoch)VM.EditNote(note.Id,n=>n with{Url=url.Text});};fields.Children.Add(Ui.Field("网站",InputActions(url,Ui.Small("打开",()=>OpenUrl(url.Text)))));
        CredentialCustomFields(fields,note,true);
        var memo=Ui.Input(note.Content,null,true);memo.MinHeight=110;memo.Margin=new Thickness(0,7,0,0);memo.TextChanged+=(_,_)=>{if(!loadingEditor&&fieldsEpoch==VM.WorkspaceEpoch)VM.EditNote(note.Id,n=>n with{Content=memo.Text});};fields.Children.Add(Ui.Spaced(Ui.Field("备注",memo),new Thickness(0,22,0,0)));return scroll;
    }
    private void CredentialCustomFields(StackPanel host,Note note,bool editing)
    {
        var fieldEpoch=VM.WorkspaceEpoch;var rows=new StackPanel();host.Children.Add(rows);TextBox? name=null;Button? add=null;
        void Enable(){if(add!=null)add.IsEnabled=fieldEpoch==VM.WorkspaceEpoch&&!string.IsNullOrWhiteSpace(name!.Text)&&(VM.State?.Notes.FirstOrDefault(n=>n.Id==note.Id)?.Fields.Count??128)<128;}
        void Row(CredentialField field){var input=Ui.Input(field.Value);input.Name="CredentialField_"+field.Id;input.IsReadOnly=!editing;input.TextChanged+=(_,_)=>{if(editing&&!loadingEditor&&fieldEpoch==VM.WorkspaceEpoch)VM.EditNote(note.Id,n=>n with{Fields=n.Fields.Select(f=>f.Id==field.Id?f with{Value=input.Text}:f).ToList()});};var copy=Ui.Small("复制",()=>Copy(input.Text));var actions=InputActions(input,copy);var row=Ui.Field(field.Name,actions);rows.Children.Add(row);
            if(editing){var remove=Ui.Icon("Trash",()=>{if(fieldEpoch!=VM.WorkspaceEpoch)return;if(input.Text.Length>0&&!Confirm("删除「"+field.Name+"」？","属性及内容将从这条记录中移除。","删除属性",true))return;VM.EditNote(note.Id,n=>n with{Fields=n.Fields.Where(f=>f.Id!=field.Id).ToList()});rows.Children.Remove(row);Enable();},"删除属性");remove.Width=36;remove.MinWidth=36;remove.Height=36;remove.MinHeight=36;remove.Padding=new Thickness(7);DockPanel.SetDock(remove,Dock.Right);actions.Children.Insert(0,remove);}
        }
        foreach(var field in note.Fields)Row(field);
        if(!editing)return;
        name=Ui.Input("","属性名称");name.Name="NewCredentialFieldName";name.MaxLength=80;add=Ui.Button("添加新属性",()=>{if(fieldEpoch!=VM.WorkspaceEpoch)return;var label=new string(name.Text.Select(c=>char.IsControl(c)?' ':c).ToArray()).Trim();if(label.Length==0||label.Any(char.IsControl))return;var current=VM.State!.Notes.First(n=>n.Id==note.Id);if(current.Fields.Any(f=>string.Equals(f.Name,label,StringComparison.OrdinalIgnoreCase))||new[]{"账号","账号 / 邮箱","密码","网站","备注"}.Contains(label)){Toast("已存在同名属性");return;}var field=new CredentialField{Name=label};VM.EditNote(note.Id,n=>n with{Fields=[..n.Fields,field]});Row(field);name.Clear();Enable();FindCustomField(rows,"CredentialField_"+field.Id)?.Focus();});add.Name="AddCredentialField";add.FontSize=12;add.Padding=new Thickness(12,9,12,9);add.Margin=new Thickness(8,0,0,0);name.TextChanged+=(_,_)=>Enable();var form=InputActions(name,add);host.Children.Add(Ui.Field("新属性",form));Enable();
    }
    private static TextBox? FindCustomField(DependencyObject root,string name){if(root is TextBox box&&box.Name==name)return box;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var result=FindCustomField(VisualTreeHelper.GetChild(root,i),name);if(result!=null)return result;}return null;}
    private static DockPanel InputActions(UIElement input,params Button[] buttons){var panel=new DockPanel();foreach(var b in buttons.Reverse()){if(input is TextBox box&&b.Content?.ToString()=="复制"){b.IsEnabled=box.Text.Length>0;box.TextChanged+=(_,_)=>b.IsEnabled=box.Text.Length>0;}DockPanel.SetDock(b,Dock.Right);panel.Children.Add(b);}panel.Children.Add(input);return panel;}
    private static bool FromButton(DependencyObject? source){for(var item=source;item!=null;item=VisualTreeHelper.GetParent(item))if(item is System.Windows.Controls.Primitives.ButtonBase)return true;return false;}
    internal void DuplicateRecord(string id)
    {
        if(VM.State?.LiveNotes.FirstOrDefault(n=>n.Id==id) is not {} note)return;
        var names=VM.State.LiveNotes.Where(n=>n.NotebookId==note.NotebookId).Select(n=>n.Title).ToHashSet();var title=note.Title+" · 副本";for(var index=2;names.Contains(title);index++)title=note.Title+" · 副本 "+index;
        var copy=note with{Id=Clock.Id,Title=title,Variables=[..note.Variables],Fields=[..note.Fields],Pinned=false,Favorite=false,Archived=false,History=[],CreatedAt=Clock.Now,UpdatedAt=Clock.Now,Revision=Clock.Id};VM.Change(w=>w with{Notes=[copy,..w.Notes]},"edit");Navigate(copy.NotebookId,copy.Id);
    }
    internal void ToggleRecordPin(string id)
    {
        if(VM.State?.LiveNotes.FirstOrDefault(n=>n.Id==id) is not {} note)return;
        VM.Change(w=>w with{Notes=w.Notes.Select(n=>n.Id==id?n with{Pinned=!note.Pinned,Revision=Clock.Id}:n).ToList()},"edit");
        if(noteList==null||VM.View=="home"||VM.View=="trash")return;var selected=noteList.SelectedItems.Cast<NoteRow>().Select(r=>r.Note.Id).ToHashSet();refreshingNotes=true;
        try{var order=VM.Filtered().Select(n=>n.Id).ToList();for(var i=0;i<order.Count;i++){var at=noteRows.ToList().FindIndex(r=>r.Note.Id==order[i]);if(at>=0&&at!=i)noteRows.Move(at,i);}foreach(var row in noteRows)if(selected.Contains(row.Note.Id)&&!noteList.SelectedItems.Contains(row))noteList.SelectedItems.Add(row);}finally{refreshingNotes=false;}
    }
    private ContextMenu NoteMenu(string id)
    {
        var note=VM.State!.Notes.First(n=>n.Id==id);var menu=new ContextMenu();AddRecordClipboardActions(menu,[id]);menu.Items.Add(MenuItem(note.Pinned?"取消置顶":"置顶记录",()=>ToggleRecordPin(id)));menu.Items.Add(MenuItem("创建副本",()=>DuplicateRecord(id)));menu.Items.Add(MenuItem(note.Archived?"取消归档":"归档记录",()=>{VM.EditNote(id,n=>n with{Archived=!n.Archived});RefreshNoteList();}));
        var move=new MenuItem{Header="移动到记事本"};var mode=VM.State.Notebooks.First(b=>b.Id==note.NotebookId).Mode;foreach(var book in VM.State.LiveBooks.Where(b=>b.Mode==mode&&b.Id!=note.NotebookId))move.Items.Add(MenuItem(book.Name,()=>_ = MoveRecords([id],book.Id)));move.IsEnabled=move.Items.Count>0;move.Icon=new NativeIcon{Kind="Book",Width=18,Height=18};menu.Items.Add(move);
        if(mode!=NoteMode.Password){menu.Items.Add(MenuItem("导出",()=>ExportNote(note)));menu.Items.Add(MenuItem("打印 / 保存 PDF",()=>PrintNote(note)));menu.Items.Add(MenuItem("复制全文",()=>Copy(RichDocument.PlainText(note))));}menu.Items.Add(MenuItem("保存当前版本",()=>{VM.Checkpoint(id);Toast("版本已保存");}));menu.Items.Add(new Separator());menu.Items.Add(MenuItem("移到回收站",()=>TrashNotes([id])));return menu;
    }
    internal ContextMenu BuildListMenu()
    {
        var menu = new ContextMenu(); var selected = SelectedIds().Count > 0;
        AddRecordClipboardActions(menu,SelectedIds());
        void Add(string label, Action action, bool enabled = true) { var item = MenuItem(label, action); item.IsEnabled = enabled; menu.Items.Add(item); }
        var book = VM.State!.LiveBooks.FirstOrDefault(b => b.Id == VM.View);
        if (book != null) Add("编辑记事本", () => NotebookDialog(book));
        if(VM.View!="trash"){var ids=SelectedIds();Add("创建副本",()=>{if(ids.Count==1)DuplicateRecord(ids[0]);},ids.Count==1);if(ids.Count==1){var pinned=VM.State.Notes.First(n=>n.Id==ids[0]).Pinned;Add(pinned?"取消置顶":"置顶记录",()=>ToggleRecordPin(ids[0]));}}
        Add("全选", () => ActiveRecordList?.SelectAll(), noteRows.Count > 0);
        Add("取消选择", () => ActiveRecordList?.UnselectAll(), selected);
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
    private List<string> SelectedIds()=>ActiveRecordList?.SelectedItems.Cast<NoteRow>().Select(n=>n.Note.Id).ToList()??[];
    private void Batch(Func<Note,Note> change){var ids=SelectedIds();VM.Change(v=>v with{Notes=v.Notes.Select(n=>ids.Contains(n.Id)?n.Apply(change(n)):n).ToList()});}
    private void DeleteSelection(){var ids=SelectedIds();if(ids.Count==0)return;if(selectingRecords)SetRecordSelection(false);if(VM.View=="trash")Purge(ids);else TrashNotes(ids);}
    private void TrashNotes(List<string> ids){VM.Change(v=>v with{Notes=v.Notes.Select(n=>ids.Contains(n.Id)?n.Apply(n with{DeletedAt=Clock.Now}):n).ToList()});Toast("已移到回收站");}
    private void RestoreSelection()=>RestoreNotes(SelectedIds());
    private void RestoreNotes(List<string> ids){VM.Change(v=>{var books=v.Notes.Where(n=>ids.Contains(n.Id)).Select(n=>n.NotebookId).ToHashSet();return v with{Notebooks=v.Notebooks.Select(b=>books.Contains(b.Id)&&b.DeletedAt!=null?b with{DeletedAt=null,Revision=Clock.Id,UpdatedAt=Clock.Now}:b).ToList(),Notes=v.Notes.Select(n=>ids.Contains(n.Id)?n.Apply(n with{DeletedAt=null}):n).ToList()};});Toast("记录已恢复");}
    private void EmptyTrash()=>Purge(VM.State!.Notes.Where(n=>n.DeletedAt!=null&&n.PurgedAt==null).Select(n=>n.Id).ToList(),true);
    private void Purge(List<string> ids,bool all=false){if(ids.Count==0)return;if(!Confirm(all?"清空回收站？":"永久删除所选记录？",$"将永久删除 {ids.Count} 条记录，正文、密码与历史版本都会清除，无法恢复。",all?"清空回收站":"永久删除",true))return;VM.Change(v=>v with{Notes=v.Notes.Select(n=>ids.Contains(n.Id)?n with{Title="",Content="",Username="",Password="",Url="",Otp="",Tags=[],Variables=[],Fields=[],History=[],DeletedAt=n.DeletedAt??Clock.Now,PurgedAt=Clock.Now,Revision=Clock.Id,UpdatedAt=Clock.Now}:n).ToList()});Toast("记录已永久删除");}
}
