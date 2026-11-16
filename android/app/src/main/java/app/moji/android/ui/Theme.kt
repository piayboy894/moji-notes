package app.moji.android.ui

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.foundation.text.selection.LocalTextSelectionColors
import androidx.compose.foundation.text.selection.TextSelectionColors
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp
import app.moji.android.data.*

val LocalMojiPalette=staticCompositionLocalOf{ThemeCatalog.all.first()}

@Composable fun MojiTheme(preferences:Preferences=Preferences(),content:@Composable ()->Unit){
    val dark=preferences.theme=="dark"||(preferences.theme=="system"&&isSystemInDarkTheme())
    val palette=ThemeCatalog.resolve(preferences,dark);fun c(key:String)=color(palette.colors.getValue(key))
    val base=if(dark)darkColorScheme()else lightColorScheme()
    val scheme=base.copy(primary=c("Accent"),onPrimary=c("OnAccent"),primaryContainer=c("AccentLight"),onPrimaryContainer=c("OnSelected"),secondary=c("Accent"),onSecondary=c("OnAccent"),secondaryContainer=c("Soft"),onSecondaryContainer=c("Ink"),tertiary=c("Accent"),onTertiary=c("OnAccent"),background=c("Bg"),onBackground=c("Ink"),surface=c("Panel"),onSurface=c("Ink"),surfaceVariant=c("Soft"),onSurfaceVariant=c("Sub"),surfaceTint=Color.Transparent,outline=c("Outline"),outlineVariant=c("Line"),error=c("Danger"),onError=c("OnDanger"),errorContainer=c("DangerSoft"),onErrorContainer=c("Danger"),inverseSurface=c("Ink"),inverseOnSurface=c("Panel"),inversePrimary=c("OnAccent"),surfaceDim=c("Side"),surfaceBright=c("Raised"),surfaceContainerLowest=c("Bg"),surfaceContainerLow=c("Side"),surfaceContainer=c("Soft"),surfaceContainerHigh=c("Raised"),surfaceContainerHighest=c("Hover"))
    val family=remember(preferences.fontFamily){InstalledFonts.typeface(preferences.fontFamily)?.let{FontFamily(it)}}
    val typography=Typography(titleLarge=androidx.compose.ui.text.TextStyle(fontSize=24.sp,fontWeight=FontWeight.SemiBold),titleMedium=androidx.compose.ui.text.TextStyle(fontSize=17.sp,fontWeight=FontWeight.Medium),bodyLarge=androidx.compose.ui.text.TextStyle(fontSize=15.sp,lineHeight=24.sp),bodyMedium=androidx.compose.ui.text.TextStyle(fontSize=13.sp,lineHeight=21.sp),labelLarge=androidx.compose.ui.text.TextStyle(fontSize=13.sp,fontWeight=FontWeight.Medium)).withFamily(family)
    CompositionLocalProvider(LocalMojiPalette provides palette,LocalTextSelectionColors provides TextSelectionColors(c("Accent"),c("Selection"))){MaterialTheme(colorScheme=scheme,typography=typography,content=content)}
}
fun color(value:String):Color=runCatching{Color(android.graphics.Color.parseColor(value))}.getOrDefault(Color(0xFF75876D))
@Composable fun modeColor(mode:NoteMode)=when(mode){NoteMode.Text->MaterialTheme.colorScheme.primary;NoteMode.Prompt->MaterialTheme.colorScheme.secondary;NoteMode.Password->MaterialTheme.colorScheme.tertiary}

private fun Typography.withFamily(family:FontFamily?):Typography=copy(displayLarge=displayLarge.copy(fontFamily=family),displayMedium=displayMedium.copy(fontFamily=family),displaySmall=displaySmall.copy(fontFamily=family),headlineLarge=headlineLarge.copy(fontFamily=family),headlineMedium=headlineMedium.copy(fontFamily=family),headlineSmall=headlineSmall.copy(fontFamily=family),titleLarge=titleLarge.copy(fontFamily=family),titleMedium=titleMedium.copy(fontFamily=family),titleSmall=titleSmall.copy(fontFamily=family),bodyLarge=bodyLarge.copy(fontFamily=family),bodyMedium=bodyMedium.copy(fontFamily=family),bodySmall=bodySmall.copy(fontFamily=family),labelLarge=labelLarge.copy(fontFamily=family),labelMedium=labelMedium.copy(fontFamily=family),labelSmall=labelSmall.copy(fontFamily=family))
