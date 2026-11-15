#!/usr/bin/env python3
"""Black-box editor regression against the actual optimized, signed release APK."""
from native_ui import *

def tool(label):
    for _ in range(8):
        for n in hierarchy().iter('node'):
            if n.get('content-desc') == label:
                tap(n); return
        adb('shell','input','swipe','920','430','120','430','250')
    raise RuntimeError('Toolbar action not reachable: '+label)

def capture(name):
    REPORT.parent.mkdir(parents=True,exist_ok=True)
    result=subprocess.run(['adb','exec-out','screencap','-p'],stdout=subprocess.PIPE,check=True)
    (REPORT.parent/(name+'.png')).write_bytes(result.stdout)

try:
    if adb('shell','getprop','ro.kernel.qemu').strip()!='1':raise RuntimeError('Emulator only')
    adb('shell','wm','size','1920x1200');adb('shell','wm','density','160')
    adb('shell','settings','put','secure','show_ime_with_hard_keyboard','0')
    adb('install','-r',str(APK));launch();wait_node(resource='home-title')
    tap(wait_node(resource='add-notebook-card'));enter('notebook-name','EditorTest')
    tap(wait_node(text='提示词'));tap(wait_node(text='创建记事本'))
    tap(wait_node(description='新建记录'));wait_node(resource='note-content')
    # Empty editor and caret at the beginning were absent from earlier smoke checks.
    tool('引用');wait_node(resource='note-content')
    checks.append('quote can be inserted into a completely empty release editor')
    capture('01-quote')
    tool('撤销');wait_node(resource='note-content')
    tap(wait_node(text='插入变量'));enter('variable-name','Audience');tap(wait_node(text='插入'))
    wait_node(resource='note-content',contains='Audience');checks.append('explicit variable inserts in an empty release editor')
    capture('02-variable')
    tool('撤销');wait_node(resource='note-content')
    tool('插入表格');tap(wait_node(text='插入'));wait_node(resource='table-cell-0-0')
    enter('table-cell-0-0','TableValue');tap(wait_node(text='添加列'));tap(wait_node(text='添加行'))
    checks.append('release table inserts, accepts text and adds rows and columns');capture('03-table')
    tool('撤销');tool('重做');wait_node(resource='table-cell-0-0',contains='TableValue')
    checks.append('release structural undo and redo preserve table data')
    REPORT.write_text(json.dumps({'success':True,'checks':checks},indent=2))
except Exception as error:
    REPORT.parent.mkdir(parents=True,exist_ok=True)
    REPORT.write_text(json.dumps({'success':False,'error':str(error),'checks':checks},indent=2))
    capture('failure-screen')
    (REPORT.parent/'failure-window.xml').write_text(adb('shell','uiautomator','dump','/sdcard/moji-editor-failure.xml')+adb('shell','cat','/sdcard/moji-editor-failure.xml'))
    (REPORT.parent/'failure-runtime.txt').write_text(adb('logcat','-d','-v','threadtime','-s','AndroidRuntime'))
    raise
finally:
    adb('shell','wm','size','reset');adb('shell','wm','density','reset')
