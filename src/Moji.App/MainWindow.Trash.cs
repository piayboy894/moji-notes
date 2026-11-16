using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Moji.Core;

namespace Moji.App;
public partial class MainWindow
{
    private DockPanel? trashRoot;
    private ListBox? trashGrid;
    private TextBox? trashSearch;
    private TextBlock? trashHeading,trashSelectedCount,trashEmptyCaption;
    private StackPanel? trashEmpty;
    private Button? trashRestore,trashDelete,trashClear;
    private bool refreshingTrash;
    internal ContextMenu BuildTrashNavigationMenu(){var menu=new ContextMenu();var clear=MenuItem("清空回收站",EmptyTrash);clear.IsEnabled=VM.State?.Notes.Any(n=>n.DeletedAt!=null&&n.PurgedAt==null)==true;menu.Items.Add(clear);return menu;}
    private void RenderTrash()
    {
        editorRequest++;if(sharedEditor!=null){sharedEditor.Deactivate();editorRelease=sharedEditor.Flush();sharedEditor=null;}ParkEditor();VM.SelectedId="";
        if(trashRoot==null){
            trashRoot=new DockPanel{Margin=new Thickness(30,28,30,24)};var header=new StackPanel();var row=new DockPanel();var actions=Ui.Row();
            trashRestore=Ui.Button("还原",()=>RestoreNotes(TrashSelected()),true);trashRestore.Name="RestoreTrash";trashRestore.MinWidth=80;trashDelete=Ui.Button("删除",()=>Purge(TrashSelected()));trashDelete.Style=(Style)FindResource("QuietDangerButton");trashDelete.MinWidth=72;trashClear=Ui.Button("清空回收站",EmptyTrash);trashClear.Name="EmptyTrash";trashClear.Style=(Style)FindResource("QuietDangerButton");
            actions.Children.Add(trashRestore);actions.Children.Add(Ui.Spaced(trashDelete,new Thickness(8,0,8,0)));actions.Children.Add(trashClear);DockPanel.SetDock(actions,Dock.Right);row.Children.Add(actions);
            var title=Ui.Row();trashHeading=Ui.Text("已删除的记录",25,null,true);title.Children.Add(trashHeading);trashSelectedCount=Ui.Text("",12);trashSelectedCount.Foreground=Ui.Resource("Sub");title.Children.Add(Ui.Spaced(trashSelectedCount,new Thickness(16,0,0,0)));row.Children.Add(title);header.Children.Add(row);
            var filters=new DockPanel{Margin=new Thickness(0,22,0,16)};var sort=new ComboBox{ItemsSource=new[]{"最近删除","名称顺序"},SelectedIndex=0,Width=140,FontSize=12};sort.SelectionChanged+=(_,_)=>{VM.Sort=sort.SelectedIndex==1?"name":"updated";RefreshTrash();};DockPanel.SetDock(sort,Dock.Right);filters.Children.Add(sort);trashSearch=Ui.Input("","搜索回收站");trashSearch.Name="TrashSearch";trashSearch.Width=300;trashSearch.MaxWidth=400;trashSearch.HorizontalAlignment=HorizontalAlignment.Left;trashSearch.Margin=new Thickness(0,0,16,0);trashSearch.TextChanged+=(_,_)=>{if(refreshingTrash)return;VM.Search=trashSearch.Text;RefreshTrash();};filters.Children.Add(trashSearch);header.Children.Add(filters);DockPanel.SetDock(header,Dock.Top);trashRoot.Children.Add(header);
            trashGrid=new ListBox{Name="TrashGrid",SelectionMode=SelectionMode.Extended,Background=Brushes.Transparent,Padding=new Thickness(0),Margin=new Thickness(-7,0,-7,0)};trashGrid.SetValue(ScrollViewer.CanContentScrollProperty,false);
            trashGrid.ItemsPanel=(ItemsPanelTemplate)XamlReader.Parse("<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><WrapPanel/></ItemsPanelTemplate>");
            trashGrid.ItemContainerStyle=(Style)XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ListBoxItem'><Setter Property='Width' Value='258'/><Setter Property='Height' Value='190'/><Setter Property='Margin' Value='7,6'/><Setter Property='Padding' Value='0'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='FocusVisualStyle' Value='{x:Null}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'><Border Name='card' Background='{DynamicResource Panel}' BorderBrush='{DynamicResource Line}' BorderThickness='1' CornerRadius='11' Padding='16'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='card' Property='Background' Value='{DynamicResource AccentLight}'/><Setter TargetName='card' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger><MultiTrigger><MultiTrigger.Conditions><Condition Property='IsMouseOver' Value='True'/><Condition Property='IsSelected' Value='False'/></MultiTrigger.Conditions><Setter TargetName='card' Property='Background' Value='{DynamicResource Hover}'/></MultiTrigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
            trashGrid.ItemTemplate=(DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><DockPanel LastChildFill='True'><DockPanel DockPanel.Dock='Top' Margin='0,0,0,12'><CheckBox DockPanel.Dock='Right' Margin='8,0,0,0' IsChecked='{Binding IsSelected,RelativeSource={RelativeSource AncestorType=ListBoxItem},Mode=TwoWay}' ToolTip='选择记录'/><ContentPresenter DockPanel.Dock='Left' Content='{Binding Icon}' Width='22' Height='22' Margin='0,0,8,0'/><TextBlock Text='{Binding Book}' Foreground='{DynamicResource Sub}' FontSize='11' TextTrimming='CharacterEllipsis' VerticalAlignment='Center'/></DockPanel><TextBlock DockPanel.Dock='Bottom' Text='{Binding Deleted}' FontSize='10' Foreground='{DynamicResource Sub}' Margin='0,10,0,0'/><StackPanel><TextBlock Text='{Binding Title}' Foreground='{DynamicResource Ink}' FontSize='16' FontWeight='SemiBold' TextWrapping='Wrap' MaxHeight='44' TextTrimming='CharacterEllipsis'/><TextBlock Text='{Binding Preview}' Foreground='{DynamicResource Sub}' FontSize='12' TextWrapping='Wrap' MaxHeight='36' TextTrimming='CharacterEllipsis' Margin='0,8,0,0'/></StackPanel></DockPanel></DataTemplate>");
            trashGrid.SelectionChanged+=(_,_)=>UpdateTrashSelection();trashGrid.PreviewMouseRightButtonDown+=(_,e)=>{var source=e.OriginalSource as DependencyObject;while(source!=null&&source is not ListBoxItem)source=VisualTreeHelper.GetParent(source);if(source is ListBoxItem item&&!item.IsSelected)trashGrid.SelectedItem=item.DataContext;};trashGrid.ContextMenu=new ContextMenu();trashGrid.ContextMenuOpening+=(_,_)=>{var menu=new ContextMenu();var restore=MenuItem("还原所选",()=>RestoreNotes(TrashSelected()));restore.IsEnabled=TrashSelected().Count>0;menu.Items.Add(restore);var remove=MenuItem("删除所选",()=>Purge(TrashSelected()));remove.IsEnabled=restore.IsEnabled;menu.Items.Add(remove);menu.Items.Add(new Separator());menu.Items.Add(MenuItem("全选",()=>trashGrid.SelectAll()));menu.Items.Add(MenuItem("取消选择",()=>trashGrid.UnselectAll()));menu.Items.Add(new Separator());var clear=MenuItem("清空回收站",EmptyTrash);clear.IsEnabled=VM.State!.Notes.Any(n=>n.DeletedAt!=null&&n.PurgedAt==null);menu.Items.Add(clear);trashGrid.ContextMenu=menu;};
            RectangleSelection.Attach(trashGrid);trashGrid.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Delete&&TrashSelected().Count>0){e.Handled=true;Purge(TrashSelected());}};var gridHost=new Grid();gridHost.Children.Add(trashGrid);trashEmptyCaption=Ui.Text("回收站是空的",16);trashEmpty=Ui.Stack(new NativeIcon{Kind="Trash",Width=40,Height=40,Foreground=Ui.Resource("Sub"),HorizontalAlignment=HorizontalAlignment.Center},Ui.Spaced(trashEmptyCaption,new Thickness(0,16,0,0)));trashEmpty.HorizontalAlignment=HorizontalAlignment.Center;trashEmpty.VerticalAlignment=VerticalAlignment.Center;trashEmpty.IsHitTestVisible=false;gridHost.Children.Add(trashEmpty);trashRoot.Children.Add(gridHost);
        }
        Body.Content=trashRoot;RefreshTrash();
    }
    private List<string> TrashSelected()=>trashGrid?.SelectedItems.Cast<TrashRow>().Select(r=>r.Note.Id).ToList()??[];
    private void RefreshTrash()
    {
        if(trashGrid==null||refreshingTrash)return;refreshingTrash=true;
        try{var selected=TrashSelected().ToHashSet();if(trashSearch!.Text!=VM.Search)trashSearch.Text=VM.Search;var all=VM.State!.Notes.Where(n=>n.DeletedAt!=null&&n.PurgedAt==null).ToList();var shown=all.Where(n=>RecordTools.Matches(n,VM.Search,VM.State.Notebooks.First(b=>b.Id==n.NotebookId).Mode));shown=VM.Sort=="name"?shown.OrderBy(n=>n.Title):shown.OrderByDescending(n=>n.DeletedAt);trashGrid.ItemsSource=shown.Select(n=>new TrashRow(n,VM.State.Notebooks.First(b=>b.Id==n.NotebookId))).ToList();foreach(TrashRow row in trashGrid.Items)if(selected.Contains(row.Note.Id))trashGrid.SelectedItems.Add(row);trashEmpty!.Visibility=trashGrid.Items.Count==0?Visibility.Visible:Visibility.Collapsed;trashEmptyCaption!.Text=VM.Search.Length>0?"没有找到相关记录":"回收站是空的";trashHeading!.Text=$"已删除的记录  {all.Count}";trashClear!.IsEnabled=all.Count>0;}finally{refreshingTrash=false;}UpdateTrashSelection();
    }
    private void UpdateTrashSelection(){if(trashRestore==null)return;var count=TrashSelected().Count;trashRestore.IsEnabled=trashDelete!.IsEnabled=count>0;trashSelectedCount!.Text=count>0?$"已选 {count} 项":"";}
    private sealed class TrashRow(Note note,Notebook book)
    {
        public Note Note=>note;
        public string Title=>note.Title;
        public string Book=>book.Name;
        public string Preview=>book.Mode==NoteMode.Password?note.Username:RecordTools.Plain(PromptVariables.Display(note)).Truncate(110);
        public string Deleted=>DateTimeOffset.FromUnixTimeMilliseconds(note.DeletedAt!.Value).LocalDateTime.ToString("MM-dd  HH:mm");
        public UIElement Icon{get;}=new NativeIcon{Kind=NativeIcon.ForNotebook(book),Width=22,Height=22,Foreground=new SolidColorBrush(NotebookAppearance.Parse(book.Color))};
    }
}
