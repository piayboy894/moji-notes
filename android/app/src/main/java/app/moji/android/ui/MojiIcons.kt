// Lucide 0.468.0; see assets/icons/LICENSE. Shared with the Windows icon set.
package app.moji.android.ui
import androidx.compose.ui.graphics.*
import androidx.compose.ui.graphics.vector.*
import androidx.compose.ui.unit.dp
object MojiIcons {
    private fun icon(name:String, data:String) = ImageVector.Builder(name,24.dp,24.dp,24f,24f).addPath(
        pathData=PathParser().parsePathString(data).toNodes(), fill=null, stroke=SolidColor(Color.Black),
        strokeLineWidth=1.8f, strokeLineCap=StrokeCap.Round, strokeLineJoin=StrokeJoin.Round).build()
    val Add by lazy { icon("Add","M0 0 M5 12h14 M0 0 M12 5v14") }
    val Archive by lazy { icon("Archive","M0 0 M3 3H21Q22 3 22 4V7Q22 8 21 8H3Q2 8 2 7V4Q2 3 3 3Z M0 0 M4 8v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8 M0 0 M10 12h4") }
    val ArrowBack by lazy { icon("ArrowBack","M0 0 m12 19-7-7 7-7 M0 0 M19 12H5") }
    val ArrowForward by lazy { icon("ArrowForward","M0 0 M5 12h14 M0 0 m12 5 7 7-7 7") }
    val Article by lazy { icon("Article","M0 0 M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z M0 0 M14 2v4a2 2 0 0 0 2 2h4 M0 0 M10 9H8 M0 0 M16 13H8 M0 0 M16 17H8") }
    val AutoAwesome by lazy { icon("AutoAwesome","M0 0 M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z M0 0 M20 3v4 M0 0 M22 5h-4 M0 0 M4 17v2 M0 0 M5 18H3") }
    val Casino by lazy { icon("Casino","M0 0 M4 10H12Q14 10 14 12V20Q14 22 12 22H4Q2 22 2 20V12Q2 10 4 10Z M0 0 m17.92 14 3.5-3.5a2.24 2.24 0 0 0 0-3l-5-4.92a2.24 2.24 0 0 0-3 0L10 6 M0 0 M6 18h.01 M0 0 M10 14h.01 M0 0 M15 6h.01 M0 0 M18 9h.01") }
    val Check by lazy { icon("Check","M0 0 M20 6 9 17l-5-5") }
    val Checklist by lazy { icon("Checklist","M0 0 m3 17 2 2 4-4 M0 0 m3 7 2 2 4-4 M0 0 M13 6h8 M0 0 M13 12h8 M0 0 M13 18h8") }
    val ChevronRight by lazy { icon("ChevronRight","M0 0 m9 18 6-6-6-6") }
    val Close by lazy { icon("Close","M0 0 M18 6 6 18 M0 0 m6 6 12 12") }
    val CloudDone by lazy { icon("CloudDone","M0 0 M17.5 19H9a7 7 0 1 1 6.71-9h1.79a4.5 4.5 0 1 1 0 9Z") }
    val Code by lazy { icon("Code","M0 0 M16 18 22 12 16 6 M0 0 M8 6 2 12 8 18") }
    val ContentCopy by lazy { icon("ContentCopy","M0 0 M10 8H20Q22 8 22 10V20Q22 22 20 22H10Q8 22 8 20V10Q8 8 10 8Z M0 0 M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2") }
    val CopyAll by lazy { icon("CopyAll","M0 0 M10 8H20Q22 8 22 10V20Q22 22 20 22H10Q8 22 8 20V10Q8 8 10 8Z M0 0 M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2") }
    val Dashboard by lazy { icon("Dashboard","M0 0 M4 3H9Q10 3 10 4V11Q10 12 9 12H4Q3 12 3 11V4Q3 3 4 3Z M0 0 M15 3H20Q21 3 21 4V7Q21 8 20 8H15Q14 8 14 7V4Q14 3 15 3Z M0 0 M15 12H20Q21 12 21 13V20Q21 21 20 21H15Q14 21 14 20V13Q14 12 15 12Z M0 0 M4 16H9Q10 16 10 17V20Q10 21 9 21H4Q3 21 3 20V17Q3 16 4 16Z") }
    val DeleteOutline by lazy { icon("DeleteOutline","M0 0 M3 6h18 M0 0 M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6 M0 0 M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2 M0 0 M10 11L10 17 M0 0 M14 11L14 17") }
    val DeleteSweep by lazy { icon("DeleteSweep","M0 0 M3 6h18 M0 0 M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6 M0 0 M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2 M0 0 M10 11L10 17 M0 0 M14 11L14 17") }
    val Description by lazy { icon("Description","M0 0 M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z M0 0 M14 2v4a2 2 0 0 0 2 2h4 M0 0 M10 9H8 M0 0 M16 13H8 M0 0 M16 17H8") }
    val DriveFileMove by lazy { icon("DriveFileMove","M0 0 M2 9V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H20a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-1 M0 0 M2 13h10 M0 0 m9 16 3-3-3-3") }
    val ErrorOutline by lazy { icon("ErrorOutline","M0 0 M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0 M0 0 M12 8L12 12 M0 0 M12 16L12.01 16") }
    val ExpandMore by lazy { icon("ExpandMore","M0 0 m6 9 6 6 6-6") }
    val FileDownload by lazy { icon("FileDownload","M0 0 M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M0 0 M7 10 12 15 17 10 M0 0 M12 15L12 3") }
    val FileUpload by lazy { icon("FileUpload","M0 0 M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M0 0 M17 8 12 3 7 8 M0 0 M12 3L12 15") }
    val FindReplace by lazy { icon("FindReplace","M0 0 M14 4a2 2 0 0 1 2-2 M0 0 M16 10a2 2 0 0 1-2-2 M0 0 M20 2a2 2 0 0 1 2 2 M0 0 M22 8a2 2 0 0 1-2 2 M0 0 m3 7 3 3 3-3 M0 0 M6 10V5a3 3 0 0 1 3-3h1 M0 0 M4 14H8Q10 14 10 16V20Q10 22 8 22H4Q2 22 2 20V16Q2 14 4 14Z") }
    val FolderZip by lazy { icon("FolderZip","M0 0 M13 19a2 2 0 1 0 4 0a2 2 0 1 0 -4 0 M0 0 M20.9 19.8A2 2 0 0 0 22 18V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2h5.1 M0 0 M15 11v-1 M0 0 M15 17v-2") }
    val FormatBold by lazy { icon("FormatBold","M0 0 M6 12h9a4 4 0 0 1 0 8H7a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1h7a4 4 0 0 1 0 8") }
    val FormatItalic by lazy { icon("FormatItalic","M0 0 M19 4L10 4 M0 0 M14 20L5 20 M0 0 M15 4L9 20") }
    val FormatListBulleted by lazy { icon("FormatListBulleted","M0 0 M3 12h.01 M0 0 M3 18h.01 M0 0 M3 6h.01 M0 0 M8 12h13 M0 0 M8 18h13 M0 0 M8 6h13") }
    val FormatListNumbered by lazy { icon("FormatListNumbered","M0 0 M10 12h11 M0 0 M10 18h11 M0 0 M10 6h11 M0 0 M4 10h2 M0 0 M4 6h1v4 M0 0 M6 18H4c0-1 2-2 2-3s-1-1.5-2-1") }
    val FormatQuote by lazy { icon("FormatQuote","M0 0 M16 3a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2 1 1 0 0 1 1 1v1a2 2 0 0 1-2 2 1 1 0 0 0-1 1v2a1 1 0 0 0 1 1 6 6 0 0 0 6-6V5a2 2 0 0 0-2-2z M0 0 M5 3a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2 1 1 0 0 1 1 1v1a2 2 0 0 1-2 2 1 1 0 0 0-1 1v2a1 1 0 0 0 1 1 6 6 0 0 0 6-6V5a2 2 0 0 0-2-2z") }
    val FormatStrikethrough by lazy { icon("FormatStrikethrough","M0 0 M16 4H9a3 3 0 0 0-2.83 4 M0 0 M14 12a4 4 0 0 1 0 8H6 M0 0 M4 12L20 12") }
    val Fullscreen by lazy { icon("Fullscreen","M0 0 M8 3H5a2 2 0 0 0-2 2v3 M0 0 M21 8V5a2 2 0 0 0-2-2h-3 M0 0 M3 16v3a2 2 0 0 0 2 2h3 M0 0 M16 21h3a2 2 0 0 0 2-2v-3") }
    val FullscreenExit by lazy { icon("FullscreenExit","M0 0 M8 3v3a2 2 0 0 1-2 2H3 M0 0 M21 8h-3a2 2 0 0 1-2-2V3 M0 0 M3 16h3a2 2 0 0 1 2 2v3 M0 0 M16 21v-3a2 2 0 0 1 2-2h3") }
    val History by lazy { icon("History","M0 0 M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8 M0 0 M3 3v5h5 M0 0 M12 7v5l4 2") }
    val Image by lazy { icon("Image","M0 0 M5 3H19Q21 3 21 5V19Q21 21 19 21H5Q3 21 3 19V5Q3 3 5 3Z M0 0 M7 9a2 2 0 1 0 4 0a2 2 0 1 0 -4 0 M0 0 m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21") }
    val Key by lazy { icon("Key","M0 0 M2.586 17.414A2 2 0 0 0 2 18.828V21a1 1 0 0 0 1 1h3a1 1 0 0 0 1-1v-1a1 1 0 0 1 1-1h1a1 1 0 0 0 1-1v-1a1 1 0 0 1 1-1h.172a2 2 0 0 0 1.414-.586l.814-.814a6.5 6.5 0 1 0-4-4z M0 0 M16 7.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0") }
    val Link by lazy { icon("Link","M0 0 M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71 M0 0 M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71") }
    val Menu by lazy { icon("Menu","M0 0 M4 12L20 12 M0 0 M4 6L20 6 M0 0 M4 18L20 18") }
    val MenuBook by lazy { icon("MenuBook","M0 0 M12 7v14 M0 0 M3 18a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1h5a4 4 0 0 1 4 4 4 4 0 0 1 4-4h5a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1h-6a3 3 0 0 0-3 3 3 3 0 0 0-3-3z") }
    val MoreHoriz by lazy { icon("MoreHoriz","M0 0 M11 12a1 1 0 1 0 2 0a1 1 0 1 0 -2 0 M0 0 M18 12a1 1 0 1 0 2 0a1 1 0 1 0 -2 0 M0 0 M4 12a1 1 0 1 0 2 0a1 1 0 1 0 -2 0") }
    val NorthEast by lazy { icon("NorthEast","M0 0 M7 7h10v10 M0 0 M7 17 17 7") }
    val OpenInNew by lazy { icon("OpenInNew","M0 0 M15 3h6v6 M0 0 M10 14 21 3 M0 0 M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6") }
    val PushPin by lazy { icon("PushPin","M0 0 M12 17v5 M0 0 M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 5 15.24V16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-.76a2 2 0 0 0-1.11-1.79l-1.78-.9A2 2 0 0 1 15 10.76V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z") }
    val Redo by lazy { icon("Redo","M0 0 m15 14 5-5-5-5 M0 0 M20 9H9.5A5.5 5.5 0 0 0 4 14.5A5.5 5.5 0 0 0 9.5 20H13") }
    val Refresh by lazy { icon("Refresh","M0 0 M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8 M0 0 M21 3v5h-5 M0 0 M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16 M0 0 M8 16H3v5") }
    val Restore by lazy { icon("Restore","M0 0 M3 3H21Q22 3 22 4V7Q22 8 21 8H3Q2 8 2 7V4Q2 3 3 3Z M0 0 M4 8v11a2 2 0 0 0 2 2h2 M0 0 M20 8v11a2 2 0 0 1-2 2h-2 M0 0 m9 15 3-3 3 3 M0 0 M12 12v9") }
    val RestoreFromTrash by lazy { icon("RestoreFromTrash","M0 0 M3 3H21Q22 3 22 4V7Q22 8 21 8H3Q2 8 2 7V4Q2 3 3 3Z M0 0 M4 8v11a2 2 0 0 0 2 2h2 M0 0 M20 8v11a2 2 0 0 1-2 2h-2 M0 0 m9 15 3-3 3 3 M0 0 M12 12v9") }
    val Save by lazy { icon("Save","M0 0 M15.2 3a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M0 0 M17 21v-7a1 1 0 0 0-1-1H8a1 1 0 0 0-1 1v7 M0 0 M7 3v4a1 1 0 0 0 1 1h7") }
    val Search by lazy { icon("Search","M0 0 M3 11a8 8 0 1 0 16 0a8 8 0 1 0 -16 0 M0 0 m21 21-4.3-4.3") }
    val Segment by lazy { icon("Segment","M0 0 M21 12h-8 M0 0 M21 6H8 M0 0 M21 18h-8 M0 0 M3 6v4c0 1.1.9 2 2 2h3 M0 0 M3 10v6c0 1.1.9 2 2 2h3") }
    val Settings by lazy { icon("Settings","M0 0 M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z M0 0 M9 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0") }
    val Share by lazy { icon("Share","M0 0 M15 5a3 3 0 1 0 6 0a3 3 0 1 0 -6 0 M0 0 M3 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0 M0 0 M15 19a3 3 0 1 0 6 0a3 3 0 1 0 -6 0 M0 0 M8.59 13.51L15.42 17.49 M0 0 M15.41 6.51L8.59 10.49") }
    val Shield by lazy { icon("Shield","M0 0 M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z") }
    val Star by lazy { icon("Star","M0 0 M11.525 2.295a.53.53 0 0 1 .95 0l2.31 4.679a2.123 2.123 0 0 0 1.595 1.16l5.166.756a.53.53 0 0 1 .294.904l-3.736 3.638a2.123 2.123 0 0 0-.611 1.878l.882 5.14a.53.53 0 0 1-.771.56l-4.618-2.428a2.122 2.122 0 0 0-1.973 0L6.396 21.01a.53.53 0 0 1-.77-.56l.881-5.139a2.122 2.122 0 0 0-.611-1.879L2.16 9.795a.53.53 0 0 1 .294-.906l5.165-.755a2.122 2.122 0 0 0 1.597-1.16z") }
    val StarOutline by lazy { icon("StarOutline","M0 0 M11.525 2.295a.53.53 0 0 1 .95 0l2.31 4.679a2.123 2.123 0 0 0 1.595 1.16l5.166.756a.53.53 0 0 1 .294.904l-3.736 3.638a2.123 2.123 0 0 0-.611 1.878l.882 5.14a.53.53 0 0 1-.771.56l-4.618-2.428a2.122 2.122 0 0 0-1.973 0L6.396 21.01a.53.53 0 0 1-.77-.56l.881-5.139a2.122 2.122 0 0 0-.611-1.879L2.16 9.795a.53.53 0 0 1 .294-.906l5.165-.755a2.122 2.122 0 0 0 1.597-1.16z") }
    val TableChart by lazy { icon("TableChart","M0 0 M12 3v18 M0 0 M5 3H19Q21 3 21 5V19Q21 21 19 21H5Q3 21 3 19V5Q3 3 5 3Z M0 0 M3 9h18 M0 0 M3 15h18") }
    val Title by lazy { icon("Title","M0 0 M4 12h8 M0 0 M4 18V6 M0 0 M12 18V6 M0 0 M21 18h-4c0-4 4-3 4-6 0-1.5-2-2.5-4-1") }
    val Undo by lazy { icon("Undo","M0 0 M9 14 4 9l5-5 M0 0 M4 9h10.5a5.5 5.5 0 0 1 5.5 5.5a5.5 5.5 0 0 1-5.5 5.5H11") }
    val Visibility by lazy { icon("Visibility","M0 0 M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0 M0 0 M9 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0") }
    val VisibilityOff by lazy { icon("VisibilityOff","M0 0 M10.733 5.076a10.744 10.744 0 0 1 11.205 6.575 1 1 0 0 1 0 .696 10.747 10.747 0 0 1-1.444 2.49 M0 0 M14.084 14.158a3 3 0 0 1-4.242-4.242 M0 0 M17.479 17.499a10.75 10.75 0 0 1-15.417-5.151 1 1 0 0 1 0-.696 10.75 10.75 0 0 1 4.446-5.143 M0 0 m2 2 20 20") }
    val Notebook by lazy { icon("Notebook","M0 0 M2 6h4 M0 0 M2 10h4 M0 0 M2 14h4 M0 0 M2 18h4 M0 0 M6 2H18Q20 2 20 4V20Q20 22 18 22H6Q4 22 4 20V4Q4 2 6 2Z M0 0 M16 2v20") }
}
