# Native rich text editor

Vendored Android sources from [Compose Rich Editor 1.0.0-rc13](https://github.com/MohamedRejeb/compose-rich-editor/tree/v1.0.0-rc13), Apache-2.0. No browser or JavaScript runtime.

Local changes: compile the Android target only; remove unused Material wrappers; expose synchronous native text-change callbacks so saves include the final keystroke; disable implicit typed-list recognition in `RichTextState` so literal typing/pasting is not treated as a command. Explicit list buttons remain. Preserve escaped literal punctuation in Markdown round trips and count inline image tokens as one removable character. Editable variable labels use native text transformation with atomic cursor mapping; BasicTextField does not support the read-only renderer's InlineTextContent. Original sources and license are included so app builds need no patched binary. Moji's block editor and interchange serializer live in the app module.
