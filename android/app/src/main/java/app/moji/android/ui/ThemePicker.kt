package app.moji.android.ui
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.Alignment
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.*
import androidx.compose.ui.unit.dp
import app.moji.android.MojiViewModel
import app.moji.android.data.*
@Composable fun ThemePicker(p:Preferences,vm:MojiViewModel){
    Text("外观",style=MaterialTheme.typography.labelLarge)
    Row(Modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(8.dp)){listOf("light" to "浅色","dark" to "深色","system" to "跟随系统").forEach{(id,name)->FilterChip(p.theme==id,{vm.preferences{it.copy(theme=id)}},label={Text(name)})}}
    val active=ThemeCatalog.resolve(p,p.theme=="dark"||(p.theme=="system"&&androidx.compose.foundation.isSystemInDarkTheme())).id
    for(dark in listOf(false,true)){
        Text(if(dark)"深色主题"else"浅色主题",style=MaterialTheme.typography.labelLarge,modifier=Modifier.padding(top=8.dp))
        Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(9.dp)){
            ThemeCatalog.all.filter{it.dark==dark}.forEach{palette->val c=palette.colors;val chosen=palette.id==active
                Surface(onClick={vm.preferences{if(dark)it.copy(theme="dark",darkPalette=palette.id)else it.copy(theme="light",lightPalette=palette.id)}},modifier=Modifier.weight(1f).testTag("theme-${palette.id}").semantics{contentDescription=palette.name;selected=chosen},shape=RoundedCornerShape(9.dp),color=color(c.getValue("Panel")),contentColor=color(c.getValue("Ink")),border=BorderStroke(if(chosen)2.dp else 1.dp,color(c.getValue(if(chosen)"Accent"else"Line")))){
                    Column{Row(Modifier.fillMaxWidth().height(64.dp)){Box(Modifier.width(23.dp).fillMaxHeight().background(color(c.getValue("Side"))));Column(Modifier.weight(1f).padding(9.dp,12.dp),verticalArrangement=Arrangement.spacedBy(7.dp)){Box(Modifier.width(32.dp).height(4.dp).background(color(c.getValue("Ink")),RoundedCornerShape(2.dp)));Box(Modifier.fillMaxWidth().height(3.dp).background(color(c.getValue("Line")),RoundedCornerShape(2.dp)));Box(Modifier.fillMaxWidth().height(12.dp).background(color(c.getValue("AccentLight")),RoundedCornerShape(3.dp)))}}
                        Row(Modifier.fillMaxWidth().padding(9.dp),verticalAlignment=Alignment.CenterVertically){Text(palette.name,Modifier.weight(1f),style=MaterialTheme.typography.labelMedium);if(chosen)Icon(MojiIcons.Check,null,Modifier.size(14.dp),tint=color(c.getValue("Accent")))}}
                }
            }
        }
    }
}
