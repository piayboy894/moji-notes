# Tiptap Simple Editor

The UI, hooks and editor styles in src/components, src/hooks, src/lib and src/styles originate from the MIT-licensed official Simple Editor template:
https://github.com/ueberdosis/tiptap-ui-components
Upstream snapshot: 799929bea4804c73767562b69f8acc2acdb8ac86 (2026-11-15).
https://tiptap.dev/docs/ui-components/templates/simple-editor

Tiptap packages are pinned to 3.34.0. The app uses the same compiled local resources on Windows and Android. Changes to the upstream template remove sample content, remote fonts and duplicate theme controls, open the system image picker directly from the image button and store images locally, omit static shortcut hints because native shortcuts are user-configurable, display the actual checkbox after Tiptap 3.34 made its sibling span screen-reader-only, add the single prompt-variable node, and connect local autosave. The ProseMirror document, selection, Enter, Backspace, lists, quotation, paste and undo rules are managed by Tiptap, not native block widgets. No CDN, external editor page, account or online editor service is used.

The old native block editor is replaced. Old content is retained as text, with registered variable tokens recognized; no compatibility layer for its previous custom formatting is maintained.
