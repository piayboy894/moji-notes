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
    adb('push',str(picture),'/sdcard/Download/Moji-Editor-Test.png');adb('shell','am','broadcast','-a','android.intent.action.MEDIA_SCANNER_SCAN_FILE','-d','file:///sdcard/Download/Moji-Editor-Test.png')
    tool('插入图片');tap(wait_node(description_contains='Moji-Editor-Test.png'));wait_saved_image('Moji-Editor-Test.png');checks.append('real system image picker embeds a large image offline; screenshot captures its rendering');capture('03-image')
    adb('shell','am','force-stop',PACKAGE);launch();wait_node(resource='home-title');tap(wait_node(resource='book-Prompt'));tap(wait_node(contains='新提示词'));wait_editor(contains='Normal');wait_saved_variable('Audience');wait_saved_image('Moji-Editor-Test.png');checks.append('text, variable and image reopen after a complete process restart');capture('04-reopened')
    REPORT.write_text(json.dumps({'success':True,'framework':'Compose / Android WebView / Tiptap 3.34.0','checks':checks},indent=2))
except Exception as error:
    REPORT.parent.mkdir(parents=True,exist_ok=True);REPORT.write_text(json.dumps({'success':False,'error':str(error),'checks':checks},indent=2));capture('failure-screen')
    (REPORT.parent/'failure-window.xml').write_text(adb('shell','uiautomator','dump','/sdcard/moji-editor-failure.xml')+adb('shell','cat','/sdcard/moji-editor-failure.xml'));(REPORT.parent/'failure-runtime.txt').write_text(adb('logcat','-d','-v','threadtime','-s','AndroidRuntime'));raise
finally:
    adb('shell','wm','size','reset');adb('shell','wm','density','reset')
