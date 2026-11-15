using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Markdig;
using Moji.Core;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;

namespace Moji.App;
public static class MarkdownDocument
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    public static FlowDocument Render(string markdown, double fontSize = 15, IReadOnlyList<PromptVariable>? variables = null)
    {
        var document = new FlowDocument { FontFamily = new FontFamily("Microsoft YaHei UI"), FontSize = fontSize, Foreground = Ui.Resource("Ink"), Background = Ui.Resource("Panel"), PagePadding = new Thickness(28,24,28,35), LineHeight = fontSize * 1.95, ColumnWidth = double.PositiveInfinity };
        foreach (var block in Markdig.Markdown.Parse(markdown, Pipeline)) AddBlock(document.Blocks, block, fontSize, variables ?? []);
        if (document.Blocks.Count == 0) document.Blocks.Add(new System.Windows.Documents.Paragraph());
        return document;
    }
    public static Border VariableChip(PromptVariable variable) {
        var label=Ui.Text(variable.Name,13);label.LineHeight=18;label.LineStackingStrategy=LineStackingStrategy.BlockLineHeight;label.TextWrapping=TextWrapping.NoWrap;label.TextAlignment=TextAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;label.HorizontalAlignment=HorizontalAlignment.Center;
        return new Border { Child=label,Background=Ui.Resource("AccentLight"),CornerRadius=new CornerRadius(5),Padding=new Thickness(8,4,8,4),Margin=new Thickness(2,0,2,0),ToolTip="变量："+variable.Name };
    }
    private static void AddBlock(BlockCollection target, Markdig.Syntax.Block block, double size, IReadOnlyList<PromptVariable> variables)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var title = Paragraph(heading.Inline, variables); title.Tag = heading.Level; title.FontSize = size * (heading.Level == 1 ? 1.8 : heading.Level == 2 ? 1.4 : 1.15); title.FontWeight = FontWeights.SemiBold; title.Margin = new Thickness(0,24,0,13); target.Add(title); break;
            case ParagraphBlock paragraph: var p = Paragraph(paragraph.Inline, variables); p.Margin = new Thickness(0,0,0,17); target.Add(p); break;
            case Markdig.Syntax.ListBlock list:
                var nativeList = new System.Windows.Documents.List { MarkerStyle = list.Descendants<ParagraphBlock>().Any(p=>p.Inline?.Descendants<TaskList>().Any()==true) ? TextMarkerStyle.None : list.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new Thickness(5,6,0,18), Padding = new Thickness(19,0,0,0) };
                foreach (ListItemBlock item in list) { var nativeItem = new System.Windows.Documents.ListItem(); foreach (var child in item) AddBlock(nativeItem.Blocks, child, size, variables); nativeList.ListItems.Add(nativeItem); } target.Add(nativeList); break;
            case QuoteBlock quote:
                var section = new Section { Tag = "quote", BorderBrush = Ui.Brush("#B9C8AA"), BorderThickness = new Thickness(3,0,0,0), Padding = new Thickness(15,7,12,7), Background = Ui.Resource("Soft"), Margin = new Thickness(0,15,0,22) }; foreach (var child in quote) AddBlock(section.Blocks, child, size, variables); target.Add(section); break;
            case CodeBlock code:
                var codeText = new System.Windows.Documents.Paragraph(new Run(code.Lines.ToString())) { Tag = "code", FontFamily = new FontFamily("Consolas"), FontSize = size * .88, Background = Ui.Resource("Soft"), Padding = new Thickness(16), Margin = new Thickness(0,10,0,22), BorderBrush = Ui.Resource("Line"), BorderThickness = new Thickness(1), LineHeight = size * 1.65 }; target.Add(codeText); break;
            case Markdig.Extensions.Tables.Table table:
                var nativeTable = new System.Windows.Documents.Table { CellSpacing = 0, Margin = new Thickness(0,13,0,23), FontSize = size * .88 }; var rows = new TableRowGroup();
                foreach (Markdig.Extensions.Tables.TableRow row in table) { var nativeRow = new System.Windows.Documents.TableRow(); foreach (Markdig.Extensions.Tables.TableCell cell in row) { var nativeCell = new System.Windows.Documents.TableCell { BorderBrush = Ui.Resource("Line"), BorderThickness = new Thickness(.5), Padding = new Thickness(10,7,10,7), Background = row.IsHeader ? Ui.Resource("Soft") : Brushes.Transparent }; foreach (var child in cell) AddBlock(nativeCell.Blocks, child, size, variables); if (row.IsHeader) nativeCell.FontWeight = FontWeights.SemiBold; nativeRow.Cells.Add(nativeCell); } rows.Rows.Add(nativeRow); } nativeTable.RowGroups.Add(rows); target.Add(nativeTable); break;
            case ThematicBreakBlock: target.Add(new BlockUIContainer(new Border { Height = 1, Background = Ui.Resource("Line"), Margin = new Thickness(0,15,0,15) })); break;
            case HtmlBlock html: target.Add(new System.Windows.Documents.Paragraph(new Run(html.Lines.ToString()))); break;
            case ContainerBlock container: foreach (var child in container) AddBlock(target, child, size, variables); break;
        }
    }
    private static System.Windows.Documents.Paragraph Paragraph(ContainerInline? container, IReadOnlyList<PromptVariable> variables) { var p = new System.Windows.Documents.Paragraph(); if (container != null) AddInline(p.Inlines, container, variables); return p; }
    private static void AddInline(InlineCollection target, ContainerInline container, IReadOnlyList<PromptVariable> variables)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal: target.Add(new Run(literal.Content.ToString())); break;
                case LineBreakInline: target.Add(new LineBreak()); break;
                case CodeInline code: target.Add(new Run(code.Content) { Tag = "code", FontFamily = new FontFamily("Consolas"), Background = Ui.Resource("Soft") }); break;
                case EmphasisInline emphasis: Span span = emphasis.DelimiterChar == '~' ? new Span { TextDecorations = TextDecorations.Strikethrough } : emphasis.DelimiterCount >= 2 ? new Bold() : new Italic(); AddInline(span.Inlines, emphasis, variables); target.Add(span); break;
                case LinkInline link when link.IsImage:
                    if(link.Url?.StartsWith("moji-variable:")==true && variables.FirstOrDefault(v=>"moji-variable:"+v.Id==link.Url) is {} variable) { target.Add(new InlineUIContainer(VariableChip(variable)){Tag="image:"+link.Url,BaselineAlignment=BaselineAlignment.Center}); }
                    else if (link.Url?.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) == true)
                    { try { var encoded = link.Url[(link.Url.IndexOf(',')+1)..]; if (encoded.Length > 4_000_000) break; var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.StreamSource = new MemoryStream(Convert.FromBase64String(encoded)); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.EndInit(); bitmap.Freeze(); target.Add(new InlineUIContainer(new Image { Source = bitmap, MaxWidth = 640, MaxHeight = 450, Stretch = Stretch.Uniform, Margin = new Thickness(0,12,0,12) }) { Tag = "image:" + link.Url }); } catch { target.Add(new Run("[无法读取图片]")); } }
                    else target.Add(new Run("图片") { Tag = "image:" + link.Url }); break;
                case LinkInline link:
                    if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http") { var hyperlink = new Hyperlink { NavigateUri = uri, Foreground = Ui.Resource("Accent") }; AddInline(hyperlink.Inlines, link, variables); hyperlink.RequestNavigate += (_, e) => { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; }; target.Add(hyperlink); } else AddInline(target, link, variables); break;
                case TaskList task: target.Add(new InlineUIContainer(new CheckBox { IsChecked = task.Checked, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) })); break;
                case HtmlInline html: if(html.Tag.ToLowerInvariant() is "<br>" or "<br/>" or "<br />")target.Add(new LineBreak());else target.Add(new Run(html.Tag)); break;
                case ContainerInline nested: AddInline(target, nested, variables); break;
            }
        }
    }
}
