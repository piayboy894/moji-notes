using Moji.Core;
using Xunit;
namespace Moji.Tests;
public class RecordPreviewTests
{
    [Fact] public void HugeInlineImagesDoNotAllocateAHtmlDocumentForACard()
    {
        var note = new Note { Content = EditorContent.Prefix + "<p>开头 &amp; 内容</p><img src=\"data:image/png;base64," + new string('A', 12_000_000) + "\"><p>图片之后</p>" };
        RecordPreview.Text(new Note());var before=GC.GetAllocatedBytesForCurrentThread();var result=RecordPreview.Text(note);var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Contains("开头 & 内容",result);Assert.Contains("图片之后",result);Assert.DoesNotContain("base64",result);Assert.True(allocated<65536,$"Preview allocated {allocated} bytes");
    }
    [Fact] public void ExcerptsStopAfterVisibleTextAndKeepLiteralDocumentsLiteral()
    {
        Assert.Equal("abc…",RecordPreview.Text(new Note{Content=EditorContent.Prefix+"<p>abcd</p>"},3));
        Assert.Equal("# 标题 {{原样}}",RecordPreview.Text(new Note{Content="# 标题\n{{原样}}"}));
        Assert.Equal("安全 文字",RecordPreview.Text(new Note{Content=EditorContent.Prefix+"<script>bad</script><p>安全</p><style>.bad{}</style><p>文字</p>"}));
    }
}
