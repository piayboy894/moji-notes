-keepattributes Signature,InnerClasses,EnclosingMethod
-keep class app.moji.android.data.**$$serializer { *; }
-keepclassmembers class app.moji.android.data.** { *** Companion; }
-keep class org.commonmark.** { *; }
-dontwarn javax.annotation.**

# Ksoup's builder delegates callbacks to an anonymous handler. R8 class merging
# incorrectly routed unimplemented callbacks through a null delegate in 1.1.0.
# Keep this small parser boundary intact; the rest of the app stays optimized.
-keep class com.mohamedrejeb.ksoup.** { *; }
