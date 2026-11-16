using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Moji.Core;

namespace Moji.App;
internal sealed record SearchHit(string Id,string NotebookId,string Title,string Detail,string Icon,bool Archived)
{
    public override string ToString()=>Title;
}
public partial class MainWindow
{
    private Popup? globalSearchPopup;
    private ListBox? globalSearchResults;
    private TextBlock? globalSearchEmpty;
    private CancellationTokenSource? searchCancellation;
    private bool suppressGlobalSearch,searchBusy,openSearchWhenReady;
    internal ListBox? GlobalSearchResults=>globalSearchResults;
    internal FrameworkElement? SearchDropdownVisual=>globalSearchPopup?.Child as FrameworkElement;
    internal bool SearchDropdownOpen=>globalSearchPopup?.IsOpen==true;
    private void EnsureSearchDropdown()
    {
        if(globalSearchPopup!=null)return;
        var list=new ListBox{Name="GlobalSearchResults",MaxHeight=344,BorderThickness=new(0),Focusable=false,Margin=new(3)};list.SetResourceReference(Control.FontFamilyProperty,"AppFont");globalSearchResults=list;
        list.ItemTemplate=(DataTemplate)XamlReader.Parse("""
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:local="clr-namespace:Moji.App;assembly=Moji.Notes"><DockPanel><local:NativeIcon Kind="{Binding Icon}" Width="19" Height="19" Margin="0,0,11,0" VerticalAlignment="Top" Foreground="{Binding Foreground,RelativeSource={RelativeSource AncestorType=ListBoxItem}}"/><StackPanel><TextBlock Text="{Binding Title}" FontSize="13" FontWeight="SemiBold" TextTrimming="CharacterEllipsis" Foreground="{Binding Foreground,RelativeSource={RelativeSource AncestorType=ListBoxItem}}"/><TextBlock Text="{Binding Detail}" FontSize="11" Margin="0,6,0,0" TextTrimming="CharacterEllipsis" Style="{StaticResource RecordSecondary}"/></StackPanel></DockPanel></DataTemplate>
        """);
        list.ItemContainerStyle=new Style(typeof(ListBoxItem),(Style)FindResource(typeof(ListBoxItem)));list.ItemContainerStyle.Setters.Add(new Setter(FocusableProperty,false));list.ItemContainerStyle.Setters.Add(new Setter(MarginProperty,new Thickness(3,2,3,2)));
        list.PreviewMouseLeftButtonUp+=(_,e)=>{for(var node=e.OriginalSource as DependencyObject;node!=null;node=VisualTreeHelper.GetParent(node))if(node is ListBoxItem{DataContext:SearchHit hit}){e.Handled=true;OpenSearchHit(hit);break;}};
        globalSearchEmpty=Ui.Text("",12,"Sub");globalSearchEmpty.Margin=new(15,18,15,18);globalSearchEmpty.Visibility=Visibility.Collapsed;var grid=new Grid();grid.Children.Add(list);grid.Children.Add(globalSearchEmpty);
        var frame=new Border{Child=grid,MinHeight=50,CornerRadius=new(10),BorderThickness=new(1),Padding=new(3),Margin=new(6),Effect=new DropShadowEffect{BlurRadius=18,ShadowDepth=3,Opacity=.15}};frame.SetResourceReference(Border.BackgroundProperty,"Raised");frame.SetResourceReference(Border.BorderBrushProperty,"Line");frame.SetResourceReference(TextElement.FontFamilyProperty,"AppFont");
        globalSearchPopup=new Popup{PlacementTarget=GlobalSearchInput,Placement=PlacementMode.Bottom,HorizontalOffset=-6,VerticalOffset=2,Width=364,AllowsTransparency=true,Focusable=false,StaysOpen=false,PopupAnimation=PopupAnimation.Fade,Child=frame};globalSearchPopup.Closed+=(_,_)=>searchCancellation?.Cancel();
    }
    internal void FocusGlobalSearch()
    {
        if(VM?.State==null)return;if(focusMode)ToggleFocus();Dispatcher.BeginInvoke(()=>{GlobalSearchInput.Focus();GlobalSearchInput.SelectAll();RefreshGlobalSearch();},DispatcherPriority.Input);
    }
    private async void RefreshGlobalSearch()
    {
        if(VM?.State==null||!GlobalSearchInput.IsVisible)return;EnsureSearchDropdown();searchCancellation?.Cancel();searchCancellation?.Dispose();var cancellation=new CancellationTokenSource();searchCancellation=cancellation;var state=VM.State;var query=GlobalSearchInput.Text.Trim();
        searchBusy=true;openSearchWhenReady=false;globalSearchResults!.ItemsSource=null;globalSearchEmpty!.Visibility=Visibility.Collapsed;globalSearchPopup!.IsOpen=true;
        try{
            await Task.Delay(100,cancellation.Token);
            var hits=await Task.Run(()=>{var books=state.LiveBooks.ToDictionary(b=>b.Id);return state.Notes.Where(n=>{cancellation.Token.ThrowIfCancellationRequested();return n.DeletedAt==null&&n.PurgedAt==null&&books.TryGetValue(n.NotebookId,out var book)&&(book.Name.Contains(query,StringComparison.OrdinalIgnoreCase)||RecordTools.Matches(n,query,book.Mode));}).OrderByDescending(n=>n.UpdatedAt).Take(80).Select(n=>new SearchHit(n.Id,n.NotebookId,n.Title.Length>0?n.Title:"未命名记录",books[n.NotebookId].Name+" · "+RecordTools.Relative(n.UpdatedAt)+(n.Archived?" · 归档":""),NativeIcon.ForNotebook(books[n.NotebookId]),n.Archived)).ToList();},cancellation.Token);
            if(cancellation.IsCancellationRequested||globalSearchPopup?.IsOpen!=true)return;searchBusy=false;globalSearchResults.ItemsSource=hits;globalSearchResults.SelectedIndex=hits.Count>0?0:-1;globalSearchEmpty.Text=query.Length==0?"暂无记录":"没有找到记录";globalSearchEmpty.Visibility=hits.Count==0?Visibility.Visible:Visibility.Collapsed;if(openSearchWhenReady&&hits.Count>0){openSearchWhenReady=false;OpenSearchHit(hits[0]);}
        }catch(OperationCanceledException){}catch(Exception){if(!cancellation.IsCancellationRequested){globalSearchEmpty.Text="暂时无法搜索";globalSearchEmpty.Visibility=Visibility.Visible;}}
    }
    private async void OpenSearchHit(SearchHit hit){CloseGlobalSearch();Navigate(hit.Archived?"archive":hit.NotebookId,hit.Id);try{await editorTransition;if(VM.SelectedId!=hit.Id)return;if(sharedEditor!=null)await sharedEditor.Command("focus");else titleBox?.Focus();}catch(OperationCanceledException){}}
    private void CloseGlobalSearch(){openSearchWhenReady=false;searchBusy=false;searchCancellation?.Cancel();if(globalSearchPopup!=null)globalSearchPopup.IsOpen=false;}
    private void GlobalSearchChanged(object sender,TextChangedEventArgs e){if(suppressGlobalSearch||GlobalSearchInput==null||GlobalSearchClear==null)return;GlobalSearchClear.Visibility=GlobalSearchInput.Text.Length>0?Visibility.Visible:Visibility.Collapsed;if(GlobalSearchInput.IsKeyboardFocusWithin)RefreshGlobalSearch();}
    private void GlobalSearchFocused(object sender,RoutedEventArgs e)=>RefreshGlobalSearch();
    private void GlobalSearchMouseUp(object sender,MouseButtonEventArgs e){if(!SearchDropdownOpen&&GlobalSearchInput.IsKeyboardFocusWithin)RefreshGlobalSearch();}
    private void GlobalSearchCleared(object sender,RoutedEventArgs e){suppressGlobalSearch=true;GlobalSearchInput.Clear();suppressGlobalSearch=false;GlobalSearchClear.Visibility=Visibility.Collapsed;CloseGlobalSearch();GlobalSearchInput.Focus();}
    private void GlobalSearchKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Escape){CloseGlobalSearch();e.Handled=true;return;}
        if(e.Key==Key.Enter){if(globalSearchResults?.SelectedItem is SearchHit hit)OpenSearchHit(hit);else if(searchBusy)openSearchWhenReady=true;e.Handled=true;return;}
        if(e.Key is not (Key.Down or Key.Up))return;e.Handled=true;if(!SearchDropdownOpen){RefreshGlobalSearch();return;}if(globalSearchResults!.Items.Count==0)return;globalSearchResults.SelectedIndex=Math.Clamp(globalSearchResults.SelectedIndex+(e.Key==Key.Down?1:-1),0,globalSearchResults.Items.Count-1);globalSearchResults.ScrollIntoView(globalSearchResults.SelectedItem);
    }
}
