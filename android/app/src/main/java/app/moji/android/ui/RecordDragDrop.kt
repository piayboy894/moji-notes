package app.moji.android.ui

import android.content.ClipData
import android.content.ClipDescription
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.border
import androidx.compose.foundation.draganddrop.dragAndDropSource
import androidx.compose.foundation.draganddrop.dragAndDropTarget
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.input.pointer.PointerEventType
import androidx.compose.ui.input.pointer.PointerEventPass
import androidx.compose.ui.input.pointer.isSecondaryPressed
import androidx.compose.ui.draganddrop.*
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.drawText
import androidx.compose.ui.text.rememberTextMeasurer
import androidx.compose.ui.unit.dp
import app.moji.android.MojiViewModel
import app.moji.android.data.Notebook

internal const val RecordDragMime = "application/vnd.moji.records-drag"
private fun DragAndDropEvent.recordToken():String? = toAndroidDragEvent().localState as? String

@Composable internal fun Modifier.recordDragRoot(vm:MojiViewModel):Modifier {
    val target=remember(vm){object:DragAndDropTarget{
        override fun onDrop(event:DragAndDropEvent)=false
        override fun onEnded(event:DragAndDropEvent){vm.endRecordDrag()}
    }}
    return dragAndDropTarget(shouldStartDragAndDrop={RecordDragMime in it.mimeTypes()},target=target)
}

@Composable internal fun Modifier.recordDropTarget(vm:MojiViewModel,book:Notebook):Modifier {
    var entered by remember(book.id){mutableStateOf(false)}
    var compatible by remember(book.id){mutableStateOf(true)}
    val target=remember(vm,book){object:DragAndDropTarget{
        override fun onEntered(event:DragAndDropEvent){if(vm.acceptsRecordDrag(event.recordToken())){entered=true;compatible=vm.recordDragCompatible(book)}}
        override fun onExited(event:DragAndDropEvent){entered=false}
        override fun onEnded(event:DragAndDropEvent){entered=false}
        override fun onDrop(event:DragAndDropEvent):Boolean{entered=false;return vm.dropRecords(event.recordToken(),book.id)}
    }}
    val outline=if(entered)Modifier.border(BorderStroke(2.dp,if(compatible)MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error),RoundedCornerShape(12.dp))else Modifier
    return then(outline).dragAndDropTarget(shouldStartDragAndDrop={RecordDragMime in it.mimeTypes() && vm.acceptsRecordDrag(it.recordToken())},target=target)
}

@Composable internal fun Modifier.recordDragSource(vm:MojiViewModel,ids:Set<String>,enabled:Boolean):Modifier {
    if(!enabled)return this
    val background=MaterialTheme.colorScheme.primary;val foreground=MaterialTheme.colorScheme.onPrimary
    val measurer=rememberTextMeasurer();val text=measurer.measure(AnnotatedString("${ids.size} 条记录"),MaterialTheme.typography.labelLarge.copy(color=foreground))
    return dragAndDropSource(drawDragDecoration={
        val width=(text.size.width+32.dp.toPx()).coerceAtMost(size.width);val height=48.dp.toPx().coerceAtMost(size.height)
        val x=(size.width-width)/2;val y=(size.height-height)/2
        drawRoundRect(background,Offset(x,y),Size(width,height),CornerRadius(10.dp.toPx()))
        drawText(text,topLeft=Offset((size.width-text.size.width)/2,(size.height-text.size.height)/2))
    },transferData={
        vm.beginRecordDrag(ids)?.let{token->
            val clip=ClipData(ClipDescription("墨记记录",arrayOf(RecordDragMime)),ClipData.Item(token))
            DragAndDropTransferData(clipData=clip,localState=token)
        }
    })
}

@Composable internal fun Modifier.recordContextTrigger(open:()->Unit):Modifier {
    val latest by rememberUpdatedState(open)
    return pointerInput(Unit){awaitPointerEventScope{while(true){val event=awaitPointerEvent(PointerEventPass.Initial);if(event.type==PointerEventType.Press&&event.buttons.isSecondaryPressed){event.changes.forEach{it.consume()};latest()}}}}
}
