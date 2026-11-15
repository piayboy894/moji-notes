using System.Windows.Media;
namespace Moji.App;
internal static partial class NotebookAppearance {
    internal static Color Parse(string value){try{return (Color)ColorConverter.ConvertFromString(value);}catch{return Color.FromRgb(117,135,109);}}
    internal static Color Mix(Color a,Color b,double t)=>Color.FromRgb((byte)(a.R+(b.R-a.R)*t),(byte)(a.G+(b.G-a.G)*t),(byte)(a.B+(b.B-a.B)*t));
    internal static Color Paper(Color value) {
        var result=Mix(value,Colors.White,.12);
        double Linear(byte c){var n=c/255d;return n<=.04045?n/12.92:Math.Pow((n+.055)/1.055,2.4);}
        double Light(Color c)=>.2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);
        while(Light(result)<.45)result=Mix(result,Colors.White,.08);
        return result;
    }
    internal static Brush Swatch(string start,string end)=>string.IsNullOrEmpty(end)?new SolidColorBrush(Parse(start)):new LinearGradientBrush(Paper(Parse(start)),Paper(Parse(end)),45);
    internal static string Ink(string value)=>Mix(Parse(value),System.Windows.Media.Colors.Black,.32).ToString();
}
