package app.moji.android.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import app.moji.android.MojiViewModel
import app.moji.android.data.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

@Composable fun DisplayFontSetting(p:Preferences,vm:MojiViewModel){
    var open by remember{mutableStateOf(false)}
    Text("显示字体",style=MaterialTheme.typography.labelLarge)
    OutlinedButton(onClick={open=true},modifier=Modifier.fillMaxWidth().testTag("choose-display-font")){Text(InstalledFonts.resolve(p.fontFamily).name,Modifier.weight(1f));Icon(MojiIcons.ExpandMore,null)}
    if(open){var query by remember{mutableStateOf("")};var selected by remember{mutableStateOf(p.fontFamily)}
        val fonts by produceState<List<DisplayFont>>(emptyList()){value=withContext(Dispatchers.IO){InstalledFonts.all}}
        // Refresh the dialog footer along with asynchronous font loading and
        // selection; the separately composed list must not leave a stale action.
        val canApply=fonts.isNotEmpty();val chosenFont=selected
        PanelDialog("显示字体",{open=false},width=560,actions={TextButton(onClick={open=false}){Text("取消")};Button(onClick={vm.preferences{it.copy(fontFamily=chosenFont)};open=false},enabled=canApply,modifier=Modifier.testTag("apply-display-font")){Text("应用")}}){
            OutlinedTextField(query,{query=it},Modifier.fillMaxWidth().testTag("font-search"),singleLine=true,placeholder={Text("搜索字体")},leadingIcon={Icon(MojiIcons.Search,null)})
            LazyColumn(Modifier.fillMaxWidth().height(340.dp).testTag("installed-font-list"),verticalArrangement=Arrangement.spacedBy(7.dp)){
                items(fonts.filter{it.name.contains(query,true)||it.id.contains(query,true)},key={it.id}){font->
                    Surface(onClick={selected=font.id},shape=MaterialTheme.shapes.medium,color=if(selected==font.id)color(LocalMojiPalette.current.colors.getValue("RecordSelected")) else MaterialTheme.colorScheme.surface,contentColor=if(selected==font.id)color(LocalMojiPalette.current.colors.getValue("RecordSelectedInk"))else MaterialTheme.colorScheme.onSurface,border=BorderStroke(1.dp,if(selected==font.id)MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant),modifier=Modifier.fillMaxWidth().testTag("font-${font.id}")){
                        Column(Modifier.padding(14.dp),verticalArrangement=Arrangement.spacedBy(6.dp)){Text(font.name,style=MaterialTheme.typography.bodyMedium);Text("墨记 Aa 123",style=MaterialTheme.typography.titleMedium,fontFamily=remember(font.id){InstalledFonts.typeface(font.id)?.let{FontFamily(it)}})}
                    }
                }
            }
        }
    }
}
