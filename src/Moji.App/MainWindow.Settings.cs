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
            void Heading(string title,string subtitle){content.Children.Add(Ui.Text(title,22,null,true));content.Children.Add(Ui.Spaced(Ui.Text(subtitle,11,"Sub"),new Thickness(0,13,0,29)));}
            if(tab=="general")
            {
                Heading("通用设置","");
                RenderThemeSettings(content);
                RenderFontSetting(content,dialog);
                var size=new Slider{Minimum=11,Maximum=28,Value=state.Preferences.FontSize,TickFrequency=1,IsSnapToTickEnabled=true};var sizeLabel=Ui.Text("编辑器字号 · "+state.Preferences.FontSize,11);size.ValueChanged+=(_,_)=>{sizeLabel.Text="编辑器字号 · "+(int)size.Value;VM.Change(v=>v with{Preferences=v.Preferences with{FontSize=(int)size.Value}},"edit");};content.Children.Add(Ui.Spaced(sizeLabel,new Thickness(0,8,0,15)));content.Children.Add(size);

            }
            else if(tab=="sync")
            {
                Heading("云存档连接", "");
                var repo=Ui.Input(state.Sync.Repository); var token=new PasswordBox{Password=state.Sync.Token};
                content.Children.Add(Ui.Field("数据仓库",repo)); content.Children.Add(Ui.Field("GitHub Token",token));
                content.Children.Add(Ui.Spaced(Ui.Text("仅需数据仓库的 Contents 读写权限。",10,"Sub"),new Thickness(0,-7,0,20)));
                var status=Ui.Text("",11); content.Children.Add(status);
                void Save(){VM.Change(v=>v with{Sync=v.Sync with{Repository=repo.Text.Trim(),Token=token.Password.Trim(),Base=[]},Preferences=v.Preferences with{AutoSync=false}},"edit");}
                var save=Ui.Button("保存设置",()=>{Save();status.Text="连接设置已保存";});
                var open=Ui.Button("打开云存档",()=>{Save();dialog.Close();ShowCloudSaves();},true);

                content.Children.Add(Ui.Spaced(Ui.Row(save,Ui.Spaced(open,new Thickness(12,0,0,0))),new Thickness(0,24,0,0)));
                content.Children.Add(Ui.Spaced(Ui.Small("创建 GitHub 授权   ↗",()=>OpenUrl("https://github.com/settings/personal-access-tokens/new")),new Thickness(0,15,0,0)));

            }
            else if(tab=="shortcuts") { RenderShortcuts(content, () => Show("shortcuts")); }
            else if(tab=="data")
            {
                Heading("数据管理",$"{state.LiveBooks.Count()} 本记事本 · {state.LiveNotes.Count()} 条记录");
                void Option(string title,string hint,string button,Action action){var row=new DockPanel();var b=Ui.Small(button,action);DockPanel.SetDock(b,Dock.Right);row.Children.Add(b);row.Children.Add(Ui.Stack(Ui.Text(title,13,null,true),Ui.Spaced(Ui.Text(hint,10,"Sub"),new Thickness(0,9,0,0))));var card=Ui.Card(row);card.Padding=new Thickness(18);card.Margin=new Thickness(0,0,0,15);content.Children.Add(card);}
                Option("本地存档","全部记事本与记录。","导出",()=>_ = ExportBackup());Option("文本集","按记事本导出 ZIP，不包含密码库。","导出",ExportAllMarkdown);Option("导入与恢复","支持 Markdown、TXT 和墨记存档。","导入",()=>_ = ImportFiles());Option("本地数据目录",VM.Store.DirectoryPath,"打开",()=>Process.Start(new ProcessStartInfo("explorer.exe",VM.Store.DirectoryPath){UseShellExecute=true}));content.Children.Add(Ui.Spaced(Ui.Text("云端 Git 提交历史可能仍保留旧存档；清空回收站不会删除 GitHub 历史。",10,"Sub"),new Thickness(0,18,0,0)));
            }
            else
            {
                Heading("墨记  M O J I","v"+App.CurrentVersion);var status=Ui.Text(availableUpdate!=null?$"发现新版本 {availableUpdate.Version}":"软件更新",15,null,true);var progress=new ProgressBar{Minimum=0,Maximum=1,Height=5,Margin=new Thickness(0,18,0,18),Visibility=Visibility.Collapsed};content.Children.Add(status);content.Children.Add(progress);
                Button? check=null;check=Ui.Button("检查更新",async()=>{check!.IsEnabled=false;status.Text="正在检查…";await CheckUpdates(true,message=>status.Text=message);check.IsEnabled=true;});Button? update=null;update=Ui.Button(downloadedUpdate==null?"下载更新":"重启并更新",async()=>{if(downloadedUpdate!=null){await InstallUpdate();return;}if(availableUpdate==null){status.Text="请先检查是否有新版本";return;}update!.IsEnabled=false;progress.Visibility=Visibility.Visible;try{downloadedUpdate=await updater.DownloadAsync(availableUpdate,Path.Combine(VM.Store.DirectoryPath,"updates"),new Progress<double>(p=>{progress.Value=p;status.Text=$"正在下载 {p:P0}";}));status.Text="SHA-256 校验通过，准备就绪";update.Content="重启并更新";}catch(Exception e){status.Text="下载失败："+UserMessages.For(e);}finally{update.IsEnabled=true;}},true);content.Children.Add(Ui.Row(check,Ui.Spaced(update,new Thickness(12,0,0,0))));content.Children.Add(Ui.Spaced(Ui.Text("检测公开 Releases，无需账号或 Token。更新仅在你点击后安装。",10,"Sub"),new Thickness(0,20,0,30)));content.Children.Add(Ui.Button("GitHub 项目与下载   ↗",()=>OpenUrl("https://github.com/piayboy894/moji-notes/releases")));content.Children.Add(Ui.Spaced(Ui.Button("反馈问题   ↗",()=>OpenUrl("https://github.com/piayboy894/moji-notes/issues")),new Thickness(0,12,0,0)));content.Children.Add(Ui.Spaced(Ui.Text("当前发行版未附 Windows 发行者签名；校验值用于验证下载完整性。",10,"Sub"),new Thickness(0,27,0,0)));
            }
        }
        foreach(var(id,title)in new[]{("general","通用设置"),("sync","云存档"),("shortcuts","快捷键"),("data","数据管理"),("about","关于与更新")}){var button=Ui.Button(title,()=>Show(id));button.Style=(Style)FindResource("NavButton");button.Margin=new Thickness(0,3,0,3);button.FontSize=12;tabs.Children.Add(button);buttons[id]=button;}
        dialog.Closed+=(_,_)=>{if(VM.IsUnlocked){renderedId="";RefreshShell();}};Show(initial);dialog.ShowDialog();
    }
    private async Task CheckUpdates(bool userInitiated,Action<string>? status=null)
    {
        try{availableUpdate=await updater.CheckAsync(App.CurrentVersion,RuntimeInformation.OSArchitecture==Architecture.Arm64?"arm64":"x64");if(availableUpdate!=null){status?.Invoke($"发现新版本 {availableUpdate.Version}");if(!userInitiated)Toast($"墨记 {availableUpdate.Version} 已发布，可在「关于与更新」中更新");}else status?.Invoke("你已在使用最新版本");}catch(Exception e){if(userInitiated)status?.Invoke(UserMessages.For(e,"检查更新未完成，请稍后重试。"));}
    }
    private async Task InstallUpdate()
    {if(downloadedUpdate==null)return;try{await VM.SaveAsync();UpdateService.StartReplacement(downloadedUpdate,Environment.ProcessPath!);Close();}catch(Exception e){Toast("更新未能启动："+UserMessages.For(e));}}
}
