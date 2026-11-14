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
        var existing = envelope != null; var grid = new Grid();
        var form = new StackPanel { Width = 350, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(form);
        var logo = Ui.Row(new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/icon.png")), Width = 43, Height = 43 }, Ui.Spaced(Ui.Text("墨记   M O J I", 24, null, true), new Thickness(14,0,0,0)));
        form.Children.Add(Ui.Spaced(logo, new Thickness(0,0,0,45)));
        var heading = Ui.Text(existing ? "解锁墨记" : "创建空间", 25, null, true); form.Children.Add(heading);
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
        submit = Ui.Button(existing ? "解锁空间   →" : "创建空间   →", () => _ = Submit(), true); submit.Height = 46; submit.IsEnabled = string.IsNullOrEmpty(readError); form.Children.Add(submit); pw.KeyDown += (_, e) => { if (e.Key == Key.Enter) _ = Submit(); }; confirm.KeyDown += (_, e) => { if (e.Key == Key.Enter) _ = Submit(); };
        form.Children.Add(Ui.Spaced(Ui.Text("请妥善保管主密码", 10, "#A3AD98"), new Thickness(22,17,0,0)));
        form.Children.Add(new Separator { Margin = new Thickness(0,32,0,23) });
        var demo = Ui.Button("查看演示   ↗", VM.StartDemo); demo.Background = Brushes.Transparent; demo.BorderThickness = new Thickness(0); demo.HorizontalAlignment = HorizontalAlignment.Left; demo.Foreground = Ui.Resource("Accent"); demo.FontSize = 12; form.Children.Add(demo);
        var import = Ui.Button("恢复备份", async () => { var open = new OpenFileDialog { Filter = "墨记加密备份|*.moji;*.mojibak;*.json" }; if (open.ShowDialog(this) != true) return; try { imported = await VaultStore.ReadEnvelope(open.FileName); heading.Text = "恢复你的空间"; pwField.Children[0].SetValue(TextBlock.TextProperty, "备份的主密码"); confirmField.Visibility = Visibility.Collapsed; submit.Content = "解密并恢复   →"; submit.IsEnabled = true; existing = true; pw.Clear(); if (envelope != null && !Confirm("替换当前本地空间？", "恢复后，本地空间将使用备份内容。原文件仍保留为加密 .bak 备份。")) { imported = null; await ShowAuthAsync(); return; } pw.Focus(); } catch (Exception error) { errorText.Text = error.Message; } }); import.Background = Brushes.Transparent; import.BorderThickness = new Thickness(0); import.HorizontalAlignment = HorizontalAlignment.Left; import.Foreground = Ui.Resource("Sub"); import.FontSize = 11; import.Margin = new Thickness(0,7,0,0); form.Children.Add(import);
        AuthHost.Content = grid; AuthHost.Visibility = Visibility.Visible; Shell.Visibility = Visibility.Collapsed; _ = Dispatcher.InvokeAsync(() => pw.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }
}
