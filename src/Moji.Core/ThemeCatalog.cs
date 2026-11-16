namespace Moji.Core;
public record AppPalette(string Id,string Name,bool Dark,Dictionary<string,string> Colors);
public static class ThemeCatalog
{
    public static IReadOnlyList<AppPalette> All {get;}=Read();
    private static List<AppPalette> Read(){using var stream=typeof(ThemeCatalog).Assembly.GetManifestResourceStream("Moji.Themes.json")!;using var reader=new StreamReader(stream);return Json.Deserialize<List<AppPalette>>(reader.ReadToEnd());}
    public static AppPalette Resolve(Preferences preferences,bool dark)=>All.FirstOrDefault(p=>p.Dark==dark&&p.Id==(dark?preferences.DarkPalette:preferences.LightPalette))??All.First(p=>p.Dark==dark);
}
