using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Moji.App;
internal static class Ui
{
    public static Brush Brush(string value) => (Brush)new BrushConverter().ConvertFromString(value)!;
    public static Brush Resource(string key) => (Brush)Application.Current.FindResource(key);
    public static TextBlock Text(string text, double size = 13, string? color = null, bool bold = false) => new() { Text = text, FontSize = size, Foreground = color == null ? Resource("Ink") : Brush(color), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    public static Button Button(string text, Action action, bool primary = false, string? tip = null)
    { var button = new Button { Content = text, Style = (Style)Application.Current.FindResource(primary ? "PrimaryButton" : typeof(Button)), ToolTip = tip }; button.Click += (_, _) => action(); return button; }
    public static Button Small(string text, Action action, string? tip = null)
    { var b = Button(text, action, false, tip); b.Padding = new Thickness(9,6,9,6); b.FontSize = 12; b.Margin = new Thickness(2,0,2,0); return b; }
    public static Button Icon(string kind, Action action, string label)
    {
        var button = Button("",action,false,label); var icon=new NativeIcon { Kind=kind, Width=22, Height=22 };icon.SetBinding(NativeIcon.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){Source=button});button.Content=icon; button.MinWidth=40; button.MinHeight=40; button.Padding=new Thickness(8); button.Margin=new Thickness(2);
        System.Windows.Automation.AutomationProperties.SetName(button,label); return button;
    }
    public static StackPanel Stack(params UIElement[] children) { var panel = new StackPanel(); foreach (var child in children) panel.Children.Add(child); return panel; }
    public static StackPanel Row(params UIElement[] children) { var panel = Stack(children); panel.Orientation = Orientation.Horizontal; return panel; }
    public static Border Card(UIElement content, string? background = null, double radius = 9) => new() { Child = content, Background = background == null ? Resource("Panel") : Brush(background), BorderBrush = Resource("Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(radius) };
    public static Border Spaced(UIElement content, Thickness margin) => new() { Child = content, Margin = margin };
    public static TextBox Input(string value = "", string? hint = null, bool multiline = false)
    { var box = new TextBox { Text = value, ToolTip = hint,Tag=hint, AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap }; box.ContextMenu=DesktopNativeUi.TextMenu(box); if (multiline) { box.MinHeight = 90; box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; } return box; }
    public static void FitWidth(FrameworkElement target,FrameworkElement source,double inset=56,double maximum=800,string widthProperty="ActualWidth")=>target.SetBinding(FrameworkElement.WidthProperty,new System.Windows.Data.Binding(widthProperty){Source=source,Converter=new InsetWidth(inset,maximum)});
    private sealed class InsetWidth(double inset,double maximum):System.Windows.Data.IValueConverter {public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>Math.Max(0,Math.Min(maximum,(value is double width?width:0)-inset));public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>System.Windows.Data.Binding.DoNothing;}
    public static StackPanel Field(string title, UIElement control)
    { var label = Text(title, 11); label.Foreground = Resource("Sub"); label.Margin = new Thickness(0,0,0,9); var field = Stack(label, control); field.Margin = new Thickness(0,0,0,20); return field; }
    public static Window Dialog(Window owner, string title, UIElement content, double width = 500, double height = 0)
    { var dialog = new Window { Title = title, Owner = owner, Width = width, MinWidth = width, MaxHeight = 850, SizeToContent = height == 0 ? SizeToContent.Height : SizeToContent.Manual, Height = height == 0 ? 450 : height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = height == 0 ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip, ShowInTaskbar = false, Content = content }; DesktopNativeUi.Decorate(dialog); return dialog; }
    public static bool IsDark(string? theme)=>theme == "dark" || (theme == "system" && Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int light && light == 0);
    private static bool? appliedDark;
    public static void InitializeTheme(){foreach(var key in new[]{"Bg","Panel","Side","Ink","Sub","Line","Soft","Accent","AccentLight","Hover","Danger","DangerSoft","ScrollThumb"})if(Application.Current.Resources[key] is SolidColorBrush brush)Application.Current.Resources[key]=brush.CloneCurrentValue();}
    public static void Theme(string theme)
    {
        var dark=IsDark(theme);if(appliedDark==dark)return;appliedDark=dark;
        var values = dark ? new Dictionary<string,string> { ["Hover"]="#333F2F", ["Danger"]="#C76A5D", ["DangerSoft"]="#442D29", ["ScrollThumb"]="#526049", ["Bg"]="#1C231C", ["Panel"]="#252D22", ["Side"]="#20271E", ["Ink"]="#E1E7DA", ["Sub"]="#A2AF96", ["Line"]="#394532", ["Soft"]="#303B29", ["Accent"]="#829E70", ["AccentLight"]="#3B4C31" } : new Dictionary<string,string> { ["Hover"]="#F0F3EB", ["Danger"]="#AD493E", ["DangerSoft"]="#F9EDE9", ["ScrollThumb"]="#C2CABA", ["Bg"]="#F8F7F3", ["Panel"]="#FFFFFF", ["Side"]="#FDFCF9", ["Ink"]="#303C34", ["Sub"]="#7E8875", ["Line"]="#E8EBE1", ["Soft"]="#F0F3EB", ["Accent"]="#526B4D", ["AccentLight"]="#E7EDDF" };
        foreach(var (key,value) in values){var color=(Color)ColorConverter.ConvertFromString(value);if(Application.Current.Resources[key] is SolidColorBrush brush&&!brush.IsFrozen)brush.Color=color;else Application.Current.Resources[key]=new SolidColorBrush(color);}
        foreach(Window window in Application.Current.Windows)DesktopNativeUi.ApplyCaption(window);
    }
}
