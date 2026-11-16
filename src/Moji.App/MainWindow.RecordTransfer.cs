using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Moji.Core;

namespace Moji.App;
public partial class MainWindow
{
    private const string RecordClipboardFormat = "Moji.RecordClipboard";
    private const string RecordDragFormat = "Moji.RecordsDrag";
    private RecordBundle? recordClipboard;
    private string recordClipboardToken = "";
    private long recordClipboardEpoch;
    private bool recordTransferBusy, draggingRecords;
    private RecordBundle? draggingRecordBundle;
    private long draggingRecordEpoch;
    private string recordDragToken = "";
    private Button? lastRecordDropTarget;
    private bool lastRecordDropCompatible;
    private readonly Dictionary<Button, RecordDropOutline> recordDropOutlines = [];
    private bool InRecordKeyboardContext => Keyboard.FocusedElement is not TextBoxBase and not PasswordBox
        && cachedEditor?.Browser.IsKeyboardFocusWithin != true
        && (noteList?.IsKeyboardFocusWithin == true || BooksPanel.IsKeyboardFocusWithin || recordSelectionTools?.IsKeyboardFocusWithin == true);
    private static string? RecordMarker(object? value)=>value switch{string text=>text,System.IO.MemoryStream stream=>System.Text.Encoding.UTF8.GetString(stream.ToArray()).TrimEnd('\0'),byte[] bytes=>System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0'),_=>null};
    private static DataObject RecordDragData(string token){var data=new DataObject();data.SetData(RecordDragFormat,new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(token)),false);return data;}
    private bool HasRecordClipboard()
    { try { return recordClipboard != null && recordClipboardEpoch == VM.WorkspaceEpoch && RecordMarker(Clipboard.GetData(RecordClipboardFormat)) == recordClipboardToken; } catch { return false; } }
    internal async Task CopyRecords(IEnumerable<string> selection, bool cut)
    {
        var ids = selection.Distinct().ToList(); if (ids.Count == 0 || VM.View == "trash") return;
        var epoch = VM.WorkspaceEpoch;
        try {
            if (VM.FlushEditor != null) await VM.FlushEditor();
            if (epoch != VM.WorkspaceEpoch) return;
            var bundle = RecordTransfer.Capture(VM.State!, ids, cut); var token = Clock.Id;
            var data = new DataObject(); data.SetData(RecordClipboardFormat,new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(token)),false); data.SetData(DataFormats.UnicodeText, string.Join(Environment.NewLine, bundle.Records.Select(r => r.Note.Title)));
            Clipboard.SetDataObject(data, true);
            recordClipboard = bundle; recordClipboardToken = token; recordClipboardEpoch = epoch;
            Toast(cut ? $"已剪切 {ids.Count} 条记录，粘贴后移动" : $"已复制 {ids.Count} 条记录");
        } catch (Exception error) { Toast(UserMessages.For(error, "剪贴板暂时不可用，请重试。")); }
    }
    internal async Task PasteRecords(string? destination = null)
    {
        if (recordTransferBusy) return;
        if(!HasRecordClipboard()){try{if(Clipboard.ContainsData(RecordClipboardFormat))Toast("记录剪贴板已失效，请重新复制。");}catch{}return;}
        var target = destination ?? VM.View; var epoch = VM.WorkspaceEpoch; recordTransferBusy = true;
        try {
            if (VM.FlushEditor != null) await VM.FlushEditor();
            if (epoch != VM.WorkspaceEpoch || !HasRecordClipboard()) return;
            var bundle = recordClipboard!; var result = RecordTransfer.Apply(VM.State!, bundle, target);
            if (!ReferenceEquals(result.State, VM.State)) VM.Change(_ => result.State);
            if (bundle.Cut) { recordClipboard = null; if (RecordMarker(Clipboard.GetData(RecordClipboardFormat)) == recordClipboardToken) Clipboard.Clear(); }
            if (result.Ids.Count > 0) Navigate(target, result.Ids[0]);
            noteList?.Focus(); Toast(bundle.Cut ? $"已移动 {result.Ids.Count} 条记录" : $"已粘贴 {result.Ids.Count} 条记录");
        } catch (Exception error) { Toast(UserMessages.For(error, "记录未能粘贴，请重试。")); }
        finally { recordTransferBusy = false; }
    }
    internal async Task MoveRecords(IReadOnlyCollection<string> ids, string destination, long? sourceEpoch = null)
    {
        if (recordTransferBusy) return; var epoch = sourceEpoch ?? VM.WorkspaceEpoch; recordTransferBusy = true;
        try {
            if (VM.FlushEditor != null) await VM.FlushEditor();
            if (epoch != VM.WorkspaceEpoch) return;
            var result = RecordTransfer.Apply(VM.State!, RecordTransfer.Capture(VM.State!, ids, true), destination);
            if (ReferenceEquals(result.State, VM.State)) { Toast("记录已在此记事本中"); return; }
            VM.Change(_ => result.State); Navigate(destination, result.Ids.FirstOrDefault()); noteList?.Focus(); Toast($"已移动 {result.Ids.Count} 条记录");
        } catch (Exception error) { Toast(UserMessages.For(error, "记录未能移动，请重试。")); }
        finally { recordTransferBusy = false; }
    }
    private void AddRecordClipboardActions(ContextMenu menu, IEnumerable<string> selection, string? destination = null)
    {
        var ids = selection.Distinct().ToArray();
        var copy = MenuItem("复制记录", () => _ = CopyRecords(ids, false)); copy.IsEnabled = ids.Length > 0 && VM.View != "trash"; menu.Items.Add(copy);
        var cut = MenuItem("剪切记录", () => _ = CopyRecords(ids, true)); cut.IsEnabled = copy.IsEnabled; menu.Items.Add(cut);
        var paste = MenuItem("粘贴记录", () => _ = PasteRecords(destination)); paste.IsEnabled = HasRecordClipboard() && VM.State!.LiveBooks.Any(b => b.Id == (destination ?? VM.View)); menu.Items.Add(paste);
        menu.Items.Add(new Separator());
    }
    private static ListBoxItem? RecordContainer(DependencyObject? source)
    { for (var p = source; p != null; p = VisualTreeHelper.GetParent(p)) if (p is ListBoxItem item) return item; return null; }
    private void AttachRecordDrag(ListBox list)
    {
        Point origin = default; NoteRow? pressed = null; bool postponeToggle = false;
        list.PreviewMouseLeftButtonDown += (_, e) => {
            origin = e.GetPosition(list); pressed = RecordContainer(e.OriginalSource as DependencyObject)?.DataContext as NoteRow; postponeToggle = false;
            if (pressed != null && selectingRecords && list.SelectedItems.Contains(pressed) && !FromButton(e.OriginalSource as DependencyObject)) { postponeToggle = true; list.Focus(); list.CaptureMouse(); e.Handled = true; }
        };
        list.PreviewMouseLeftButtonUp += (_, e) => {
            if (postponeToggle && !draggingRecords && pressed != null) { list.SelectedItems.Remove(pressed); e.Handled = true; }
            if(Mouse.Captured==list)list.ReleaseMouseCapture();postponeToggle = false; pressed = null;
        };
        list.PreviewMouseMove += (_, e) => {
            if (draggingRecords || VM.View == "trash" || e.LeftButton != MouseButtonState.Pressed || pressed == null || FromButton(e.OriginalSource as DependencyObject)) return;
            var point = e.GetPosition(list); if (Math.Abs(point.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            var ids = list.SelectedItems.Contains(pressed) ? SelectedIds() : [pressed.Note.Id]; if (ids.Count == 0) return;
            try{draggingRecordBundle = RecordTransfer.Capture(VM.State!, ids, true);}catch(RecordTransferException error){Toast(error.Message);pressed=null;return;} draggingRecordEpoch = VM.WorkspaceEpoch; recordDragToken = Clock.Id; draggingRecords = true; postponeToggle = false;if(Mouse.Captured==list)list.ReleaseMouseCapture();
            var canceled = false; bool incompatible = false;
            void Continuing(object sender, QueryContinueDragEventArgs args) { if (args.EscapePressed) canceled = true; }
            void Feedback(object sender, GiveFeedbackEventArgs args) { incompatible = args.Effects == DragDropEffects.None; }
            list.QueryContinueDrag += Continuing; list.GiveFeedback += Feedback;
            var browser=cachedEditor?.Browser;var allowExternalDrop=browser?.AllowExternalDrop??false;if(browser!=null)browser.AllowExternalDrop=false;
            try {
                var result = DragDrop.DoDragDrop(list, RecordDragData(recordDragToken), DragDropEffects.Move);
                if (result == DragDropEffects.None && incompatible && !canceled && lastRecordDropTarget is {} rejected && !lastRecordDropCompatible && rejected.IsLoaded && new Rect(rejected.RenderSize).Contains(Mouse.GetPosition(rejected))) Toast("记事本类型不兼容，未移动任何记录。");
            } catch(System.Runtime.InteropServices.COMException){Toast("拖动未能开始，请重试。");} finally { if(browser!=null){try{browser.AllowExternalDrop=allowExternalDrop;}catch(ObjectDisposedException){}}list.QueryContinueDrag -= Continuing; list.GiveFeedback -= Feedback; draggingRecords = false; draggingRecordBundle = null; pressed = null; lastRecordDropTarget=null;ClearRecordDropOutlines(); }
        };
    }
    private bool IsRecordDrag(IDataObject data) => draggingRecordBundle != null && draggingRecordEpoch == VM.WorkspaceEpoch && RecordMarker(data.GetData(RecordDragFormat)) == recordDragToken;
    private void ClearRecordDropOutlines()
    { foreach (var (button, outline) in recordDropOutlines) outline.Layer?.Remove(outline); recordDropOutlines.Clear(); }
    private void AttachRecordDrop(Button button, Notebook book)
    {
        button.AllowDrop = true;
        button.DragOver += (_, e) => {
            if (!IsRecordDrag(e.Data)) return;
            e.Handled = true; var compatible = draggingRecordBundle!.Records.All(r => r.Mode == book.Mode);lastRecordDropTarget=button;lastRecordDropCompatible=compatible; e.Effects = compatible ? DragDropEffects.Move : DragDropEffects.None;
            if (!recordDropOutlines.ContainsKey(button)) { ClearRecordDropOutlines(); var layer=AdornerLayer.GetAdornerLayer(button);var outline = new RecordDropOutline(button, compatible,layer); recordDropOutlines[button] = outline; layer?.Add(outline); }
        };
        button.DragLeave += (_, _) => ClearRecordDropOutlines();
        button.Drop += (_, e) => {
            if (!IsRecordDrag(e.Data)) return;
            e.Handled = true; var ids = draggingRecordBundle!.Records.Select(r => r.Note.Id).ToArray(); var epoch = draggingRecordEpoch; ClearRecordDropOutlines();
            _ = MoveRecords(ids, book.Id, epoch);
        };
    }
    private sealed class RecordDropOutline : Adorner
    {
        internal bool Compatible {get;}
        internal AdornerLayer? Layer {get;}
        internal RecordDropOutline(UIElement element,bool compatible,AdornerLayer? layer):base(element){Compatible=compatible;Layer=layer;IsHitTestVisible=false;}
        protected override void OnRender(DrawingContext dc) => dc.DrawRoundedRectangle(null, new Pen(Ui.Resource(Compatible ? "Accent" : "Danger"), 2), new Rect(new Point(1, 1), new Size(Math.Max(0, AdornedElement.RenderSize.Width - 2), Math.Max(0, AdornedElement.RenderSize.Height - 2))), 9, 9);
    }
}
