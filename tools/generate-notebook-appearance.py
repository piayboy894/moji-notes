from pathlib import Path
import json
r=Path(__file__).resolve().parents[1]
d=json.loads((r/'assets/notebooks/appearance.json').read_text())
q=lambda s:json.dumps(s,ensure_ascii=False)
cs='// Generated from assets/notebooks/appearance.json.\nnamespace Moji.App;\ninternal static partial class NotebookAppearance\n{\n'
cs+='    internal static readonly (string Icon,string Label)[] Icons=['+','.join('('+q(x['id'])+','+q(x['name'])+')'for x in d['icons'])+'];\n'
for kind,name in [('solid','Solids'),('gradient','Gradients')]:cs+='    internal static readonly (string Name,string Start,string End)[] '+name+'=['+','.join('('+','.join(q(x[k])for k in ['name','start','end'])+')'for x in d[kind])+'];\n'
cs+='}\n';(r/'src/Moji.App/NotebookAppearance.Catalog.cs').write_text(cs)
kt='// Generated from assets/notebooks/appearance.json.\npackage app.moji.android.ui\nimport androidx.compose.ui.graphics.vector.ImageVector\nimport app.moji.android.data.Notebook\nimport app.moji.android.data.NoteMode\ndata class NotebookPalette(val name:String,val start:String,val end:String="")\n'
kt+='val notebookIcons=listOf('+','.join(q(x['id'])+' to '+q(x['name'])for x in d['icons'])+')\n'
for kind,name in [('solid','notebookSolids'),('gradient','notebookGradients')]:kt+='val '+name+'=listOf('+','.join('NotebookPalette('+','.join(q(x[k])for k in ['name','start','end'])+')'for x in d[kind])+')\n'
kt+='fun notebookIconId(book:Notebook):String=book.icon.takeIf{icon->notebookIcons.any{it.first==icon}}?:when(book.mode){NoteMode.Text->"Document";NoteMode.Prompt->"Prompt";NoteMode.Password->"Lock"}\nfun notebookIcon(book:Notebook):ImageVector=when(notebookIconId(book)){\n'
alias={'Book':'Notebook','Document':'Description','Prompt':'AutoAwesome','Lock':'Key','Cloud':'CloudDone','Pin':'PushPin'}
kt+=''.join(q(x['id'])+'->MojiIcons.'+alias.get(x['id'],x['id'])+'\n' for x in d['icons'])+'else->modeIcon(book.mode)\n}\n'
(r/'android/app/src/main/java/app/moji/android/ui/NotebookAppearance.kt').write_text(kt)
