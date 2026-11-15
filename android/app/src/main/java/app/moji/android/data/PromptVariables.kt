package app.moji.android.data
import kotlinx.serialization.Serializable
@Serializable data class PromptVariable(val id:String=uid(),val name:String="")
object PromptVariables {
    private val pattern=Regex("!\\[[^]]*]\\(moji-variable:([a-f0-9]{32})\\)")
    fun token(v:PromptVariable)="![变量](moji-variable:${v.id})"
    fun named(note:Note,name:String):PromptVariable {val value=name.trim();require(value.length in 1..40&&value.none{it.isISOControl()}){"变量名称需为 1–40 个字符"};return note.variables.firstOrNull{it.name==value}?:PromptVariable(name=value)}
    fun used(note:Note):List<PromptVariable>{val ids=pattern.findAll(note.content).map{it.groupValues[1]}.toSet();return note.variables.filter{it.id in ids}}
    fun display(note:Note)=fill(note,note.variables.associate{it.id to it.name})
    fun plainText(note:Note,values:Map<String,String>,render:(String)->String):String {
        val placeholders=used(note).associate{it.id to "MOJIVAR${it.id}MOJI"}
        return Regex("MOJIVAR([a-f0-9]{32})MOJI").replace(render(fill(note,placeholders))){values[it.groupValues[1]]?:it.value}
    }
    fun fill(note:Note,values:Map<String,String>)=pattern.replace(note.content){m->val v=note.variables.firstOrNull{it.id==m.groupValues[1]};v?.let{values[it.id]}?:m.value}
}
