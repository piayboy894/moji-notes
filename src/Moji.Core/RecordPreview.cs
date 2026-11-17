using System.Net;
using System.Text;

namespace Moji.Core;

// Card excerpts are bounded text scans, not full HTML documents. In particular,
// a 20 MB image attribute never becomes a DOM tree on the WPF input thread.
public static class RecordPreview
{
    public static string Text(Note note, int limit = 82)
    {
        var content = note.Content;
        if (!EditorContent.IsDocument(content) && note.Variables.Count > 0)
            content = PromptVariables.Display(note with { Content = content[..Math.Min(content.Length, 4096)] });
        var html = EditorContent.IsDocument(content); var result = new StringBuilder(limit + 1); var i = 0;
        void Append(char ch) { if (char.IsWhiteSpace(ch)) { if (result.Length > 0 && result[^1] != ' ') result.Append(' '); } else result.Append(ch); }
        while (i < content.Length && result.Length <= limit)
        {
            if (html && content[i] == '<')
            {
                if (content.AsSpan(i).StartsWith("<!--")) { var end = content.IndexOf("-->", i + 4, StringComparison.Ordinal); i = end < 0 ? content.Length : end + 3; continue; }
                var start = i + 1; if (start < content.Length && content[start] == '/') start++;
                var nameEnd = start; while (nameEnd < content.Length && char.IsAsciiLetterOrDigit(content[nameEnd])) nameEnd++;
                var tag = content.AsSpan(start, nameEnd - start); var endTag = nameEnd;
                while (endTag < content.Length)
                {
                    var next = content.AsSpan(endTag).IndexOfAny('>', '\'', '"'); if (next < 0) { endTag = content.Length; break; }
                    endTag += next; if (content[endTag] == '>') break;
                    var quote = content[endTag]; var close = content.IndexOf(quote, endTag + 1); if (close < 0) { endTag = content.Length; break; } endTag = close + 1;
                }
                if (tag.Equals("script", StringComparison.OrdinalIgnoreCase) || tag.Equals("style", StringComparison.OrdinalIgnoreCase))
                { var close = content.IndexOf("</" + tag.ToString(), Math.Min(endTag + 1, content.Length), StringComparison.OrdinalIgnoreCase); i = close < 0 ? content.Length : content.IndexOf('>', close) + 1; if (i <= 0) i = content.Length; continue; }
                if (tag.Equals("img", StringComparison.OrdinalIgnoreCase)) { Append(' '); foreach (var ch in "图片") Append(ch); Append(' '); }
                if (tag.Equals("br", StringComparison.OrdinalIgnoreCase) || tag.Equals("p", StringComparison.OrdinalIgnoreCase) || tag.Equals("li", StringComparison.OrdinalIgnoreCase) || tag.Equals("blockquote", StringComparison.OrdinalIgnoreCase) || (tag.Length == 2 && (tag[0] is 'h' or 'H') && char.IsAsciiDigit(tag[1]))) Append(' ');
                i = Math.Min(endTag + 1, content.Length); continue;
            }
            if (html && content[i] == '&')
            {
                var end = content.IndexOf(';', i + 1, Math.Min(64, content.Length - i - 1));
                if (end >= 0) { foreach (var ch in WebUtility.HtmlDecode(content[i..(end + 1)])) { Append(ch); if (result.Length > limit) break; } i = end + 1; continue; }
            }
            Append(content[i++]);
        }
        var excerpt = result.ToString().Trim(); return excerpt.Length > limit ? excerpt[..limit] + "…" : excerpt;
    }
}
