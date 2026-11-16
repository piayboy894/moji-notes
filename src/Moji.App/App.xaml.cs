using System.IO;
using System.Windows;
using Moji.Core;

namespace Moji.App;
public partial class App : Application
{
    public static Version CurrentVersion { get { var v = typeof(App).Assembly.GetName().Version!; return new(v.Major, v.Minor, v.Build); } }
    // Render selection underneath glyphs so an opaque highlight keeps text readable.
    public App()=>AppContext.SetSwitch("Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering",false);
    private Mutex? instance;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);Ui.InitializeTheme();Ui.Theme("system");
        DispatcherUnhandledException += (_, error) => { MessageBox.Show("操作未能完成：" + UserMessages.For(error.Exception), "墨记", MessageBoxButton.OK, MessageBoxImage.Warning); error.Handled = true; };
        if (e.Args.FirstOrDefault() == "--apply-update")
        { try { var result = await UpdateService.ApplyReplacementAsync(e.Args); if (result != 0) MessageBox.Show("更新未能自动替换。已下载的新版本保存在：\n" + Environment.ProcessPath + "\n\n请将它复制到可写的程序目录后启动。", "墨记更新"); Shutdown(result); } catch (Exception error) { MessageBox.Show(UserMessages.For(error), "墨记更新"); Shutdown(3); } return; }
        if (e.Args.Contains("--smoke-test")) { await NativeSmoke.Run(e.Args); return; }
        instance = new Mutex(true, @"Local\MojiNotes-" + CryptoService.Hash(Environment.UserName)[..16], out var first);
        if (!first) { MessageBox.Show("墨记已在运行，请切换到已打开的窗口。", "墨记"); Shutdown(); return; }
        MainWindow = new MainWindow(); MainWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e) { try { instance?.ReleaseMutex(); } catch (ApplicationException) { } instance?.Dispose(); base.OnExit(e); }
}
