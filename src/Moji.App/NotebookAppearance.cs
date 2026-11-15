using System.Windows.Media;
namespace Moji.App;
internal static class NotebookAppearance {
    internal static readonly (string Icon,string Label)[] Icons=[("Book","笔记本"),("Document","文档"),("Prompt","灵感"),("Lock","钥匙"),("Star","星标"),("Archive","收纳"),("Pin","图钉"),("Image","图片"),("Cloud","云朵"),("Code","代码"),("Search","搜索"),("Settings","齿轮")];
    internal static readonly string[] Colors=["#75876D","#B2946E","#8393AC","#A5849D","#BA886C","#9182A8"];
    internal static Color Parse(string value){try{return (Color)ColorConverter.ConvertFromString(value);}catch{return Color.FromRgb(117,135,109);}}
    internal static Color Mix(Color a,Color b,double t)=>Color.FromRgb((byte)(a.R+(b.R-a.R)*t),(byte)(a.G+(b.G-a.G)*t),(byte)(a.B+(b.B-a.B)*t));
    internal static string Ink(string value)=>Mix(Parse(value),System.Windows.Media.Colors.Black,.32).ToString();
}
