"""Inspect only disposable-emulator fixture data from the exact signed APK."""
import sys
sys.path.insert(0,'tools')
from native_ui import *
folder=Path('app/build/outputs/editor-probe');folder.mkdir(parents=True,exist_ok=True)
if adb('shell','getprop','ro.kernel.qemu').strip()!='1':raise RuntimeError('Emulator required')
adb('install','-r',str(APK));adb('shell','pm','clear',PACKAGE);launch();wait_node(resource='home-title')
tap(wait_node(resource='add-notebook-card'));enter('notebook-name','Probe');tap(wait_node(text='创建记事本'));adb('shell','input','keyevent','KEYCODE_BACK');tap(wait_node(resource='book-Text'));tap(wait_node(description='新建记录'));tap(wait_editor());type_editor_text('NativeHello');time.sleep(2)
(folder/'hierarchy.xml').write_text(ET.tostring(hierarchy(),encoding='unicode'))
(folder/'screen.png').write_bytes(subprocess.check_output(['adb','exec-out','screencap','-p']))
(folder/'editor-log.txt').write_text(adb('logcat','-d','-v','threadtime','-s','MojiEditor','chromium'))
print(adb('root'));adb('wait-for-device')
text=adb('shell','cat','/data/user/0/app.moji.android/no_backup/vault/workspace.json')
(folder/'fixture-workspace.json').write_text(text)
print(json.dumps([{'title':n['title'],'content':n['content']} for n in json.loads(text)['notes']],ensure_ascii=False))
