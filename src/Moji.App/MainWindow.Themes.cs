using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Moji.Core;
namespace Moji.App;
public partial class MainWindow
{
    internal void RenderThemeSettings(StackPanel content)
    {
        var mode=new ComboBox{Name="ThemeMode",ItemsSource=new[]{"浅色","深色","跟随 Windows"}};bool updating=false;var cards=new Dictionary<string,(Button Button,Border Frame,NativeIcon Check)>();
        void Refresh(){updating=true;var p=VM.State!.Preferences;mode.SelectedIndex=p.Theme=="dark"?1:p.Theme=="system"?2:0;var active=Ui.Palette(p).Id;foreach(var(id,card)in cards){var palette=ThemeCatalog.All.First(x=>x.Id==id);card.Frame.BorderBrush=Ui.Brush(palette.Colors[active==id?"Accent":"Line"]);card.Frame.BorderThickness=new Thickness(active==id?2:1);card.Check.Visibility=active==id?Visibility.Visible:Visibility.Hidden;System.Windows.Automation.AutomationProperties.SetItemStatus(card.Button,active==id?"已选择":"");}updating=false;}
        mode.SelectionChanged+=(_,_)=>{if(updating)return;var theme=mode.SelectedIndex==1?"dark":mode.SelectedIndex==2?"system":"light";VM.Change(w=>w with{Preferences=w.Preferences with{Theme=theme}},"edit");Ui.Theme(VM.State!.Preferences);Refresh();};content.Children.Add(Ui.Field("外观",mode));
        foreach(var dark in new[]{false,true}){content.Children.Add(Ui.Spaced(Ui.Text(dark?"深色主题":"浅色主题",12,null,true),new Thickness(0,8,0,12)));var row=new System.Windows.Controls.Primitives.UniformGrid{Columns=3,Margin=new Thickness(-4,0,-4,16)};foreach(var palette in ThemeCatalog.All.Where(p=>p.Dark==dark)){
            var c=palette.Colors;var preview=new Grid{Height=66,ClipToBounds=true,Background=Ui.Brush(c["Panel"])};preview.ColumnDefinitions.Add(new(){Width=new GridLength(27)});preview.ColumnDefinitions.Add(new());preview.Children.Add(new Border{Background=Ui.Brush(c["Side"])});var lines=new StackPanel{Margin=new Thickness(10,12,10,8)};lines.Children.Add(new Border{Height=5,Width=48,HorizontalAlignment=HorizontalAlignment.Left,Background=Ui.Brush(c["Ink"]),CornerRadius=new(2),Margin=new(0,0,0,8)});lines.Children.Add(new Border{Height=4,Background=Ui.Brush(c["Line"]),CornerRadius=new(2),Margin=new(0,0,0,6)});lines.Children.Add(new Border{Height=12,Background=Ui.Brush(c["RecordSelected"]),CornerRadius=new(3)});Grid.SetColumn(lines,1);preview.Children.Add(lines);
            var check=new NativeIcon{Kind="Check",Width=16,Height=16,Foreground=Ui.Brush(c["Accent"]),VerticalAlignment=VerticalAlignment.Center};var caption=new DockPanel{Margin=new Thickness(10,9,10,9)};DockPanel.SetDock(check,Dock.Right);caption.Children.Add(check);caption.Children.Add(Ui.Text(palette.Name,12,c["Ink"]));var stack=Ui.Stack(preview,caption);var frame=new Border{Background=Ui.Brush(c["Panel"]),CornerRadius=new(9),BorderThickness=new(1),Child=stack,ClipToBounds=true};frame.SizeChanged+=(_,_)=>frame.Clip=new RectangleGeometry(new Rect(frame.RenderSize),9,9);var button=Ui.Button("",()=>{VM.Change(w=>w with{Preferences=palette.Dark?w.Preferences with{Theme="dark",DarkPalette=palette.Id}:w.Preferences with{Theme="light",LightPalette=palette.Id}},"edit");Ui.Theme(VM.State!.Preferences);Refresh();});button.Name="Theme_"+palette.Id;button.Padding=new Thickness(0);button.BorderThickness=new(0);button.Margin=new(4,0,4,0);button.Content=frame;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;System.Windows.Automation.AutomationProperties.SetName(button,palette.Name);row.Children.Add(button);cards[palette.Id]=(button,frame,check);
        }content.Children.Add(row);}Refresh();
    }
}
