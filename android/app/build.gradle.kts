import java.security.KeyStore

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
    id("org.jetbrains.kotlin.plugin.serialization")
}
android {
    namespace = "app.moji.android"
    compileSdk = 36
    defaultConfig {
        applicationId = "app.moji.android"
        minSdk = 26
        targetSdk = 36
        versionCode = 20101
        versionName = "2.1.1"
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        vectorDrawables.useSupportLibrary = true
    }
    signingConfigs {
        if (System.getenv("MOJI_KEYSTORE_PATH") != null) create("publisher") {
            storeFile = file(System.getenv("MOJI_KEYSTORE_PATH"))
            storePassword = System.getenv("MOJI_KEYSTORE_PASSWORD")
            val password = System.getenv("MOJI_KEYSTORE_PASSWORD").toCharArray()
            val archive = KeyStore.getInstance("PKCS12").apply { file(System.getenv("MOJI_KEYSTORE_PATH")).inputStream().use { load(it, password) } }
            keyAlias = archive.aliases().toList().first { archive.isKeyEntry(it) }
            password.fill('\u0000')
            keyPassword = System.getenv("MOJI_KEYSTORE_PASSWORD")
        }
    }
    buildTypes {
        debug { applicationIdSuffix = ".debug"; versionNameSuffix = "-debug" }
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            signingConfig = signingConfigs.findByName("publisher")
        }
    }
    sourceSets["main"].assets.srcDir("../../assets")
    buildFeatures { compose = true; buildConfig = true }
    compileOptions { sourceCompatibility = JavaVersion.VERSION_17; targetCompatibility = JavaVersion.VERSION_17 }
    kotlinOptions { jvmTarget = "17" }
    packaging { resources.excludes += "/META-INF/{AL2.0,LGPL2.1}" }
    testOptions { unitTests.isReturnDefaultValues = true; animationsDisabled = true }
}
dependencies {
    val composeBom = platform("androidx.compose:compose-bom:2025.05.01")
    implementation(composeBom); androidTestImplementation(composeBom)
    implementation("androidx.core:core-ktx:1.16.0")
    implementation("androidx.exifinterface:exifinterface:1.4.1")
    implementation("androidx.activity:activity-compose:1.10.1")
    implementation("androidx.fragment:fragment-ktx:1.8.7")
    implementation("androidx.lifecycle:lifecycle-viewmodel-compose:2.9.1")
    implementation("androidx.lifecycle:lifecycle-runtime-compose:2.9.1")
    implementation("androidx.window:window:1.4.0")
    implementation("androidx.compose.ui:ui")
    implementation("androidx.compose.ui:ui-tooling-preview")
    implementation("androidx.compose.foundation:foundation")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.webkit:webkit:1.14.0")
    implementation("org.jsoup:jsoup:1.18.3")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-android:1.10.2")
    implementation("org.jetbrains.kotlinx:kotlinx-serialization-json:1.8.1")
    implementation("com.squareup.okhttp3:okhttp:4.12.0")
    testImplementation("junit:junit:4.13.2")
    testImplementation("com.squareup.okhttp3:mockwebserver:4.12.0")
    testImplementation("org.jetbrains.kotlinx:kotlinx-coroutines-test:1.10.2")
    androidTestImplementation("androidx.test.ext:junit:1.2.1")
    androidTestImplementation("androidx.test:runner:1.6.2")
    androidTestImplementation("androidx.compose.ui:ui-test-junit4")
    debugImplementation("androidx.compose.ui:ui-tooling")
    debugImplementation("androidx.compose.ui:ui-test-manifest")
}
