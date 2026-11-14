using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Moji.Core;

namespace Moji.App;
public partial class MainWindow
{
    private async Task ShowAuthAsync()
    {
        EncryptedEnvelope? envelope = null; string readError = "";
        try { envelope = await VM.Store.ReadAsync(); } catch (Exception error) { readError = error.Message; }
        var existing = envelope != null; var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var story = new Grid { Background = Ui.Brush("#E9EDE3") }; grid.Children.Add(story);
        var storyContent = new StackPanel { Margin = new Thickness(55,45,50,40) };
        var logo = Ui.Row(new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/icon.png")), Width = 44, Height = 44 }, Ui.Spaced(Ui.Text("墨记   M O J I", 23, "#303C34", true), new Thickness(15,0,0,0))); storyContent.Children.Add(logo);
        storyContent.Children.Add(Ui.Spaced(Ui.Text("YOUR PRIVATE CORNER", 10, "#7B8C70"), new Thickness(0,100,0,24)));
        storyContent.Children.Add(Ui.Text("重要的，\n都放在这里。", 56, "#43563A"));
        storyContent.Children.Add(Ui.Spaced(Ui.Text("文字的片刻，灵感的火花，\n还有只属于你的秘密。", 13, "#7C8973"), new Thickness(0,28,0,0))); story.Children.Add(storyContent);
        var paper = Ui.Card(Ui.Stack(Ui.Text("记", 90, "#697E58"), Ui.Spaced(Ui.Text("A PLACE FOR\nEVERY THOUGHT.", 11, "#8D977F"), new Thickness(0,20,0,0))), "#F5F2E8"); paper.Padding = new Thickness(30,15,30,30); paper.Width = 220; paper.Height = 280; paper.HorizontalAlignment = HorizontalAlignment.Right; paper.VerticalAlignment = VerticalAlignment.Bottom; paper.Margin = new Thickness(0,0,60,80); paper.RenderTransform = new RotateTransform(-8,110,140); story.Children.Add(paper);
        var form = new StackPanel { Width = 350, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(form,1); grid.Children.Add(form);
        var emblem = Ui.Card(Ui.Text("⚿", 27, "#7D9469"), "#EFF3E9", 14); emblem.Width = 55; emblem.Height = 55; emblem.HorizontalAlignment = HorizontalAlignment.Left; emblem.Padding = new Thickness(13,4,10,8); emblem.Margin = new Thickness(0,0,0,30); form.Children.Add(emblem);
        form.Children.Add(Ui.Text(existing ? "WELCOME BACK" : "MAKE IT YOURS", 9, "#A2AC98"));
        var heading = Ui.Text(existing ? "欢迎回来" : "建立你的私人空间", 29, null, true); heading.Margin = new Thickness(0,12,0,14); form.Children.Add(heading); form.Children.Add(Ui.Text(existing ? "输入主密码，继续你的记录。" : "一个主密码，妥帖保护所有记录。", 12, "#919B86"));
        var pw = new PasswordBox { Margin = new Thickness(0,0,0,0), Height = 46 }; var confirm = new PasswordBox { Height = 46 };
        var pwField = Ui.Field(existing ? "主密码" : "设置主密码（至少 10 个字符）", pw); pwField.Margin = new Thickness(0,30,0,20); form.Children.Add(pwField); var confirmField = Ui.Field("确认主密码", confirm); if (existing) confirmField.Visibility = Visibility.Collapsed; form.Children.Add(confirmField);
        var errorText = Ui.Text(readError, 11, "#B56C5F"); errorText.Margin = new Thickness(0,0,0,12); form.Children.Add(errorText);
        EncryptedEnvelope? imported = null; Button? submit = null;
        async Task Submit()
        {
            if (submit == null || !submit.IsEnabled) return; errorText.Text = ""; submit.IsEnabled = false; submit.Content = "正在解锁…";
            try { if (existing || imported != null) await VM.UnlockAsync(pw.Password, imported); else { if (pw.Password != confirm.Password) throw new InvalidOperationException("两次输入的主密码不一致"); await VM.CreateAsync(pw.Password); } pw.Clear(); confirm.Clear(); }
            catch (Exception error) { errorText.Text = error.Message; submit.IsEnabled = true; submit.Content = existing ? "解锁空间   →" : "创建空间   →"; }
        }
        submit = Ui.Button(existing ? "解锁空间   →" : "创建空间   →", () => _ = Submit(), true); submit.Height = 46; form.Children.Add(submit); pw.KeyDown += (_, e) => { if (e.Key == Key.Enter) _ = Submit(); }; confirm.KeyDown += (_, e) => { if (e.Key == Key.Enter) _ = Submit(); };
        form.Children.Add(Ui.Spaced(Ui.Text("AES-256-GCM 加密 · 主密码无法找回", 10, "#A3AD98"), new Thickness(22,17,0,0)));
        form.Children.Add(new Separator { Margin = new Thickness(0,32,0,23) });
        var demo = Ui.Button("先逛逛演示空间   ↗", VM.StartDemo); demo.Background = Brushes.Transparent; demo.BorderThickness = new Thickness(0); demo.HorizontalAlignment = HorizontalAlignment.Left; demo.Foreground = Ui.Resource("Accent"); demo.FontSize = 12; form.Children.Add(demo);
        var import = Ui.Button("↥  从加密备份恢复", async () => { var open = new OpenFileDialog { Filter = "墨记加密备份|*.moji;*.mojibak;*.json" }; if (open.ShowDialog(this) != true) return; try { imported = await VaultStore.ReadEnvelope(open.FileName); heading.Text = "恢复你的空间"; pwField.Children[0].SetValue(TextBlock.TextProperty, "备份的主密码"); confirmField.Visibility = Visibility.Collapsed; submit.Content = "解密并恢复   →"; existing = true; pw.Clear(); if (envelope != null && !Confirm("替换当前本地空间？", "恢复后，本地空间将使用备份内容。原文件仍保留为加密 .bak 备份。")) { imported = null; await ShowAuthAsync(); return; } pw.Focus(); } catch (Exception error) { errorText.Text = error.Message; } }); import.Background = Brushes.Transparent; import.BorderThickness = new Thickness(0); import.HorizontalAlignment = HorizontalAlignment.Left; import.Foreground = Ui.Resource("Sub"); import.FontSize = 11; import.Margin = new Thickness(0,7,0,0); form.Children.Add(import);
        AuthHost.Content = grid; AuthHost.Visibility = Visibility.Visible; Shell.Visibility = Visibility.Collapsed; Dispatcher.InvokeAsync(() => pw.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }
}
