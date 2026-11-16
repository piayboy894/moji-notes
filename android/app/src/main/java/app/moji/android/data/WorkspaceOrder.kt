package app.moji.android.data
object CredentialFields {
    fun valid(fields:List<CredentialField>)=fields.size<=128&&fields.map{it.id}.distinct().size==fields.size&&fields.all{it.id.matches(Regex("[a-f0-9]{32}"))&&it.name.isNotBlank()&&it.name.length<=80&&it.name.none(Char::isISOControl)&&it.value.length<=2_000_000}
}
object NotebookOrder {
    fun move(state:Workspace,source:String,target:String,after:Boolean=false):Workspace {
        val live=state.liveBooks.toMutableList();val item=live.firstOrNull{it.id==source}?:return state
        if(source==target||live.none{it.id==target})return state
        live.remove(item);live.add(live.indexOfFirst{it.id==target}+(if(after)1 else 0),item);var index=0
        return state.copy(notebooks=state.notebooks.map{if(it.deletedAt==null)live[index++] else it})
    }
}
