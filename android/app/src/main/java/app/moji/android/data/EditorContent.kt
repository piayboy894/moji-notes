package app.moji.android.data
import org.jsoup.Jsoup
object EditorContent {
    const val PREFIX="<!--moji:tiptap:2-->"
    private const val LEGACY_PREFIX="<!--moji:tiptap:1-->"
    fun isDocument(content:String)=(content.startsWith(PREFIX)||content.startsWith(LEGACY_PREFIX))
    fun html(content:String)=if(isDocument(content))content.substring(PREFIX.length)else "<p>"+org.jsoup.nodes.Entities.escape(content).replace("\n","</p><p>")+"</p>"
    fun plain(content:String):String {
        if(!isDocument(content))return content
        val doc=Jsoup.parseBodyFragment(content.substring(PREFIX.length))
        doc.select("script,style").remove()
        doc.select("img").forEach{it.replaceWith(org.jsoup.nodes.TextNode(it.attr("alt").ifBlank{"图片"}))}
        doc.select("br").forEach{it.replaceWith(org.jsoup.nodes.TextNode("\n"))}
        doc.select("p,h1,h2,h3,h4,h5,h6,li,blockquote,pre,tr").forEach{it.appendChild(org.jsoup.nodes.TextNode("\n"))}
        return doc.body().wholeText().trimEnd()
    }
}
