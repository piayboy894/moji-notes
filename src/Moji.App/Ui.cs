using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Moji.App;
internal static class Ui
{
    internal static DependencyObject? Parent(DependencyObject child)=>child is FrameworkContentElement content?content.Parent:VisualTreeHelper.GetParent(child);
    public static Brush Brush(string value) => (Brush)new BrushConverter().ConvertFromString(value)!;
    public static Brush Resource(string key) => (Brush)Application.Current.FindResource(key);
    public static TextBlock Text(string text, double size = 13, string? color = null, bool bold = false) { var block=new TextBlock { Text=text, FontSize=size, FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal, TextWrapping=TextWrapping.Wrap, VerticalAlignment=VerticalAlignment.Center };if(color==null||!color.StartsWith("#"))block.SetResourceReference(TextBlock.ForegroundProperty,color??"Ink");else block.Foreground=Brush(color);return block; }
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
    { var label = Text(title, 11); label.SetResourceReference(TextBlock.ForegroundProperty,"Sub"); label.Margin = new Thickness(0,0,0,9); var field = Stack(label, control); field.Margin = new Thickness(0,0,0,20); return field; }
    public static Window Dialog(Window owner, string title, UIElement content, double width = 500, double height = 0)
    { var dialog = new Window { Title = title, Owner = owner, Width = width, MinWidth = width, MaxHeight = 850, SizeToContent = height == 0 ? SizeToContent.Height : SizeToContent.Manual, Height = height == 0 ? 450 : height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = height == 0 ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip, ShowInTaskbar = false, Content = content }; DesktopNativeUi.Decorate(dialog); return dialog; }
    public static bool IsDark(string? theme)=>theme == "dark" || (theme == "system" && Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int light && light == 0);
    public static Moji.Core.AppPalette Palette(Moji.Core.Preferences preferences)=>Moji.Core.ThemeCatalog.Resolve(preferences,IsDark(preferences.Theme));
    private static string appliedPalette="";
    private static readonly PaletteState paletteState=new();
    private sealed class PaletteState:System.ComponentModel.INotifyPropertyChanged
    {
        public Dictionary<string,string> Values {get;set;}=[];
        public Color this[string key]=>Values.TryGetValue(key,out var value)?(Color)ColorConverter.ConvertFromString(value):Colors.Transparent;
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public void Update(Dictionary<string,string> values){Values=values;PropertyChanged?.Invoke(this,new("Item[]"));}
    }
    public static void InitializeTheme()
    {
        Application.Current.Resources["AppFont"]=new FontFamily("Microsoft YaHei UI, Segoe UI");
        paletteState.Values=Moji.Core.ThemeCatalog.All.First().Colors;
        foreach(var key in paletteState.Values.Keys){var brush=new SolidColorBrush();System.Windows.Data.BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new System.Windows.Data.Binding("["+key+"]"){Source=paletteState});Application.Current.Resources[key]=brush;}
    }
    private static string appliedFont="";
    private static void ApplyFont(string requested)
    {
        var font=InstalledFonts.Resolve(requested);if(appliedFont==font)return;appliedFont=font;Application.Current.Resources["AppFont"]=new FontFamily(font);
    }
    public static void Theme(string theme)=>Theme(new Moji.Core.Preferences{Theme=theme});
    public static void Theme(Moji.Core.Preferences preferences)
    {
        ApplyFont(preferences.FontFamily);var palette=Palette(preferences);if(appliedPalette==palette.Id)return;appliedPalette=palette.Id;paletteState.Update(palette.Colors);
        foreach(Window window in Application.Current.Windows)DesktopNativeUi.ApplyCaption(window);
    }
}
