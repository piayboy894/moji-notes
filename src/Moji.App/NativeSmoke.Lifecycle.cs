using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Moji.Core;

namespace Moji.App;
internal static partial class NativeSmoke
{
    private static async Task CheckIdleRecoveryAndOverview(MainWindow window,WorkspaceViewModel vm,List<string> checks,string output)
    {
        using var watchdog=new System.Threading.Timer(_=>{File.WriteAllText(Path.Combine(output,"idle-watchdog.txt"),"Native idle recovery did not finish within 75 seconds.");Environment.Exit(24);},null,75000,System.Threading.Timeout.Infinite);
        var book=new Notebook{Name="恢复与浏览",Mode=NoteMode.Text};var note=new Note{NotebookId=book.Id,Title="已保存的记录",Content="闲置前已经收到的内容"};
        vm.Change(w=>w with{Notebooks=[..w.Notebooks,book],Notes=[..w.Notes,note]});window.Navigate(book.Id,note.Id);var editor=await window.ReadyEditor();await editor.Flush();await vm.SaveAsync();
        var originalFlush=vm.FlushEditor;vm.FlushEditor=()=>Task.FromException(new TimeoutException("Test-only unavailable renderer"));vm.EditNote(note.Id,n=>n with{Content="收到后立即保存的内容"});
        try{await vm.SaveAsync();throw new Exception("Unavailable explicit flush must remain visible to its caller");}catch(TimeoutException){}
        vm.FlushEditor=originalFlush;if((await vm.Store.ReadAsync())!.Notes.First(n=>n.Id==note.Id).Content!="收到后立即保存的内容")throw new Exception("Renderer failure prevented received data from reaching disk");
        window.Navigate(book.Id,note.Id);editor=await window.ReadyEditor();checks.Add("received document edits persist locally even when the editor flush fails");
        await editor.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.setWebLifecycleState","{\"state\":\"frozen\"}").WaitAsync(TimeSpan.FromSeconds(5));
        window.WindowState=WindowState.Minimized;var oldCount=vm.State!.Notes.Count;var ticks=0;var pulse=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};pulse.Tick+=(_,_)=>ticks++;pulse.Start();
        window.AddNote();var created=vm.SelectedId;var newTitle=FindNamed<TextBox>(window,"RecordTitle")??throw new Exception("New title is not available during renderer recovery");newTitle.Text="恢复时立即输入的标题";
        try{editor=await window.ReadyEditor().WaitAsync(TimeSpan.FromSeconds(35));}finally{pulse.Stop();window.WindowState=WindowState.Normal;window.Activate();}
        if(vm.State.Notes.Count!=oldCount+1||vm.SelectedId!=created||ticks<3||vm.State.Notes.First(n=>n.Id==created).Title!="恢复时立即输入的标题"||vm.State.Notes.First(n=>n.Id==note.Id).Content!="收到后立即保存的内容")throw new Exception("Background renderer recovery blocked UI or corrupted the original document");
        await Script(editor,"window.MojiEditor.editor.commands.insertContent('恢复后继续编辑')");await editor.Flush();await vm.SaveAsync();
        if(!vm.State.Notes.First(n=>n.Id==created).Content.Contains("恢复后继续编辑"))throw new Exception("Recovered renderer cannot continue saving");
        Capture(window,Path.Combine(output,"idle-recovery.png"));checks.Add("a frozen background renderer recovers while creating a record, keeps dispatcher input alive, and saves both old and new documents");
        var prior=editor.Browser;var processes=prior.CoreWebView2.Environment.GetProcessInfos().Where(p=>p.Kind==Microsoft.Web.WebView2.Core.CoreWebView2ProcessKind.Renderer).ToList();if(processes.Count==0)throw new Exception("No test renderer to terminate");
        foreach(var process in processes)try{System.Diagnostics.Process.GetProcessById(process.ProcessId).Kill();}catch(ArgumentException){}
        await editor.ResumeAsync().WaitAsync(TimeSpan.FromSeconds(35));if(ReferenceEquals(prior,editor.Browser))throw new Exception("A failed renderer was not replaced");
        if(!(await editor.Browser.CoreWebView2.ExecuteScriptAsync("window.MojiEditor.snapshot().content")).Contains("恢复后继续编辑"))throw new Exception("Renderer restart did not restore the acknowledged body");
        checks.Add("a terminated renderer is recreated from the acknowledged document without recreating the native editor chrome");
        var browsing=vm.State;var bulk=Enumerable.Range(0,1000).Select(i=>new Note{NotebookId=book.Id,Title=$"浏览记录 {i}",Content="卡片预览"}).ToList();vm.Change(w=>w with{Notes=[..w.Notes,..bulk]});window.Navigate(book.Id);await Render();
        var grid=FindNamed<ListBox>(window,"OverviewGrid")??throw new Exception("Notebook did not open its card overview");
        var containers=FindAll<ListBoxItem>(grid).Count();Capture(window,Path.Combine(output,"notebook-overview-before-selection.png"));if(containers>60||containers<2||vm.SelectedId!="")throw new Exception($"Card overview is not virtualized or opened a document automatically: containers={containers}, items={grid.Items.Count}, selected={vm.SelectedId}, width={grid.ActualWidth}, height={grid.ActualHeight}, panels={string.Join(",",FindAll<System.Windows.Controls.Panel>(grid).Select(p=>$"{p.GetType().Name}:{p.ActualWidth}x{p.ActualHeight}"))}");
        RectangleSelection.ApplyRegion(grid,new Rect(0,0,grid.ActualWidth,Math.Min(220,grid.ActualHeight)));if(grid.SelectedItems.Count<2||FindNamed<TextBox>(window,"RecordTitle")!=null)throw new Exception("Overview rectangle selection opened the editor");
        Capture(window,Path.Combine(output,"notebook-overview.png"));grid.UnselectAll();
        var motion=window.RecordMotionOverride;window.RecordMotionOverride=true;var stages=new HashSet<string>();
        window.RecordEntryStage=stage=>{stages.Add(stage);var frame=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(70)};frame.Tick+=(_,_)=>{frame.Stop();Capture(window,Path.Combine(output,"record-entry-"+stage+".png"));};frame.Start();};
        var late=(NoteRow)grid.Items[900];grid.ScrollIntoView(late);await Render();var stableBrowser=editor.Browser;
        window.OpenOverviewRecord(late.Note.Id);await window.RecordEntryCompletion.WaitAsync(TimeSpan.FromSeconds(10));editor=await window.ReadyEditor();await Render();
        var detail=FindNamed<ListBox>(window,"RecordList")!;var activeRow=(NoteRow)detail.SelectedItem;var activeCard=detail.ItemContainerGenerator.ContainerFromItem(activeRow)as ListBoxItem;
        var viewport=FindAll<ScrollViewer>(detail).First();var activeBounds=activeCard?.TransformToAncestor(viewport).TransformBounds(new Rect(activeCard.RenderSize));
        var detailContainers=FindAll<ListBoxItem>(detail).Count();
        if(activeRow.Note.Id!=late.Note.Id||activeBounds==null||activeBounds.Value.Top<-.5||activeBounds.Value.Bottom>viewport.ViewportHeight+.5||detailContainers>60||!ReferenceEquals(stableBrowser,editor.Browser)||window.RecordEntryActive||!stages.SetEquals(new[]{"cards","editor"})||window.LastRecordEntrySnapshotCount>70)throw new Exception($"Late record entry lost its card, browser or motion lifecycle: visible={activeBounds}, viewport={viewport.ViewportHeight}, containers={detailContainers}, stages={string.Join(",",stages)}, snapshots={window.LastRecordEntrySnapshotCount}");
        Capture(window,Path.Combine(output,"late-record-visible.png"));checks.Add("overview cards move into the left list before the ready editor frame enters from the right; a late record is fully visible and the same browser is retained with bounded temporary snapshots");
        window.RecordEntryStage=null;window.ReturnToOverview();await Render();window.RecordMotionOverride=false;var snapshots=window.LastRecordEntrySnapshotCount;window.OpenOverviewRecord(late.Note.Id);await window.RecordEntryCompletion;await window.ReadyEditor();
        if(window.RecordEntryActive||window.LastRecordEntrySnapshotCount!=snapshots)throw new Exception("Reduced motion must reveal the record without capturing animation frames");
        window.ReturnToOverview();await Render();window.RecordMotionOverride=true;window.OpenOverviewRecord(late.Note.Id);window.ReturnToOverview();await window.RecordEntryCompletion;
        if(window.RecordEntryActive||FindNamed<ListBox>(window,"OverviewGrid")==null)throw new Exception("Returning during entry left an overlay or wrong scene");
        window.RecordMotionOverride=motion;checks.Add("reduced motion uses no transition snapshots and immediately returning cancels the in-flight entry without leaving an overlay");
        grid=FindNamed<ListBox>(window,"OverviewGrid")!;FindNamed<TextBox>(window,"OverviewSearch")!.Text="浏览记录 99";await Render();
        var chosen=((NoteRow)grid.Items[0]).Note.Id;var browser=editor.Browser;window.OpenOverviewRecord(chosen);await window.ReadyEditor();var back=FindNamed<Button>(window,"BackToOverview")??throw new Exception("Missing top-left return action");back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Render();
        if(FindNamed<ListBox>(window,"OverviewGrid")==null||vm.Search!="浏览记录 99"||!ReferenceEquals(browser,editor.Browser))throw new Exception("Returning to cards lost the search or rebuilt the renderer");
        checks.Add("notebooks default to a virtualized card grid, support rectangle multi-selection, and return from the editor's top-left action without losing search or the browser instance");
        vm.Change(_=>browsing!);window.Navigate(book.Id,created);await window.ReadyEditor();
    }
}
