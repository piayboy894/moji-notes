using AngleSharp.Html.Parser;
namespace Moji.Core;
public static class EditorContent
{
    public const string Prefix="<!--moji:tiptap:1-->";
    public static bool IsDocument(string content)=>content.StartsWith(Prefix,StringComparison.Ordinal);
    public static string Html(string content)=>IsDocument(content)?content[Prefix.Length..]:"<p>"+System.Net.WebUtility.HtmlEncode(content).Replace("\n","</p><p>")+"</p>";
    public static string Plain(string content)
    {
        if(!IsDocument(content))return content;
        var document=new HtmlParser().ParseDocument(content[Prefix.Length..]);
        foreach(var element in document.QuerySelectorAll("script,style"))element.Remove();
        foreach(var image in document.QuerySelectorAll("img"))image.Parent?.ReplaceChild(document.CreateTextNode(image.GetAttribute("alt")??"图片"),image);
        foreach(var element in document.QuerySelectorAll("br"))element.Parent?.ReplaceChild(document.CreateTextNode("\n"),element);
        foreach(var element in document.QuerySelectorAll("p,h1,h2,h3,h4,h5,h6,li,blockquote,pre,tr"))element.AppendChild(document.CreateTextNode("\n"));
        return (document.Body?.TextContent??"").TrimEnd();
    }
}
