"""Trace disposable debug fixture through the real WebView DevTools socket."""
import sys,json,time,urllib.request
sys.path.insert(0,'tools')
import native_ui as ui
from native_ui import *
import websocket
ui.PACKAGE=PACKAGE='app.moji.android.debug'
folder=Path('app/build/outputs/editor-probe');folder.mkdir(parents=True,exist_ok=True)
if adb('shell','getprop','ro.kernel.qemu').strip()!='1':raise RuntimeError('Emulator required')
adb('shell','pm','disable-user','--user','0','com.google.android.apps.nexuslauncher')
adb('install','-r','app/build/outputs/apk/debug/app-debug.apk')
for shown in ('0','1'):
    adb('shell','settings','put','secure','show_ime_with_hard_keyboard',shown)
    adb('shell','pm','clear',PACKAGE);launch();wait_node(resource='home-title')
    tap(wait_node(resource='add-notebook-card'));enter('notebook-name','Probe');tap(wait_node(text='创建记事本'));adb('shell','input','keyevent','KEYCODE_BACK');tap(wait_node(resource='book-Text'));tap(wait_node(description='新建记录'))
    wait_editor()
    pid=adb('shell','pidof',PACKAGE).strip()
    adb('forward','tcp:9222','localabstract:webview_devtools_remote_'+pid)
    pages=json.load(urllib.request.urlopen('http://127.0.0.1:9222/json'))
    ws=websocket.create_connection(pages[0]['webSocketDebuggerUrl'],suppress_origin=True,timeout=10)
    serial=0
    def evaluate(expression):
        global serial
        serial+=1;ws.send(json.dumps({'id':serial,'method':'Runtime.evaluate','params':{'expression':expression,'returnByValue':True}}))
        while True:
            r=json.loads(ws.recv())
            if r.get('id')==serial:return r
    evaluate("window.probe=[]; ['keydown','beforeinput','input','keyup','selectionchange'].forEach(type=>document.addEventListener(type,e=>probe.push({type,key:e.key,keyCode:e.keyCode,data:e.data,cancelable:e.cancelable,inputType:e.inputType,composing:e.isComposing,sel:window.getSelection()?.anchorOffset,head:MojiEditor.editor.state.selection.head,html:MojiEditor.editor.getHTML()}),true));")
    tap(wait_editor());type_editor_text('ABC123');time.sleep(.5);adb('shell','input','keyevent','KEYCODE_DEL');time.sleep(.2);adb('shell','input','keyevent','KEYCODE_ENTER');time.sleep(.2);type_editor_text('Next');time.sleep(1)
    actual=evaluate('MojiEditor.editor.getHTML()')['result']['result']['value'];print('EDIT',shown,actual);assert '<p>ABC12</p><p>Next</p>' in actual
    evaluate("MojiEditor.editor.chain().clearContent().focus().toggleBlockquote().run()")
    type_editor_text('Quote');adb('shell','input','keyevent','KEYCODE_ENTER');adb('shell','input','keyevent','KEYCODE_ENTER');type_editor_text('Normal');time.sleep(1)
    actual=evaluate('MojiEditor.editor.getHTML()')['result']['result']['value'];print('QUOTE',shown,actual);assert '<blockquote><p>Quote</p></blockquote><p>Normal</p>' in actual
    evaluate("MojiEditor.editor.chain().clearContent().focus().toggleTaskList().run()")
    type_editor_text('Task');adb('shell','input','keyevent','KEYCODE_ENTER');type_editor_text('Second');adb('shell','input','keyevent','KEYCODE_ENTER');adb('shell','input','keyevent','KEYCODE_ENTER');type_editor_text('Tail');time.sleep(1)
    actual=evaluate('MojiEditor.editor.getHTML()')['result']['result']['value'];print('TASK',shown,actual);assert '<p>Tail</p>' in actual and actual.count('data-type="taskItem"')==2
    (folder/('events-'+shown+'.json')).write_text(json.dumps(evaluate('probe'),indent=2))
    # Compare a plain browser-managed contenteditable within the same WebView.
    evaluate("document.body.innerHTML='<div contenteditable=true id=plain style=\"min-height:300px\">plain: </div>';document.getElementById('plain').focus();getSelection().selectAllChildren(document.getElementById('plain'));getSelection().collapseToEnd()")
    type_editor_text('XYZ789');time.sleep(1)
    print('PLAIN',shown,json.dumps(evaluate('document.body.innerHTML')))
    ws.close()
(folder/'editor-log.txt').write_text(adb('logcat','-d','-v','threadtime','-s','MojiEditor','chromium'))
