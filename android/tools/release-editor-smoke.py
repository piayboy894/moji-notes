#!/usr/bin/env python3
"""Black-box tests use accessibility, physical keys and the system picker in the signed APK."""
from native_ui import *
def tool(label):
    for _ in range(10):
        for n in hierarchy().iter('node'):
            if n.get('content-desc')==label or n.get('text')==label:tap(n);return
        adb('shell','input','swipe','1500','285','400','285','250')
    raise RuntimeError('Editor action not reachable: '+label)
def capture(name):
    REPORT.parent.mkdir(parents=True,exist_ok=True)
    (REPORT.parent/(name+'.png')).write_bytes(subprocess.check_output(['adb','exec-out','screencap','-p']))
def select_picker_file(name):
    # Recents is an index, not a directory listing. Browse the actual shared
    # Pictures directory so the test does not depend on gallery indexing.
    model=adb('shell','getprop','ro.product.model').strip().lower()
    def storage_root(root):
        for node in root.iter('node'):
            label=(node.get('text') or node.get('content-desc','')).strip().lower()
            if label and (label in ('internal storage','internal shared storage') or model==label or label.startswith('sdk_') or label.startswith('pixel ')):return node
    tap(wait_node(description='Show roots'))
    root=hierarchy();storage=storage_root(root)
    if storage is None:
        adb('shell','input','keyevent','KEYCODE_BACK');tap(wait_node(description='More options'))
        menu=hierarchy()
        show=next((n for n in menu.iter('node') if (n.get('text') or '').lower()=='show internal storage'),None)
        if show is not None:tap(show)
        else:adb('shell','input','keyevent','KEYCODE_BACK')
        tap(wait_node(description='Show roots'));root=hierarchy();storage=storage_root(root)
    if storage is None:raise RuntimeError('Shared storage root not found: '+str([n.get('text') for n in root.iter('node') if n.get('text')]))
    tap(storage);tap(wait_node(text='Pictures'))
    until=time.monotonic()+45
    while time.monotonic()<until:
        for n in hierarchy().iter('node'):
            label=n.get('text','')+' '+n.get('content-desc','')
            if name not in label and name.rsplit('.',1)[0] not in label:continue
            tap(n)
            # DocumentsUI may require confirmation when multiple files are allowed.
            for button in hierarchy().iter('node'):
                label=(button.get('text') or button.get('content-desc','')).strip().lower()
                if button.get('package','').endswith('.documentsui') and button.get('enabled')=='true' and label in ('open','select','done'):
                    tap(button);break
            return
        time.sleep(.5)
    raise RuntimeError('System picker did not show the published fixture image')
try:
    if adb('shell','getprop','ro.kernel.qemu').strip()!='1':raise RuntimeError('Emulator only')
    adb('shell','wm','size','1920x1200');adb('shell','wm','density','160');adb('shell','settings','put','secure','show_ime_with_hard_keyboard','0')
    adb('install','-r',str(APK));adb('shell','pm','clear',PACKAGE);launch();wait_node(resource='home-title')
    tap(wait_node(resource='add-notebook-card'));enter('notebook-name','EditorTest');tap(wait_node(text='提示词'));tap(wait_node(text='创建记事本'))
    tap(wait_node(description='新建记录'));tap(wait_editor());tool('引用');type_editor_text('Quote');adb('shell','input','keyevent','KEYCODE_ENTER','KEYCODE_ENTER');type_editor_text('Normal')
    wait_editor(contains='Normal');checks.append('signed shared editor quote inserts, exits with Enter and accepts normal text');capture('01-quote')
    tap(wait_node(text='插入变量'));enter('variable-name','Audience');tap(wait_node(text='插入'));wait_saved_variable('Audience');checks.append('native variable dialog inserts and persists an atomic variable in the signed WebView');capture('02-variable')
    tool('插入图片');adb('shell','input','keyevent','KEYCODE_BACK');wait_editor(contains='Normal');wait_saved_variable('Audience');checks.append('canceling system image selection preserves the document')
    import struct,zlib,random
    def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
    raw=b''.join(b'\x00'+random.Random(row).randbytes(2000*3) for row in range(1200))
    picture=REPORT.parent/'Moji-Editor-Test.png';picture.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>2I5B',2000,1200,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))
    # Register through MediaStore: adb-pushed files need not appear in Recents.
    # content uses com.android.shell attribution, so execute it as UID 2000
    # even though adbd is root for reading our private synthetic fixture.
    collection='content://media/external_primary/images/media'
    adb('shell','su','2000','content','insert','--uri',collection,'--bind','_display_name:s:Moji-Editor-Test.png','--bind','mime_type:s:image/png','--bind','relative_path:s:Pictures/')
    # Android's content insert command deliberately prints no URI. Query the row.
    rows=adb('shell','su','2000','content','query','--uri',collection,'--projection','_id:_display_name')
    ids=[int(re.search(r'_id=(\d+)',line).group(1)) for line in rows.splitlines() if 'Moji-Editor-Test.png' in line and re.search(r'_id=(\d+)',line)]
    if not ids:raise RuntimeError('MediaStore fixture registration failed: '+rows[:700])
    uri=collection+'/'+str(max(ids))
    with picture.open('rb') as stream:subprocess.run(['adb','shell','-T','su','2000','content','write','--uri',uri],stdin=stream,stdout=subprocess.PIPE,stderr=subprocess.PIPE,check=True,timeout=70)
    stored=subprocess.run(['adb','shell','-T','su','2000','content','read','--uri',uri],stdout=subprocess.PIPE,stderr=subprocess.PIPE,check=True,timeout=70).stdout
    if stored!=picture.read_bytes():raise RuntimeError('MediaStore fixture bytes do not match the PNG')
    adb('shell','su','2000','content','update','--uri',uri,'--bind','is_pending:i:0')
    tool('插入图片');select_picker_file('Moji-Editor-Test.png');wait_saved_image('Moji-Editor-Test.png');checks.append('real system image picker embeds a large image offline; screenshot captures its rendering');capture('03-image')
    adb('shell','am','force-stop',PACKAGE);launch();wait_node(resource='home-title');tap(wait_node(resource='book-Prompt'));tap(wait_node(contains='新提示词'));wait_editor(contains='Normal');wait_saved_variable('Audience');wait_saved_image('Moji-Editor-Test.png');checks.append('text, variable and image reopen after a complete process restart');capture('04-reopened')
    # UIAutomator dumps can outlast an ordinary four-second Snackbar. Extend
    # the emulator's accessibility timeout, without changing the shipped APK.
    adb('shell','settings','put','secure','accessibility_non_interactive_ui_timeout_ms','20000')
    adb('shell','settings','put','secure','accessibility_interactive_ui_timeout_ms','20000')
    tap(wait_node(description='偏好设置'));tap(wait_node(text='更新'));tap(wait_node(text='检查更新'))
    until=time.monotonic()+45;feedback=None;observed=set()
    while time.monotonic()<until:
        root=hierarchy();labels={n.get('text','') for n in root.iter('node')};observed.update(labels)
        # This is a foreground-feedback check. A public GitHub rate limit or
        # unavailable network must also produce visible, actionable feedback.
        outcomes={'你已在使用最新版本','检查更新失败','暂时无法连接，请检查网络后重试。','文件无法读取，请检查文件是否完整。'}
        feedback=next((label for label in labels if label in outcomes or label.startswith('发现新版本 ')),None)
        if feedback is not None and '偏好设置' in labels:
            (REPORT.parent/'update-feedback-window.xml').write_bytes(ET.tostring(root,encoding='utf-8'))
            capture('05-update-feedback');break
        time.sleep(.2)
    if feedback is None:raise RuntimeError('Update result was not visible in the foreground settings window: '+repr(sorted(observed)))
    (REPORT.parent/'update-feedback.json').write_text(json.dumps({'text':feedback,'githubReached':feedback=='你已在使用最新版本' or feedback.startswith('发现新版本 ')},ensure_ascii=False,indent=2))
    checks.append('signed APK check-update feedback is visible inside the foreground settings window: '+feedback)
    tap(wait_node(description='关闭'));wait_editor(contains='Normal');wait_saved_variable('Audience');wait_saved_image('Moji-Editor-Test.png')
    REPORT.write_text(json.dumps({'success':True,'framework':'Compose / Android WebView / Tiptap 3.34.0','checks':checks},indent=2))
except Exception as error:
    REPORT.parent.mkdir(parents=True,exist_ok=True);REPORT.write_text(json.dumps({'success':False,'error':str(error),'checks':checks},indent=2));capture('failure-screen')
    (REPORT.parent/'failure-window.xml').write_text(adb('shell','uiautomator','dump','/sdcard/moji-editor-failure.xml')+adb('shell','cat','/sdcard/moji-editor-failure.xml'));(REPORT.parent/'failure-runtime.txt').write_text(adb('logcat','-d','-v','threadtime','-s','AndroidRuntime'));raise
finally:
    adb('shell','wm','size','reset');adb('shell','wm','density','reset')
