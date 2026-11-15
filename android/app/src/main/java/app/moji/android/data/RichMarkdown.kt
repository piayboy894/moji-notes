package app.moji.android.data

import org.jsoup.Jsoup
import org.jsoup.nodes.*

// Export native styles through HTML instead of losing literal symbols or embedded images.
object RichMarkdown {
    fun escape(text:String)=text.replace("\\","\\\\").replace(Regex("([`*_\\[\\]#~<>&!+\\-.|])"),"\\\\$1")
    fun fromHtml(html:String):String=children(Jsoup.parseBodyFragment(html).body()).trimEnd()
    private fun children(node:Node,heading:Boolean=false):String=node.childNodes().joinToString(""){render(it,heading)}
    private fun render(node:Node,heading:Boolean=false):String {
        if(node is TextNode)return escape(node.wholeText)
        if(node !is Element)return ""
        val tag=node.tagName();val inside=children(node,heading)
        return when(tag){
            "p","div"->{val first=node.selectFirst("[style*=font-size]");val size=first?.attr("style")?.let{Regex("font-size:\\s*([0-9.]+)em").find(it)?.groupValues?.get(1)?.toFloatOrNull()};val level=when{size==null->0;size>=2f->1;size>=1.5f->2;size>=1.17f->3;else->0};(if(level>0)"#".repeat(level)+" "+children(node,true) else inside)+"\n\n"}
            "b","strong"->if(heading)inside else wrap("**",inside)
            "i","em"->wrap("*",inside)
            "s","del","strike"->wrap("~~",inside)
            "a"->"["+inside+"]("+node.attr("href").replace(")","%29")+")"
            "br"->"  \n"
            "ul","ol"->node.children().mapIndexed{index,li->(if(tag=="ol")"${index+1}. " else "- ")+children(li).trimEnd().replace("\n","\n  ")}.joinToString("\n")+"\n\n"
            "code","rich-code"->"`"+node.text().replace("`","\\`")+"`"
            "img"->"![图片]("+node.attr("src")+")"
            "span"->{val style=node.attr("style");var value=inside;if(style.contains("line-through"))value=wrap("~~",value);value}
            else->inside
        }
    }
    private fun wrap(mark:String,text:String):String {val start=text.takeWhile{it.isWhitespace()};val end=text.takeLastWhile{it.isWhitespace()};val center=text.trim();return if(center.isEmpty())text else start+mark+center+mark+end}
}
