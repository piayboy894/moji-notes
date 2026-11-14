using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Moji.App;
internal static class MarkdownHighlighting
{
    public static IHighlightingDefinition Create(bool dark)
    {
        var green=dark?"#B2CB9A":"#638052";var brown=dark?"#CDB38D":"#AC8A5E";var faint=dark?"#90A282":"#9CAA8E";
        var xml=$$"""
        <SyntaxDefinition name="Moji Markdown" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
          <Color name="Heading" foreground="{{green}}" fontWeight="bold"/>
          <Color name="Variable" foreground="{{brown}}" fontWeight="bold"/>
          <Color name="Code" foreground="{{brown}}"/>
          <Color name="Markup" foreground="{{faint}}"/>
          <RuleSet>
            <Rule color="Heading">^\s*\#{1,6}\s.*$</Rule>
            <Rule color="Variable">\{\{[^{}]+\}\}</Rule>
            <Rule color="Code">`[^`]+`</Rule>
            <Rule color="Markup">^\s*(&gt;|[-*+]\s|[0-9]+\.\s)</Rule>
            <Span color="Code" begin="^```" end="^```"/>
          </RuleSet>
        </SyntaxDefinition>
        """;
        using var input=new StringReader(xml);using var reader=XmlReader.Create(input);return HighlightingLoader.Load(reader,HighlightingManager.Instance);
    }
}
