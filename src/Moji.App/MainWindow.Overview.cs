using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Moji.Core;

namespace Moji.App;
public partial class MainWindow
{
    private DockPanel? overviewRoot;
    private ListBox? overviewGrid;
    private TextBox? overviewSearch;
    private TextBlock? overviewHeading, overviewCount;
    private bool inOverview, refreshingOverview;
    private ListBox? ActiveRecordList => inOverview ? overviewGrid : noteList;
    private void RenderOverview()
    {
        editorRequest++; sharedEditor?.Deactivate(); sharedEditor = null; ParkEditor();
        VM.SelectedId = ""; recordContext = "";
        if (overviewRoot == null)
        {
            overviewRoot = new DockPanel { Margin = new Thickness(30, 28, 30, 24), Name = "NotebookOverview" };
            var header = new StackPanel(); var heading = new DockPanel();
            var actions = Ui.Row(Ui.Button("全选", () => overviewGrid?.SelectAll()), Ui.Button("取消选择", () => overviewGrid?.UnselectAll()), Ui.Button("删除", DeleteSelection));
            DockPanel.SetDock(actions, Dock.Right); heading.Children.Add(actions);
            overviewHeading = Ui.Text("", 25, null, true); heading.Children.Add(overviewHeading); header.Children.Add(heading);
            var filters = new DockPanel { Margin = new Thickness(0, 20, 0, 16) };
            var sort = new ComboBox { ItemsSource = new[] { "最近编辑", "创建时间", "名称顺序" }, SelectedIndex = 0, Width = 145 };
            sort.SelectionChanged += (_, _) => { VM.Sort = sort.SelectedIndex == 1 ? "created" : sort.SelectedIndex == 2 ? "name" : "updated"; RefreshOverview(); };
            DockPanel.SetDock(sort, Dock.Right); filters.Children.Add(sort);
            overviewCount = Ui.Text("", 12, "Sub"); overviewCount.Margin = new Thickness(18, 0, 16, 0); DockPanel.SetDock(overviewCount, Dock.Right); filters.Children.Add(overviewCount);
            overviewSearch = Ui.Input("", "搜索记录"); overviewSearch.Name = "OverviewSearch"; overviewSearch.Width = 300; overviewSearch.HorizontalAlignment = HorizontalAlignment.Left;
            overviewSearch.TextChanged += (_, _) => { if (!refreshingOverview) { VM.Search = overviewSearch.Text; RefreshOverview(); } }; filters.Children.Add(overviewSearch); header.Children.Add(filters);
            DockPanel.SetDock(header, Dock.Top); overviewRoot.Children.Add(header);
            overviewGrid = new ListBox { Name = "OverviewGrid", SelectionMode = SelectionMode.Extended, ItemsSource = noteRows, Background = Brushes.Transparent, Padding = new Thickness(0), BorderThickness = new Thickness(0) };
            overviewGrid.SetValue(ScrollViewer.CanContentScrollProperty, true);
            overviewGrid.ItemsPanel = (ItemsPanelTemplate)XamlReader.Parse("<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:local='clr-namespace:Moji.App;assembly=Moji.Notes'><local:VirtualizingCardPanel/></ItemsPanelTemplate>");
            overviewGrid.ItemContainerStyle = (Style)XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ListBoxItem'><Setter Property='Margin' Value='6'/><Setter Property='Padding' Value='0'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'><Border Name='card' Background='{DynamicResource Panel}' BorderBrush='{DynamicResource Line}' BorderThickness='1' CornerRadius='11' Padding='16'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='card' Property='Background' Value='{DynamicResource AccentLight}'/><Setter TargetName='card' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger><MultiTrigger><MultiTrigger.Conditions><Condition Property='IsMouseOver' Value='True'/><Condition Property='IsSelected' Value='False'/></MultiTrigger.Conditions><Setter TargetName='card' Property='Background' Value='{DynamicResource Hover}'/></MultiTrigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
            overviewGrid.ItemTemplate = (DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:local='clr-namespace:Moji.App;assembly=Moji.Notes'><DockPanel><CheckBox DockPanel.Dock='Top' HorizontalAlignment='Right' ToolTip='选择记录' IsChecked='{Binding IsSelected,RelativeSource={RelativeSource AncestorType=ListBoxItem},Mode=TwoWay}' Margin='0,0,0,8'/><DockPanel DockPanel.Dock='Bottom' Margin='0,8,0,0'><Button Name='OverviewPin' DockPanel.Dock='Right' Style='{StaticResource RecordPinButton}' ToolTip='{Binding PinLabel}' Width='28' Height='28' MinWidth='28' MinHeight='28' Padding='5' BorderThickness='0' Focusable='False'><local:NativeIcon Kind='{Binding PinIcon}' Width='17' Height='17' Foreground='{Binding Foreground,RelativeSource={RelativeSource AncestorType=Button}}'/></Button><TextBlock Text='{Binding Meta}' Foreground='{DynamicResource Sub}' FontSize='10' VerticalAlignment='Center'/></DockPanel><StackPanel><TextBlock Text='{Binding Title}' Foreground='{DynamicResource Ink}' FontSize='17' FontWeight='SemiBold' TextWrapping='Wrap' MaxHeight='48' TextTrimming='CharacterEllipsis'/><TextBlock Text='{Binding Snippet}' Foreground='{DynamicResource Sub}' FontSize='12' TextWrapping='Wrap' MaxHeight='50' Margin='0,9,0,0' TextTrimming='CharacterEllipsis'/></StackPanel></DockPanel></DataTemplate>");
            overviewGrid.AddHandler(Button.ClickEvent,new RoutedEventHandler((_,e)=>{if(e.OriginalSource is Button{Name:"OverviewPin",DataContext:NoteRow row}){e.Handled=true;ToggleRecordPin(row.Note.Id);}}));
            overviewGrid.SelectionChanged += (_, _) => { overviewCount.Text = overviewGrid.SelectedItems.Count > 0 ? $"已选 {overviewGrid.SelectedItems.Count} 项" : $"{overviewGrid.Items.Count} 项"; };
            RectangleSelection.Attach(overviewGrid, fromEmptySpaceOnly: true); AttachRecordDrag(overviewGrid);
            overviewGrid.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (e.Handled || draggingRecords || Keyboard.Modifiers != ModifierKeys.None || overviewGrid.SelectedItems.Count > 1 || FromButton(e.OriginalSource as DependencyObject)) return;
                if (RecordContainer(e.OriginalSource as DependencyObject)?.DataContext is NoteRow row) { e.Handled = true; OpenOverviewRecord(row.Note.Id); }
            };
            overviewGrid.PreviewMouseRightButtonDown += (_, e) => { if (RecordContainer(e.OriginalSource as DependencyObject) is { } item && !item.IsSelected) overviewGrid.SelectedItem = item.DataContext; };
            overviewGrid.ContextMenu = new ContextMenu(); overviewGrid.ContextMenuOpening += (_, _) => overviewGrid.ContextMenu = BuildListMenu();
            overviewGrid.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && overviewGrid.SelectedItem is NoteRow row) { e.Handled = true; OpenOverviewRecord(row.Note.Id); } else if (e.Key == Key.Delete && overviewGrid.SelectedItems.Count > 0) { e.Handled = true; DeleteSelection(); } };
            overviewRoot.Children.Add(overviewGrid);
        }
        Body.Content = overviewRoot; RefreshOverview();
    }
    private void RefreshOverview()
    {
        if (overviewGrid == null || refreshingOverview) return; refreshingOverview = true;
        try
        {
            var selected = overviewGrid.SelectedItems.Cast<NoteRow>().Select(r => r.Note.Id).ToHashSet();
            var existing = noteRows.ToDictionary(r => r.Note.Id); var notes = VM.Filtered().ToList();
            noteRows.Clear(); foreach (var note in notes) { var row = existing.GetValueOrDefault(note.Id) ?? new NoteRow(note); if (!ReferenceEquals(row.Note, note)) row.Replace(note); noteRows.Add(row); }
            foreach (var row in noteRows) if (selected.Contains(row.Note.Id)) overviewGrid.SelectedItems.Add(row);
            if (overviewSearch!.Text != VM.Search) overviewSearch.Text = VM.Search;
            overviewHeading!.Text = ViewTitle(); overviewCount!.Text = selected.Count > 0 ? $"已选 {selected.Count} 项" : $"{notes.Count} 项";
        }
        finally { refreshingOverview = false; }
    }
    internal void OpenOverviewRecord(string id)
    {
        if (VM.State?.LiveNotes.Any(n => n.Id == id) != true) return;
        BeginOverviewEntry(id);
    }
    internal void ReturnToOverview()
    {
        CancelOverviewEntry();
        if(focusMode){focusMode=false;Shell.ColumnDefinitions[0].Width=new GridLength(270);}
        if (selectingRecords) SetRecordSelection(false); inOverview = true; VM.SelectedId = ""; RenderOverview();
    }
    private Button OverviewBackButton(TextBlock label)
    {
        var button = Ui.Button("", ReturnToOverview, tip: "返回卡片网格"); button.Name = "BackToOverview";
        button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); button.Padding = new Thickness(0, 7, 10, 7);
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Content = Ui.Row(new NativeIcon { Kind = "ChevronLeft", Width = 18, Height = 18, Foreground = Ui.Resource("Sub"), Margin = new Thickness(0, 0, 6, 0) }, label);
        System.Windows.Automation.AutomationProperties.SetName(button, "返回卡片网格"); return button;
    }
}
