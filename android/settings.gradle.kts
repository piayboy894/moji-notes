pluginManagement { repositories { google(); mavenCentral(); gradlePluginPortal() } }
dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories { google(); mavenCentral() }
}
rootProject.name = "MojiAndroid"
include(":app")

include(":rich-editor")
project(":rich-editor").projectDir = file("vendor/rich-editor")
