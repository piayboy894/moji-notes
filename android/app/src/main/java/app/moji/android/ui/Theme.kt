package app.moji.android.ui

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp
import app.moji.android.data.NoteMode

private val Light = lightColorScheme(primary = Color(0xFF526B4D), onPrimary = Color.White, primaryContainer = Color(0xFFE7EDDF), onPrimaryContainer = Color(0xFF35482F), secondary = Color(0xFFB2946E), secondaryContainer = Color(0xFFF4EDDF), tertiary = Color(0xFF8393AC), background = Color(0xFFF8F7F3), onBackground = Color(0xFF303C34), surface = Color(0xFFFFFEFA), onSurface = Color(0xFF303C34), surfaceVariant = Color(0xFFF0F3EB), onSurfaceVariant = Color(0xFF7E8875), outline = Color(0xFFADB5A3), outlineVariant = Color(0xFFE4E8DC))
private val Dark = darkColorScheme(primary = Color(0xFFB3C7A4), onPrimary = Color(0xFF22351C), primaryContainer = Color(0xFF3B4C31), onPrimaryContainer = Color(0xFFDCE9D1), secondary = Color(0xFFD4BC98), secondaryContainer = Color(0xFF4D412D), tertiary = Color(0xFFB5C4DE), background = Color(0xFF1C231C), onBackground = Color(0xFFE1E7DA), surface = Color(0xFF242D21), onSurface = Color(0xFFE1E7DA), surfaceVariant = Color(0xFF303B29), onSurfaceVariant = Color(0xFFA2AF96), outline = Color(0xFF78896B), outlineVariant = Color(0xFF3A4632))
@Composable fun MojiTheme(theme: String = "system", content: @Composable () -> Unit) {
    val dark = theme == "dark" || (theme == "system" && isSystemInDarkTheme())
    MaterialTheme(colorScheme = if (dark) Dark else Light, typography = Typography(titleLarge = androidx.compose.ui.text.TextStyle(fontSize = 24.sp, fontWeight = FontWeight.SemiBold), titleMedium = androidx.compose.ui.text.TextStyle(fontSize = 17.sp, fontWeight = FontWeight.Medium), bodyLarge = androidx.compose.ui.text.TextStyle(fontSize = 15.sp, lineHeight = 24.sp), bodyMedium = androidx.compose.ui.text.TextStyle(fontSize = 13.sp, lineHeight = 21.sp), labelLarge = androidx.compose.ui.text.TextStyle(fontSize = 13.sp, fontWeight = FontWeight.Medium)), content = content)
}
fun color(value: String): Color = runCatching { Color(android.graphics.Color.parseColor(value)) }.getOrDefault(Color(0xFF75876D))
@Composable fun modeColor(mode: NoteMode) = when (mode) { NoteMode.Text -> MaterialTheme.colorScheme.primary; NoteMode.Prompt -> MaterialTheme.colorScheme.secondary; NoteMode.Password -> MaterialTheme.colorScheme.tertiary }
