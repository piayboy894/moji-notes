package app.moji.android.data

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Matrix
import android.net.Uri
import androidx.exifinterface.media.ExifInterface
import app.moji.android.security.Crypto
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.ByteArrayOutputStream

/** Read a selected image once, normalize phone rotation and bound decoded/output memory. */
object EditorImages {
    suspend fun load(context: Context, uri: Uri): String = withContext(Dispatchers.IO) {
        val bytes=context.contentResolver.openInputStream(uri)?.use { input ->
            val output=ByteArrayOutputStream();val buffer=ByteArray(8192)
            while(true){val count=input.read(buffer);if(count<0)break;require(output.size()+count<=30_000_000){"图片过大，请选择 30 MB 以内的图片"};output.write(buffer,0,count)}
            output.toByteArray()
        } ?: error("无法读取图片")
        val bounds=BitmapFactory.Options().apply{inJustDecodeBounds=true}
        BitmapFactory.decodeByteArray(bytes,0,bytes.size,bounds)
        require(bounds.outWidth>0&&bounds.outHeight>0){"无法识别这张图片，请选择 JPG、PNG 或 WebP 图片"}
        var sample=1;while(maxOf(bounds.outWidth,bounds.outHeight)/sample>2400)sample*=2
        var bitmap=BitmapFactory.decodeByteArray(bytes,0,bytes.size,BitmapFactory.Options().apply{inSampleSize=sample}) ?: error("无法读取图片")
        try {
            val exif=runCatching{ExifInterface(bytes.inputStream())}.getOrNull()
            val matrix=Matrix().apply{exif?.let{postRotate(it.rotationDegrees.toFloat());if(it.isFlipped)postScale(-1f,1f)}}
            if(!matrix.isIdentity){val rotated=Bitmap.createBitmap(bitmap,0,0,bitmap.width,bitmap.height,matrix,true);if(rotated!==bitmap){bitmap.recycle();bitmap=rotated}}
            while(true){
                val output=ByteArrayOutputStream();val alpha=bitmap.hasAlpha()
                bitmap.compress(if(alpha)Bitmap.CompressFormat.PNG else Bitmap.CompressFormat.JPEG,85,output)
                if(output.size()<=1_800_000)return@withContext "![图片](data:image/${if(alpha)"png" else "jpeg"};base64,${Crypto.b64(output.toByteArray())})"
                val smaller=Bitmap.createScaledBitmap(bitmap,(bitmap.width*.75f).toInt().coerceAtLeast(1),(bitmap.height*.75f).toInt().coerceAtLeast(1),true)
                bitmap.recycle();bitmap=smaller
            }
            @Suppress("UNREACHABLE_CODE") error("无法读取图片")
        } finally {bitmap.recycle()}
    }
}
