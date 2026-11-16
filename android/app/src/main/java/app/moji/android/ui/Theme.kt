package app.moji.android.ui

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp
import app.moji.android.data.*

@Composable fun MojiTheme(preferences:Preferences=Preferences(),content:@Composable ()->Unit){
    val dark=preferences.theme=="dark"||(preferences.theme=="system"&&isSystemInDarkTheme())
    val palette=ThemeCatalog.resolve(preferences,dark);fun c(key:String)=color(palette.colors.getValue(key))
    val base=if(dark)darkColorScheme()else lightColorScheme()
    val scheme=base.copy(primary=c("Accent"),onPrimary=c("OnAccent"),primaryContainer=c("AccentLight"),onPrimaryContainer=c("OnSelected"),secondary=c("Accent"),onSecondary=c("OnAccent"),secondaryContainer=c("Soft"),onSecondaryContainer=c("Ink"),tertiary=c("Accent"),onTertiary=c("OnAccent"),background=c("Bg"),onBackground=c("Ink"),surface=c("Panel"),onSurface=c("Ink"),surfaceVariant=c("Soft"),onSurfaceVariant=c("Sub"),surfaceTint=Color.Transparent,outline=c("Outline"),outlineVariant=c("Line"),error=c("Danger"),onError=c("OnDanger"),errorContainer=c("DangerSoft"),onErrorContainer=c("Danger"),inverseSurface=c("Ink"),inverseOnSurface=c("Panel"),inversePrimary=c("OnAccent"),surfaceDim=c("Side"),surfaceBright=c("Raised"),surfaceContainerLowest=c("Bg"),surfaceContainerLow=c("Side"),surfaceContainer=c("Soft"),surfaceContainerHigh=c("Raised"),surfaceContainerHighest=c("Hover"))
    MaterialTheme(colorScheme=scheme,typography=Typography(titleLarge=androidx.compose.ui.text.TextStyle(fontSize=24.sp,fontWeight=FontWeight.SemiBold),titleMedium=androidx.compose.ui.text.TextStyle(fontSize=17.sp,fontWeight=FontWeight.Medium),bodyLarge=androidx.compose.ui.text.TextStyle(fontSize=15.sp,lineHeight=24.sp),bodyMedium=androidx.compose.ui.text.TextStyle(fontSize=13.sp,lineHeight=21.sp),labelLarge=androidx.compose.ui.text.TextStyle(fontSize=13.sp,fontWeight=FontWeight.Medium)),content=content)
}
fun color(value:String):Color=runCatching{Color(android.graphics.Color.parseColor(value))}.getOrDefault(Color(0xFF75876D))
@Composable fun modeColor(mode:NoteMode)=when(mode){NoteMode.Text->MaterialTheme.colorScheme.primary;NoteMode.Prompt->MaterialTheme.colorScheme.secondary;NoteMode.Password->MaterialTheme.colorScheme.tertiary}
