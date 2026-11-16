using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using Moji.Core;

namespace Moji.App;
internal record DisplayFont(string Id,string Name,string Family);
internal static class InstalledFonts
{
    internal const string DefaultFamily="Microsoft YaHei UI, Segoe UI";
    internal static IReadOnlyList<DisplayFont> All {get;}=Read();
    private static List<DisplayFont> Read()
    {
        var language=XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
        var list=new List<DisplayFont>{new("","系统默认",DefaultFamily)};
        foreach(var family in Fonts.SystemFontFamilies){var name=family.FamilyNames.TryGetValue(language,out var localized)?localized:family.Source;if(!string.IsNullOrWhiteSpace(family.Source))list.Add(new(family.Source,name,family.Source));}
        return [list[0],..list.Skip(1).DistinctBy(f=>f.Id,StringComparer.OrdinalIgnoreCase).OrderBy(f=>f.Name,StringComparer.CurrentCultureIgnoreCase)];
    }
    internal static string Resolve(string? id)=>All.FirstOrDefault(f=>string.Equals(f.Id,id,StringComparison.OrdinalIgnoreCase))?.Family??DefaultFamily;
    internal static string CssFamily(string? id)=>All.FirstOrDefault(f=>string.Equals(f.Id,id,StringComparison.OrdinalIgnoreCase))?.Id??"";
    internal static string Label(string? id)=>All.FirstOrDefault(f=>string.Equals(f.Id,id,StringComparison.OrdinalIgnoreCase))?.Name??"系统默认";
}
public partial class MainWindow
{
    internal Window BuildFontPicker(Window owner,Action changed)
    {
        var layout=new DockPanel{Margin=new Thickness(22)};var search=Ui.Input("","搜索字体");search.Name="FontSearch";search.Margin=new(0,0,0,14);DockPanel.SetDock(search,Dock.Top);layout.Children.Add(search);
        var list=new ListBox{Name="InstalledFontList",ItemsSource=InstalledFonts.All,SelectedItem=InstalledFonts.All.FirstOrDefault(f=>f.Id==VM.State!.Preferences.FontFamily)??InstalledFonts.All[0]};
        list.ItemTemplate=(DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel><TextBlock Text='{Binding Name}' FontSize='14'/><TextBlock Text='墨记 Aa 123' FontFamily='{Binding Family}' FontSize='19' Margin='0,7,0,0'/></StackPanel></DataTemplate>");
        Window? dialog=null;var save=Ui.Button("应用",()=>{if(list.SelectedItem is not DisplayFont font)return;VM.Change(w=>w with{Preferences=w.Preferences with{FontFamily=font.Id}},"edit");changed();dialog!.Close();},true);save.Name="ApplyDisplayFont";
        list.SelectionChanged+=(_,_)=>save.IsEnabled=list.SelectedItem!=null;
        var actions=Ui.Row(Ui.Button("取消",()=>dialog!.Close()),Ui.Spaced(save,new Thickness(10,0,0,0)));actions.HorizontalAlignment=HorizontalAlignment.Right;actions.Margin=new(0,15,0,0);DockPanel.SetDock(actions,Dock.Bottom);layout.Children.Add(actions);layout.Children.Add(list);
        search.TextChanged+=(_,_)=>{var selected=list.SelectedItem;list.ItemsSource=InstalledFonts.All.Where(f=>f.Name.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)||f.Id.Contains(search.Text,StringComparison.OrdinalIgnoreCase)).ToList();if(selected!=null&&list.Items.Contains(selected))list.SelectedItem=selected;};
        dialog=Ui.Dialog(owner,"显示字体",layout,540,620);dialog.Loaded+=(_,_)=>{if(list.SelectedItem!=null)list.ScrollIntoView(list.SelectedItem);search.Focus();};return dialog;
    }
    private void RenderFontSetting(StackPanel content,Window owner)
    {
        Button? choose=null;choose=Ui.Button(InstalledFonts.Label(VM.State!.Preferences.FontFamily),()=>BuildFontPicker(owner,()=>choose!.Content=InstalledFonts.Label(VM.State!.Preferences.FontFamily)).ShowDialog());choose.Name="ChooseDisplayFont";choose.HorizontalContentAlignment=HorizontalAlignment.Left;choose.Margin=new(0,8,0,0);content.Children.Add(Ui.Field("显示字体",choose));
    }
}
