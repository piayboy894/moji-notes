package app.moji.android.data

import android.graphics.Typeface
import android.os.Build
import android.util.Xml
import android.webkit.WebResourceResponse
import java.io.File
import java.security.MessageDigest
import org.xmlpull.v1.XmlPullParser

/** Only readable OS font files are exposed; no arbitrary local paths enter the WebView. */
data class DisplayFont(val id:String,val name:String,val file:File?=null,val index:Int=0){
    val resource:String get()=MessageDigest.getInstance("SHA-256").digest((file?.path+":"+index).toByteArray()).joinToString(""){"%02x".format(it)}+".ttf"
}
object InstalledFonts {
    val all:List<DisplayFont> by lazy {
        val entries=linkedMapOf<String,DisplayFont>();entries[""]=DisplayFont("","系统默认")
        val roots=listOf("/system","/system_ext","/product","/vendor")
        for(root in roots)for(name in listOf("fonts.xml","fonts_customization.xml"))runCatching{
            File("$root/etc/$name").inputStream().use { stream ->
                val parser=Xml.newPullParser();parser.setInput(stream,null);var family:String?=null;var first:DisplayFont?=null
                while(parser.eventType!=XmlPullParser.END_DOCUMENT){
                    when {
                        parser.eventType==XmlPullParser.START_TAG&&parser.name=="family"->{family=parser.getAttributeValue(null,"name");first=null}
                        parser.eventType==XmlPullParser.START_TAG&&parser.name=="font"&&family!=null->{val index=parser.getAttributeValue(null,"index")?.toIntOrNull()?:0;val weight=parser.getAttributeValue(null,"weight")?.toIntOrNull()?:400;val italic=parser.getAttributeValue(null,"style")=="italic";val depth=parser.depth;val text=StringBuilder();while(true){val event=parser.next();if(event==XmlPullParser.END_DOCUMENT||event==XmlPullParser.END_TAG&&parser.depth==depth)break;if(event==XmlPullParser.TEXT&&parser.depth==depth)text.append(parser.text)};val filename=text.toString().trim();val file=roots.map{File("$it/fonts/$filename")}.firstOrNull{it.isFile&&it.canRead()};if(file!=null&&(first==null||weight==400&&!italic))first=DisplayFont(family!!,family!!.replace('-',' '),file,index)}
                        parser.eventType==XmlPullParser.END_TAG&&parser.name=="family"->{first?.let{entries[it.id]=it};family=null;first=null}
                    };parser.next()
                }
            }
        }
        // Public Android API also includes OEM and downloaded system font files.
        if(Build.VERSION.SDK_INT>=29)runCatching{android.graphics.fonts.SystemFonts.getAvailableFonts().filter{it.style.weight==400&&it.style.slant==0&&it.ttcIndex==0}.forEach{font->font.file?.takeIf{it.isFile&&it.canRead()}?.let{file->if(entries.values.none{it.file==file&&it.index==font.ttcIndex}){val id="file:"+file.canonicalPath+":"+font.ttcIndex;entries[id]=DisplayFont(id,file.nameWithoutExtension.replace('-',' ')+(if(font.ttcIndex>0)" ${font.ttcIndex}"else""),file,font.ttcIndex)}}}}
        listOf(entries.getValue(""))+entries.values.filter{it.id.isNotEmpty()}.sortedBy{it.name.lowercase()}
    }
    fun resolve(id:String)=if(id.isEmpty())DisplayFont("","系统默认")else all.firstOrNull{it.id==id}?:DisplayFont("","系统默认")
    fun typeface(id:String):Typeface? {val font=resolve(id);return if(font.id.isEmpty())null else runCatching{if(font.id.startsWith("file:")&&font.file!=null)Typeface.Builder(font.file).setTtcIndex(font.index).build()else Typeface.create(font.id,Typeface.NORMAL)}.getOrNull()}
    fun url(id:String):String=resolve(id).takeIf{it.file!=null&&it.index==0}?.let{"https://appassets.androidplatform.net/fonts/"+it.resource}?:""
    fun response(path:String):WebResourceResponse?=all.firstOrNull{it.file!=null&&it.resource==path}?.file?.let{runCatching{WebResourceResponse("font/ttf",null,it.inputStream())}.getOrNull()}
}
