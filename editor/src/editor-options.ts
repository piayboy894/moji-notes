import type { EditorOptions } from "@tiptap/core"
import { StarterKit } from "@tiptap/starter-kit"
import { MojiImage } from "@/image-background"
import { TaskItem, TaskList } from "@tiptap/extension-list"
import { TextAlign } from "@tiptap/extension-text-align"
import { Typography } from "@tiptap/extension-typography"
import { Highlight } from "@tiptap/extension-highlight"
import { Subscript } from "@tiptap/extension-subscript"
import { Superscript } from "@tiptap/extension-superscript"
import { Selection } from "@tiptap/extensions"

import { ImageUploadNode } from "@/components/tiptap-node/image-upload-node/image-upload-node-extension"
import { HorizontalRule } from "@/components/tiptap-node/horizontal-rule-node/horizontal-rule-node-extension"
import { MojiVariable } from "./prompt-field"
import { reportImageError } from "./native"
import { LegacyAndroidInput } from "./legacy-android-input"
import { EditorIndent } from "./editor-indent"
import { listClipboardText } from "./list-clipboard"
import { handleImageUpload, MAX_FILE_SIZE } from "@/lib/tiptap-utils"

export function editorOptions(): Partial<EditorOptions> {
  return {
    editable: false,
    editorProps: {
      clipboardTextSerializer: listClipboardText,
      attributes: {
        autocomplete: "off",
        autocorrect: "off",
        autocapitalize: "off",
        "aria-label": "笔记正文",
        role:"textbox",
        "aria-multiline":"true",
        class: "simple-editor",
      },
    },
    extensions: [
      EditorIndent,
      StarterKit.configure({
        codeBlock: { enableTabIndentation: true, tabSize: 4 },
        horizontalRule: false,
        link: {
          openOnClick: false,
          enableClickSelection: true,
        },
      }),
      HorizontalRule,
      TextAlign.configure({ types: ["heading", "paragraph"] }),
      TaskList,
      TaskItem.configure({ nested: true, a11y:{checkboxLabel:node=>"待办事项："+(node.textContent||"未填写")} }),
      Highlight.configure({ multicolor: true }),
      MojiImage.configure({allowBase64:true}),
      MojiVariable,
      LegacyAndroidInput,
      Typography,
      Superscript,
      Subscript,
      Selection,
      ImageUploadNode.configure({
        accept: "image/*",
        maxSize: MAX_FILE_SIZE,
        limit: 3,
        upload: handleImageUpload,
        onError: (error) => { if(error.message!=="已取消")reportImageError() },
      }),
    ],
    content: "",
  }
}
