using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace Moji.App;

// Markdown is an interchange format only. The editor always uses native formatted text.
internal static class RichDocument
{
    public static string Serialize(FlowDocument document) => string.Join("\n\n", Blocks(document.Blocks)).TrimEnd();
    private static IEnumerable<string> Blocks(BlockCollection blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph p:
                    var text = InlineText(p.Inlines);
                    if (p.Tag is string code && code == "code") { var plain = new TextRange(p.ContentStart,p.ContentEnd).Text.TrimEnd('\r','\n'); var fence = plain.Contains("```") ? "````" : "```"; yield return fence + "\n" + plain + "\n" + fence; }
                    else yield return (p.Tag is int level ? new string('#',level)+" " : p.Tag?.ToString()=="task" ? "- " : "") + text;
                    break;
                case System.Windows.Documents.List list:
                    var index = list.StartIndex; var lines = new List<string>();
                    foreach (var item in list.ListItems) { var content = string.Join("\n\n",Blocks(item.Blocks)); var marker = list.MarkerStyle == TextMarkerStyle.Decimal ? $"{index++}. " : "- "; lines.Add(marker + content.Replace("\n","\n"+new string(' ',marker.Length))); }
                    yield return string.Join("\n",lines); break;
                case Section section:
                    var body = string.Join("\n\n",Blocks(section.Blocks)); yield return section.Tag?.ToString() == "quote" ? "> " + body.Replace("\n","\n> ") : body; break;
                case Table table:
                    var rows=table.RowGroups.SelectMany(g=>g.Rows).ToList(); if(rows.Count==0)break;
                    var count=rows.Max(r=>r.Cells.Count); var output=new List<string>();
                    for(var i=0;i<rows.Count;i++){var cells=rows[i].Cells.Select(c=>string.Join(" ",Blocks(c.Blocks)).Replace("\n","<br>")).ToList();while(cells.Count<count)cells.Add("");output.Add("| "+string.Join(" | ",cells)+" |");if(i==0)output.Add("| "+string.Join(" | ",Enumerable.Repeat("---",count))+" |");}
                    yield return string.Join("\n",output); break;
                case BlockUIContainer: yield return "---"; break;
            }
        }
    }
    public static string Escape(string text) => Regex.Replace(text.Replace("\\","\\\\"), @"([`*_\[\]#~<>&!+\-.|])", @"\$1");
    private static string InlineText(InlineCollection inlines, bool bold=false, bool italic=false, bool strike=false)
    {
        var output=new StringBuilder();
        foreach(var inline in inlines)
        {
            if(inline.Tag is string original && original.StartsWith("image:")){ output.Append("![图片]("+original[6..]+")");continue; }
            if(inline is InlineUIContainer { Child:CheckBox check }){output.Append(check.IsChecked==true?"[x] ":"[ ] ");continue;}
            if(inline is LineBreak){output.Append("  \n");continue;}
            var isBold=inline.FontWeight>=FontWeights.SemiBold && inline.Parent is not Paragraph { Tag:int };
            var isItalic=inline.FontStyle==FontStyles.Italic;
            var isStrike=inline.TextDecorations?.Any(t=>t.Location==TextDecorationLocation.Strikethrough)==true;
            string text;
            if(inline is Run run){text=Escape(run.Text);if(run.Tag?.ToString()=="code")text="`"+run.Text.Replace("`","\\`")+"`";}
            else if(inline is Span span)text=InlineText(span.Inlines,bold||isBold,italic||isItalic,strike||isStrike);
            else continue;
            if(isStrike&&!strike)text=Wrap("~~",text);
            if(isItalic&&!italic)text=Wrap("*",text);
            if(isBold&&!bold)text=Wrap("**",text);
            if(inline is Hyperlink link && link.NavigateUri!=null)text="["+text+"]("+link.NavigateUri.AbsoluteUri.Replace(")","%29")+")";
            output.Append(text);
        }
        return output.ToString();
    }
    private static string Wrap(string mark,string text){var trimmed=text.Trim();if(trimmed.Length==0)return text;var leading=text.Length-text.TrimStart().Length;return text[..leading]+mark+trimmed+mark+text[(leading+trimmed.Length)..];}
    public static string PlainText(Moji.Core.Note note)=>Moji.Core.PromptVariables.PlainText(note,note.Variables.ToDictionary(v=>v.Id,v=>v.Name),source=>{var document=MarkdownDocument.Render(source);return new TextRange(document.ContentStart,document.ContentEnd).Text.TrimEnd();});
    public static (string Text,List<TextPointer> Positions) TextMap(FlowDocument document)
    {
        var text=new StringBuilder();var positions=new List<TextPointer>();var pointer=document.ContentStart;
        while(pointer!=null){var context=pointer.GetPointerContext(LogicalDirection.Forward);
            if(context==TextPointerContext.Text){var value=pointer.GetTextInRun(LogicalDirection.Forward);for(var i=0;i<value.Length;i++){positions.Add(pointer.GetPositionAtOffset(i)!);text.Append(value[i]);}}
            else if(context==TextPointerContext.ElementEnd && pointer.Parent is Paragraph){positions.Add(pointer);text.Append('\n');}
            else if(context==TextPointerContext.ElementStart && pointer.GetAdjacentElement(LogicalDirection.Forward) is LineBreak){positions.Add(pointer);text.Append('\n');}
            pointer=pointer.GetNextContextPosition(LogicalDirection.Forward);}
        positions.Add(document.ContentEnd);return (text.ToString(),positions);
    }
}
