package app.moji.android.data

// Structural blocks not represented by the inline editor stay editable as native widgets.
data class RichBlock(val kind:String="text",val content:String="",val rows:List<List<String>> = emptyList(),val checked:Boolean=false,val prefix:String="")
object RichBlocks {
    private val task=Regex("^(\\s*)[-*+] \\[([ xX])] (.*)$")
    private val image=Regex("!\\[[^]]*]\\((?:data:image/[^)]+|https?://[^)]+)\\)")
    fun cells(line:String):List<String> = line.trim().removePrefix("|").removeSuffix("|").split(Regex("(?<!\\\\)\\|")).map{it.trim().replace("\\|","|").replace("<br>","\n")}
    private fun separator(line:String)=cells(line).let{it.isNotEmpty()&&it.all{c->c.matches(Regex(":?-{3,}:?"))}}
    fun parse(markdown:String):List<RichBlock>{
        val lines=markdown.split('\n');val result=mutableListOf<RichBlock>();val text=mutableListOf<String>();var i=0
        fun flush(){if(text.any{it.isNotBlank()})result+=RichBlock(content=text.joinToString("\n").trim('\n'));text.clear()}
        while(i<lines.size){val line=lines[i];val fence=Regex("^\\s*(`{3,}|~{3,})(.*)$").matchEntire(line)
            when {
                fence!=null->{flush();val body=mutableListOf<String>();i++;while(i<lines.size&&!lines[i].trim().startsWith(fence.groupValues[1]))body+=lines[i++];result+=RichBlock("code",body.joinToString("\n"),prefix=fence.groupValues[1]+fence.groupValues[2]);i++}
                i+1<lines.size && line.contains('|') && separator(lines[i+1])->{flush();val rows=mutableListOf(cells(line));i+=2;while(i<lines.size&&lines[i].contains('|')&&lines[i].isNotBlank())rows+=cells(lines[i++]);val width=rows.maxOf{it.size};result+=RichBlock("table",rows=rows.map{it+List(width-it.size){""}})}
                task.matches(line)->{flush();val m=task.matchEntire(line)!!;result+=RichBlock("task",m.groupValues[3],checked=m.groupValues[2].lowercase()=="x",prefix=m.groupValues[1]);i++}
                line.trimStart().startsWith('>')->{flush();val body=mutableListOf<String>();while(i<lines.size&&lines[i].trimStart().startsWith('>'))body+=lines[i++].trimStart().removePrefix(">").removePrefix(" ");result+=RichBlock("quote",body.joinToString("\n"))}
                image.containsMatchIn(line)->{flush();var at=0;for(m in image.findAll(line)){if(m.range.first>at)result+=RichBlock(content=line.substring(at,m.range.first));result+=RichBlock("image",m.value);at=m.range.last+1};if(at<line.length)result+=RichBlock(content=line.substring(at));i++}
                line.trim().matches(Regex("-{3,}|\\*{3,}|_{3,}"))->{flush();result+=RichBlock("rule");i++}
                else->{text+=line;i++}
            }
        };flush();if(result.isEmpty()||result.last().kind!="text")result+=RichBlock();return result
    }
    fun serialize(blocks:List<RichBlock>)=blocks.joinToString("\n\n"){b->when(b.kind){
        "table"->b.rows.mapIndexed{i,row->"| "+row.joinToString(" | "){it.replace(Regex("(?<!\\\\)\\|")){"\\|"}.replace("\n","<br>") }+" |"+if(i==0)"\n| "+row.joinToString(" | "){"---"}+" |" else ""}.joinToString("\n")
        "task"->b.prefix+"- ["+(if(b.checked)"x" else " ")+"] "+b.content
        "quote"->"> "+b.content.replace("\n","\n> ")
        "code"->{val fence=b.prefix.takeWhile{it=='`'||it=='~'}.ifEmpty{"```"};b.prefix.ifEmpty{"```"}+"\n"+b.content+"\n"+fence}
        "rule"->"---"
        else->b.content
    }}.trimEnd()
}
