using Moji.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Moji.App;
public partial class MainWindow
{
    private async Task OpenWorkspaceAsync()
    {
        try { if (await VM.OpenAsync()) return; ShowMigration(); }
        catch (Exception error) {
            var panel = Ui.Stack(Ui.Text("无法打开本机数据",22,null,true), Ui.Text(UserMessages.For(error),12), Ui.Button("重试", () => _ = OpenWorkspaceAsync()));
            panel.Width = 380; panel.VerticalAlignment = VerticalAlignment.Center; AuthHost.Content = panel;
        }
    }
    private void ShowMigration()
    {
        var panel = new StackPanel { Width = 360, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Ui.Text("迁移旧版数据",24,null,true));
        panel.Children.Add(Ui.Spaced(Ui.Text("输入一次旧密码，以后直接进入。",12),new Thickness(0,18,0,24)));
        var password = new PasswordBox(); panel.Children.Add(Ui.Field("旧密码",password)); var status = Ui.Text("",11); panel.Children.Add(status);
        Button? button = null;
        async Task Migrate() { if (button?.IsEnabled != true) return; button.IsEnabled = false; try { await VM.MigrateAsync(password.Password); password.Clear(); } catch (Exception error) { status.Text = UserMessages.For(error); button.IsEnabled = true; } }
        button = Ui.Button("迁移并打开", () => _ = Migrate(), true); panel.Children.Add(button);
        password.KeyDown += (_,e) => { if (e.Key == Key.Enter) _ = Migrate(); };
        AuthHost.Content = panel; AuthHost.Visibility = Visibility.Visible; Shell.Visibility = Visibility.Collapsed; password.Focus();
    }
}
