package app.moji.android.ui

import androidx.compose.foundation.clickable
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.*
import androidx.compose.ui.graphics.drawscope.clipPath
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import app.moji.android.data.Notebook
import app.moji.android.data.NoteMode

@OptIn(androidx.compose.foundation.ExperimentalFoundationApi::class)
@Composable fun NotebookCard(book: Notebook, count: Int, open: () -> Unit, edit: () -> Unit, modifier:Modifier=Modifier) {
    val dark = MaterialTheme.colorScheme.background.luminance() < .3f
    val chosen=color(book.color)
    val palette=if(book.colorEnd.isBlank())listOf(lerp(chosen,Color.White,.78f),lerp(chosen,Color.White,.58f),lerp(chosen,Color.Black,.15f),chosen)else{
        val end=color(book.colorEnd);val band=lerp(lerp(chosen,end,.5f),Color.Black,.18f)
        listOf(notebookPaper(chosen),notebookPaper(end),lerp(band,Color.Black,.15f),band)
    }
    BoxWithConstraints(modifier.widthIn(max=210.dp).fillMaxWidth().aspectRatio(.87f).clickable(onClick=open).testTag("book-${book.mode.name}")) {
        val small = maxWidth < 220.dp
        Canvas(Modifier.fillMaxSize()) {
            val w = size.width; val h = size.height; val corner = w * .065f; val pageY = h - w * .09f
            for (layer in 7 downTo 1) drawRoundRect(Color.Black.copy(alpha = .012f), Offset(w * .025f - layer, pageY - w * .025f + layer * 1.4f), Size(w * .95f + layer * 2, w * .07f + layer), CornerRadius(corner))
            drawRoundRect(palette[2], Offset(w * .015f, pageY - w * .025f), Size(w * .97f, w * .07f), CornerRadius(corner))
            drawRoundRect(Brush.verticalGradient(listOf(Color(0xFFF6F2E6), Color(0xFFD9D6C7))), Offset(w * .03f, pageY - w * .018f), Size(w * .94f, w * .047f), CornerRadius(w * .035f))
            repeat(4) { i -> drawLine(Color(0xFF777765).copy(alpha = .19f), Offset(w * .05f, pageY + i * w * .005f), Offset(w * .95f, pageY + i * w * .005f), strokeWidth = .6f) }
            val face = Path().apply { addRoundRect(androidx.compose.ui.geometry.RoundRect(androidx.compose.ui.geometry.Rect(w * .01f, 0f, w * .99f, h - w * .066f), CornerRadius(corner))) }
            drawPath(face, Brush.linearGradient(listOf(palette[0], palette[1]), Offset.Zero, Offset(w, h)))
            clipPath(face) {
                var x = -h; val step = (density * 3.5f).coerceAtLeast(3f)
                while (x < w + h) { drawLine(Color.White.copy(alpha = .13f), Offset(x, 0f), Offset(x + h, h), .7f); drawLine(Color(0xFF566347).copy(alpha = .035f), Offset(x + 2, 0f), Offset(x - h + 2, h), .65f); x += step }
                drawRect(Brush.horizontalGradient(listOf(palette[2].copy(alpha = .23f), Color.White.copy(alpha = .32f), Color.Transparent)), Offset(w * .016f, 0f), Size(w * .055f, h))
                if (dark) drawRect(Color(0xFF1B2D1C).copy(alpha = .14f))
            }
            drawPath(face, Color.White.copy(alpha = .45f), style = Stroke(1f))
            val ribbon = Path().apply { moveTo(w * .13f, pageY); lineTo(w * .2f, pageY); lineTo(w * .2f, h); lineTo(w * .165f, h - w * .023f); lineTo(w * .13f, h); close() }; drawPath(ribbon, palette[3])
            val bandX = w * .825f; val bandW = w * .043f
            drawRect(Brush.horizontalGradient(listOf(palette[2], palette[3], palette[3], palette[2]), bandX, bandX + bandW), Offset(bandX, 0f), Size(bandW, h - w * .028f))
            repeat(5) { i -> drawLine(Color.White.copy(alpha = .15f), Offset(bandX + bandW * (i + 1) / 6, 0f), Offset(bandX + bandW * (i + 1) / 6, h - w * .028f), .6f) }
        }
        val ink = Color(0xFF293226)
        Column(Modifier.fillMaxSize().padding(start = maxWidth * .14f, end = maxWidth * .22f, top = maxWidth * .16f, bottom = maxWidth * .15f)) {
            Icon(notebookIcon(book), null, Modifier.size(if (small) 25.dp else 34.dp), tint = lerp(chosen,Color.Black,.32f))
            Spacer(Modifier.weight(1f))
            Text(book.name, fontSize = if (small) 18.sp else 26.sp, lineHeight = if (small) 24.sp else 35.sp, fontWeight = FontWeight.SemiBold, color = ink, maxLines = 2, overflow = TextOverflow.Ellipsis)
            Spacer(Modifier.height(if (small) 5.dp else 10.dp))
            Text("${count.toString().padStart(2, '0')} 条记录", fontSize = if (small) 11.sp else 14.sp, color = Color(0xFF53604C))
            Spacer(Modifier.height(if (small) 10.dp else 24.dp))
            Icon(MojiIcons.ArrowForward, null, Modifier.size(if (small) 18.dp else 24.dp), tint = Color(0xFF57654F))
        }
        IconButton(onClick=edit,modifier=Modifier.align(Alignment.TopEnd).padding(end=maxWidth*.2f,top=(maxWidth*.16f-5.dp).coerceAtLeast(0.dp)).size(36.dp)){Icon(MojiIcons.MoreHoriz,"编辑记事本",Modifier.size(17.dp),tint=ink.copy(alpha=.65f))}
    }
}

@Composable fun AddNotebookCard(create:()->Unit){
    val stroke=MaterialTheme.colorScheme.outline;val accent=MaterialTheme.colorScheme.primary
    Box(Modifier.widthIn(max=210.dp).fillMaxWidth().aspectRatio(.87f).testTag("add-notebook-card").then(Modifier.clickable(onClick=create)).semantics{contentDescription="新建记事本"},contentAlignment=Alignment.Center){
        Canvas(Modifier.fillMaxSize()){val padding=3.dp.toPx();drawRoundRect(stroke,Offset(padding,padding),Size(size.width-padding*2,size.height-size.width*.066f-padding),CornerRadius(14.dp.toPx()),style=Stroke(width=1.4.dp.toPx(),pathEffect=PathEffect.dashPathEffect(floatArrayOf(7.dp.toPx(),6.dp.toPx()))))}
        Icon(MojiIcons.Add,null,Modifier.padding(bottom=10.dp).size(32.dp),tint=accent)
    }
}

fun notebookPaper(value:Color):Color {var result=lerp(value,Color.White,.12f);while(result.luminance()<.45f)result=lerp(result,Color.White,.08f);return result}
fun notebookSwatch(start:String,end:String):Brush=if(end.isBlank())SolidColor(color(start))else Brush.linearGradient(listOf(notebookPaper(color(start)),notebookPaper(color(end))))
