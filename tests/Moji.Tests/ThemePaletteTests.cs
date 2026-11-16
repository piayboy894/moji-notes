using Moji.Core;
using Xunit;
namespace Moji.Tests;
public class ThemePaletteTests
{
    [Fact]public void SixPalettesHaveReadableTextActionsSelectionAndControlEdges()
    {
        Assert.Equal(3,ThemeCatalog.All.Count(p=>p.Dark));Assert.Equal(3,ThemeCatalog.All.Count(p=>!p.Dark));Assert.Equal(6,ThemeCatalog.All.Select(p=>p.Id).Distinct().Count());
        foreach(var p in ThemeCatalog.All){var c=p.Colors;foreach(var(a,b,min)in new[]{("Ink","Panel",7d),("Sub","Panel",4.5),("Sub","AccentLight",4.5),("OnAccent","Accent",4.5),("OnSelected","AccentLight",4.5),("Danger","Panel",4.5),("OnDanger","Danger",4.5),("Outline","Panel",3d),("Ink","Selection",4.5),("Selection","Panel",1.5),("RecordSelectedInk","RecordSelected",4.5),("RecordSelectedSub","RecordSelected",4.5),("RecordSelected","Panel",3d)})Assert.True(Contrast(c[a],c[b])>=min,$"{p.Id}: {a}/{b} fails {min}:1");}
    }
    [Fact]public void IndependentLightAndDarkChoicesPersistWithoutChangingTheWorkspace()
    {
        var prefs=new Preferences{Theme="system",LightPalette="paper",DarkPalette="ocean",FontFamily="Microsoft YaHei UI"};var state=new Workspace{Preferences=prefs};var restored=Json.Clone(state);Assert.Equal(prefs.FontFamily,restored.Preferences.FontFamily);Assert.DoesNotContain("fontFamily",Json.Serialize(new SyncPayload(state.Schema,state.Id,state.Notebooks,state.Notes)));Assert.Equal("paper",ThemeCatalog.Resolve(restored.Preferences,false).Id);Assert.Equal("ocean",ThemeCatalog.Resolve(restored.Preferences,true).Id);Assert.Equal("cloud",ThemeCatalog.Resolve(new Preferences{LightPalette="removed"},false).Id);Assert.Equal("obsidian",ThemeCatalog.Resolve(new Preferences{DarkPalette="removed"},true).Id);
    }
    private static double Contrast(string a,string b){static double L(string color){var x=new[]{1,3,5}.Select(i=>Convert.ToInt32(color.Substring(i,2),16)/255d).Select(v=>v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4)).ToArray();return .2126*x[0]+.7152*x[1]+.0722*x[2];}var x=L(a);var y=L(b);return(Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
}
