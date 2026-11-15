using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Moji.Core;

namespace Moji.App;
public partial class MainWindow
{
    private void SettingsDialog(string initial="general")
    {
        var layout=new Grid();layout.ColumnDefinitions.Add(new(){Width=new GridLength(185)});layout.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});var tabs=new StackPanel{Margin=new Thickness(14,20,14,0)};var nav=new Border{Background=Ui.Resource("Side"),BorderBrush=Ui.Resource("Line"),BorderThickness=new Thickness(0,0,1,0),Child=tabs};layout.Children.Add(nav);var content=new StackPanel{Margin=new Thickness(33,28,33,28)};var scroll=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetColumn(scroll,1);layout.Children.Add(scroll);var dialog=Ui.Dialog(this,"偏好设置",layout,870,690);
        var buttons=new Dictionary<string,Button>();
        void Show(string tab)
        {
            content.Children.Clear();foreach(var (key,b)in buttons)b.Background=key==tab?Ui.Resource("AccentLight"):Brushes.Transparent;
            var state=VM.State!;
            void Heading(string title,string subtitle){content.Children.Add(Ui.Text(title,22,null,true));content.Children.Add(Ui.Spaced(Ui.Text(subtitle,11,"#9BA68D"),new Thickness(0,13,0,29)));}
            if(tab=="general")
            {
                Heading("通用设置","");
                var theme=new ComboBox{ItemsSource=new[]{"浅色","深色","跟随 Windows"},SelectedIndex=state.Preferences.Theme=="dark"?1:state.Preferences.Theme=="system"?2:0};theme.SelectionChanged+=(_,_)=>{var value=theme.SelectedIndex==1?"dark":theme.SelectedIndex==2?"system":"light";VM.Change(v=>v with{Preferences=v.Preferences with{Theme=value}},"edit");Ui.Theme(value);};content.Children.Add(Ui.Field("外观",theme));
                var size=new Slider{Minimum=11,Maximum=28,Value=state.Preferences.FontSize,TickFrequency=1,IsSnapToTickEnabled=true};var sizeLabel=Ui.Text("编辑器字号 · "+state.Preferences.FontSize,11);size.ValueChanged+=(_,_)=>{sizeLabel.Text="编辑器字号 · "+(int)size.Value;VM.Change(v=>v with{Preferences=v.Preferences with{FontSize=(int)size.Value}},"edit");};content.Children.Add(Ui.Spaced(sizeLabel,new Thickness(0,8,0,15)));content.Children.Add(size);
                var numbers=new CheckBox{Content="显示行号",IsChecked=state.Preferences.LineNumbers,Margin=new Thickness(0,25,0,12)};numbers.Click+=(_,_)=>VM.Change(v=>v with{Preferences=v.Preferences with{LineNumbers=numbers.IsChecked==true}},"edit");content.Children.Add(numbers);var wrap=new CheckBox{Content="自动换行",IsChecked=state.Preferences.WordWrap};wrap.Click+=(_,_)=>VM.Change(v=>v with{Preferences=v.Preferences with{WordWrap=wrap.IsChecked==true}},"edit");content.Children.Add(wrap);content.Children.Add(new Separator{Margin=new Thickness(0,25,0,22)});content.Children.Add(Ui.Text("常用快捷键",13,null,true));foreach(var (label,key)in new[]{("全局搜索","Ctrl K"),("新建记录","Ctrl N"),("编辑器内查找","Ctrl F"),("查找与替换","Ctrl H"),("保存历史版本","Ctrl S"),("全局云存档","Ctrl Shift S"),("锁定空间","Ctrl Shift L"),("专注模式","F11"),("多选记录","Ctrl / Shift + 单击"),("移动到记事本","Alt + 拖动记录")}){var row=new DockPanel{Margin=new Thickness(0,12,0,0)};var shortcut=Ui.Text(key,10,"#9FA990");DockPanel.SetDock(shortcut,Dock.Right);row.Children.Add(shortcut);row.Children.Add(Ui.Text(label,11,"#8C9980"));content.Children.Add(row);}
            }
            else if(tab=="sync")
            {
                Heading("云存档连接", "");
                var repo=Ui.Input(state.Sync.Repository); var token=new PasswordBox{Password=state.Sync.Token}; var code=new PasswordBox{Password=state.Sync.Code};
                content.Children.Add(Ui.Field("数据仓库",repo)); content.Children.Add(Ui.Field("GitHub Token",token));
                content.Children.Add(Ui.Spaced(Ui.Text("仅需数据仓库的 Contents 读写权限。",10,"#9BA68D"),new Thickness(0,-7,0,20)));
                content.Children.Add(Ui.Field("存档码 · 12–128 位字母或数字",InputActions(code,Ui.Small("生成",()=>code.Password=CryptoService.GeneratePassword(24,false)),Ui.Small("复制",()=>Copy(code.Password,true)))));
                content.Children.Add(Ui.Text("其他设备使用相同仓库、Token 和存档码。",10,"#9BA68D"));
                var status=Ui.Text("",11); content.Children.Add(status);
                void Save(){VM.Change(v=>v with{Sync=v.Sync with{Repository=repo.Text.Trim(),Token=token.Password.Trim(),Code=code.Password,Base=[]},Preferences=v.Preferences with{AutoSync=false}},"edit");}
                var save=Ui.Button("保存设置",()=>{Save();status.Text="连接设置已保存";});
                var open=Ui.Button("打开云存档",()=>{Save();dialog.Close();ShowCloudSaves();},true);
                save.IsEnabled=open.IsEnabled=!VM.Demo;
                content.Children.Add(Ui.Spaced(Ui.Row(save,Ui.Spaced(open,new Thickness(12,0,0,0))),new Thickness(0,24,0,0)));
                content.Children.Add(Ui.Spaced(Ui.Small("创建 GitHub 授权   ↗",()=>OpenUrl("https://github.com/settings/personal-access-tokens/new")),new Thickness(0,15,0,0)));

            }
            else if(tab=="security")
            {
                Heading("安全与隐私","");var minutes=new[]{1,5,10,15,30,60};var timeout=new ComboBox{ItemsSource=minutes.Select(n=>n+" 分钟").ToArray(),SelectedIndex=Math.Max(0,Array.IndexOf(minutes,state.Preferences.AutoLockMinutes))};timeout.SelectionChanged+=(_,_)=>VM.Change(v=>v with{Preferences=v.Preferences with{AutoLockMinutes=minutes[timeout.SelectedIndex]}},"edit");content.Children.Add(Ui.Field("闲置自动锁定",timeout));content.Children.Add(Ui.Text("系统锁屏和休眠时自动锁定。复制密码 30 秒后清除剪贴板。",10,"#9CA78F"));content.Children.Add(new Separator{Margin=new Thickness(0,25,0,25)});var old=new PasswordBox();var next=new PasswordBox();var again=new PasswordBox();content.Children.Add(Ui.Field("当前主密码",old));content.Children.Add(Ui.Field("新主密码（至少 10 个字符）",next));content.Children.Add(Ui.Field("确认新主密码",again));var result=Ui.Text("",11,"#B38C67");content.Children.Add(result);Button? change=null;change=Ui.Button("更换主密码",async()=>{change!.IsEnabled=false;try{if(next.Password!=again.Password)throw new InvalidOperationException("两次输入的新主密码不一致");await VM.ChangePasswordAsync(old.Password,next.Password);old.Clear();next.Clear();again.Clear();result.Text="主密码已更换";}catch(Exception e){result.Text=e.Message;}finally{change.IsEnabled=true;}},true);change.IsEnabled=!VM.Demo;content.Children.Add(Ui.Spaced(change,new Thickness(0,13,0,0)));
            }
            else if(tab=="data")
            {
                Heading("数据管理",$"{state.LiveBooks.Count()} 本记事本 · {state.LiveNotes.Count()} 条记录");
                void Option(string title,string hint,string button,Action action){var row=new DockPanel();var b=Ui.Small(button,action);DockPanel.SetDock(b,Dock.Right);row.Children.Add(b);row.Children.Add(Ui.Stack(Ui.Text(title,13,null,true),Ui.Spaced(Ui.Text(hint,10,"#9CA68E"),new Thickness(0,9,0,0))));var card=Ui.Card(row);card.Padding=new Thickness(18);card.Margin=new Thickness(0,0,0,15);content.Children.Add(card);}
                Option("加密备份","包含记录、历史版本及设置，使用主密码恢复。","导出",()=>_ = ExportBackup());Option("Markdown 文本集","按记事本导出 ZIP，不包含密码库。","导出",ExportAllMarkdown);Option("导入与恢复","支持 Markdown、TXT 和墨记加密备份。","导入",()=>_ = ImportFiles());Option("本地数据目录",VM.Store.DirectoryPath,"打开",()=>Process.Start(new ProcessStartInfo("explorer.exe",VM.Store.DirectoryPath){UseShellExecute=true}));content.Children.Add(Ui.Spaced(Ui.Text("云端 Git 提交历史可能仍保留旧密文；更换密码或清空回收站不会删除 GitHub 历史。",10,"#A2AA97"),new Thickness(0,18,0,0)));
            }
            else
            {
                Heading("墨记  M O J I","v"+App.CurrentVersion);var status=Ui.Text(availableUpdate!=null?$"发现新版本 {availableUpdate.Version}":"软件更新",15,null,true);var progress=new ProgressBar{Minimum=0,Maximum=1,Height=5,Margin=new Thickness(0,18,0,18),Visibility=Visibility.Collapsed};content.Children.Add(status);content.Children.Add(progress);
                Button? check=null;check=Ui.Button("检查更新",async()=>{check!.IsEnabled=false;status.Text="正在检查…";await CheckUpdates(true,message=>status.Text=message);check.IsEnabled=true;});Button? update=null;update=Ui.Button(downloadedUpdate==null?"下载更新":"重启并更新",async()=>{if(downloadedUpdate!=null){await InstallUpdate();return;}if(availableUpdate==null){status.Text="请先检查是否有新版本";return;}update!.IsEnabled=false;progress.Visibility=Visibility.Visible;try{downloadedUpdate=await updater.DownloadAsync(availableUpdate,Path.Combine(VM.Store.DirectoryPath,"updates"),new Progress<double>(p=>{progress.Value=p;status.Text=$"正在下载 {p:P0}";}));status.Text="SHA-256 校验通过，准备就绪";update.Content="重启并更新";}catch(Exception e){status.Text="下载失败："+e.Message;}finally{update.IsEnabled=true;}},true);content.Children.Add(Ui.Row(check,Ui.Spaced(update,new Thickness(12,0,0,0))));content.Children.Add(Ui.Spaced(Ui.Text("检测公开 Releases，无需账号或 Token。更新仅在你点击后安装。",10,"#A2AA97"),new Thickness(0,20,0,30)));content.Children.Add(Ui.Button("GitHub 项目与下载   ↗",()=>OpenUrl("https://github.com/piayboy894/moji-notes/releases")));content.Children.Add(Ui.Spaced(Ui.Button("反馈问题   ↗",()=>OpenUrl("https://github.com/piayboy894/moji-notes/issues")),new Thickness(0,12,0,0)));content.Children.Add(Ui.Spaced(Ui.Text("当前发行版未附 Windows 发行者签名；校验值用于验证下载完整性。",10,"#A2AA97"),new Thickness(0,27,0,0)));
            }
        }
        foreach(var(id,title)in new[]{("general","⚙   通用设置"),("sync","☁   云存档"),("security","⚿   安全与隐私"),("data","▤   数据管理"),("about","ⓘ   关于与更新")}){var button=Ui.Button(title,()=>Show(id));button.Style=(Style)FindResource("NavButton");button.Margin=new Thickness(0,3,0,3);button.FontSize=12;tabs.Children.Add(button);buttons[id]=button;}
        dialog.Closed+=(_,_)=>{if(VM.IsUnlocked){renderedId="";RefreshShell();}};Show(initial);dialog.ShowDialog();
    }
    private async Task CheckUpdates(bool userInitiated,Action<string>? status=null)
    {
        try{availableUpdate=await updater.CheckAsync(App.CurrentVersion,RuntimeInformation.OSArchitecture==Architecture.Arm64?"arm64":"x64");if(availableUpdate!=null){status?.Invoke($"发现新版本 {availableUpdate.Version}");if(!userInitiated)Toast($"墨记 {availableUpdate.Version} 已发布，可在「关于与更新」中更新");}else status?.Invoke("你已在使用最新版本");}catch(Exception e){if(userInitiated)status?.Invoke("检查失败，请稍后重试："+e.Message);}
    }
    private async Task InstallUpdate()
    {if(downloadedUpdate==null)return;try{await VM.SaveAsync();UpdateService.StartReplacement(downloadedUpdate,Environment.ProcessPath!);Close();}catch(Exception e){Toast("更新未能启动："+e.Message);}}
}
