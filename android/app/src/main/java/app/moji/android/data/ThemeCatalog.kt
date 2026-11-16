package app.moji.android.data
import android.content.Context
import kotlinx.serialization.Serializable
@Serializable data class AppPalette(val id:String,val name:String,val dark:Boolean,val colors:Map<String,String>)
object ThemeCatalog {
    lateinit var all:List<AppPalette>;private set
    fun initialize(context:Context){if(!::all.isInitialized)all=context.assets.open("themes.json").bufferedReader().use{json.decodeFromString(it.readText())}}
    fun resolve(preferences:Preferences,dark:Boolean):AppPalette=all.firstOrNull{it.dark==dark&&it.id==if(dark)preferences.darkPalette else preferences.lightPalette}?:all.first{it.dark==dark}
}
