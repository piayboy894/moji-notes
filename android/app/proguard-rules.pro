-keepattributes Signature,InnerClasses,EnclosingMethod
-keep class app.moji.android.data.**$$serializer { *; }
-keepclassmembers class app.moji.android.data.** { *** Companion; }
-dontwarn javax.annotation.**

# Public WebView bridge is called by the bundled JavaScript, including in signed release builds.
-keepclassmembers class * { @android.webkit.JavascriptInterface <methods>; }
