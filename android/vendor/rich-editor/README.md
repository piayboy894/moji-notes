# Native rich text editor

Vendored Android sources from [Compose Rich Editor 1.0.0-rc13](https://github.com/MohamedRejeb/compose-rich-editor/tree/v1.0.0-rc13), Apache-2.0. No browser or JavaScript runtime.

Local changes: compile the Android target only; remove unused Material wrappers; disable implicit typed-list recognition in `RichTextState` so literal typing/pasting is not treated as a command. Explicit list buttons remain. Original sources and license are included so app builds need no patched binary. Moji's block editor and interchange serializer live in the app module.
