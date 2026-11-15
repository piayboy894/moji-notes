using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Moji.Core;
namespace Moji.App;
public partial class MainWindow
{
    private readonly Dictionary<string,List<string>> richUndo=[];
    private readonly Dictionary<string,List<string>> richRedo=[];
    private StackPanel? tableTools;
    private TableCell? ActiveCell()
    { DependencyObject? node=textEditor?.CaretPosition.Parent; while(node!=null){if(node is TableCell cell)return cell;node=node is FrameworkContentElement content?content.Parent:null;}return null; }
    private void RichUndo(bool redo=false)
    {
        var note=CurrentNote();if(note==null||textEditor==null)return;var source=redo?richRedo:richUndo;var target=redo?richUndo:richRedo;
        if(!source.TryGetValue(note.Id,out var values)||values.Count==0)return;var content=values[^1];values.RemoveAt(values.Count-1);
        if(!target.ContainsKey(note.Id))target[note.Id]=[];target[note.Id].Add(note.Content);
        loadingEditor=true;textEditor.Document=MarkdownDocument.Render(content,VM.State!.Preferences.FontSize,note.Variables);loadingEditor=false;VM.EditNote(note.Id,n=>n with{Content=content});WireTasks();UpdateEditorStatus();textEditor.Focus();
    }
    private void WireTasks()
    {
        if(textEditor==null)return;
        foreach(var check in TaskBoxes(textEditor.Document.Blocks)){check.Checked+=(_,_)=>SaveRichText();check.Unchecked+=(_,_)=>SaveRichText();}
    }
    private static IEnumerable<CheckBox> TaskBoxes(BlockCollection blocks)
    {
        foreach(var block in blocks){if(block is Paragraph p)foreach(var inline in p.Inlines)if(inline is InlineUIContainer{Child:CheckBox check})yield return check;
            if(block is Section section)foreach(var c in TaskBoxes(section.Blocks))yield return c;
            if(block is System.Windows.Documents.List list)foreach(var item in list.ListItems)foreach(var c in TaskBoxes(item.Blocks))yield return c;
            if(block is Table table)foreach(var cell in table.RowGroups.SelectMany(g=>g.Rows).SelectMany(r=>r.Cells))foreach(var c in TaskBoxes(cell.Blocks))yield return c;}
    }
    private void InsertTask()
    {
        if(textEditor==null)return;textEditor.BeginChange();try{
            var check=new CheckBox{IsChecked=false,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};check.Checked+=(_,_)=>SaveRichText();check.Unchecked+=(_,_)=>SaveRichText();
            var paragraph=new Paragraph(new InlineUIContainer(check));paragraph.Inlines.Add(new Run(""));paragraph.Tag="task";
            var current=textEditor.CaretPosition.Paragraph;if(current?.Parent==textEditor.Document)textEditor.Document.Blocks.InsertAfter(current,paragraph);else textEditor.Document.Blocks.Add(paragraph);
            textEditor.CaretPosition=paragraph.ContentEnd;textEditor.Focus();
        }finally{textEditor.EndChange();}SaveRichText();
    }
    private void InsertQuote()
    {
        if(textEditor?.CaretPosition.Paragraph is not {} paragraph)return;textEditor.BeginChange();try{
            if(paragraph.Parent is Section { Tag:"quote" } section){var blocks=section.Blocks.ToList();foreach(var block in blocks){section.Blocks.Remove(block);textEditor.Document.Blocks.InsertBefore(section,block);}textEditor.Document.Blocks.Remove(section);}
            else if(paragraph.Parent==textEditor.Document){var section2=new Section{Tag="quote",BorderBrush=Ui.Resource("Accent"),BorderThickness=new Thickness(3,0,0,0),Padding=new Thickness(14,5,0,5)};textEditor.Document.Blocks.InsertBefore(paragraph,section2);textEditor.Document.Blocks.Remove(paragraph);section2.Blocks.Add(paragraph);}
        }finally{textEditor.EndChange();}SaveRichText();
    }
    private void UpdateRichSelection(){if(tableTools!=null)tableTools.Visibility=ActiveCell()!=null?Visibility.Visible:Visibility.Collapsed;}
    private StackPanel BuildTableTools()
    {
        var panel=Ui.Row();panel.Margin=new Thickness(18,5,18,5);panel.Visibility=Visibility.Collapsed;
        foreach(var(label,action)in new[]{("添加行","row+"),("添加列","col+"),("删除行","row-"),("删除列","col-"),("删除表格","table-")}){var button=Ui.Small(label,()=>EditTable(action));button.Focusable=false;panel.Children.Add(button);}return panel;
    }
    private static TableCell NewCell()=>new(new Paragraph()){BorderBrush=Ui.Resource("Line"),BorderThickness=new Thickness(.5),Padding=new Thickness(10)};
    private void EditTable(string action)
    {
        if(textEditor==null||ActiveCell() is not {} cell||cell.Parent is not TableRow row||row.Parent is not TableRowGroup group||group.Parent is not Table table)return;
        if(action=="table-"&&!Confirm("删除表格？","表格内容会一并删除，可撤销。"))return;
        textEditor.BeginChange();try{
            var at=group.Rows.IndexOf(row);var col=row.Cells.IndexOf(cell);
            if(action=="row+"){var next=new TableRow();for(var i=0;i<row.Cells.Count;i++)next.Cells.Add(NewCell());group.Rows.Insert(at+1,next);textEditor.CaretPosition=next.Cells[0].ContentStart;}
            else if(action=="col+"){foreach(var r in group.Rows)r.Cells.Insert(Math.Min(col+1,r.Cells.Count),NewCell());}
            else if(action=="row-"&&group.Rows.Count>1){var next=group.Rows[at==0?1:at-1];textEditor.CaretPosition=next.Cells[0].ContentStart;group.Rows.Remove(row);}
            else if(action=="col-"&&row.Cells.Count>1){var next=row.Cells[col==0?1:col-1];textEditor.CaretPosition=next.ContentStart;foreach(var r in group.Rows)if(r.Cells.Count>col)r.Cells.RemoveAt(col);}
            else if(action=="table-"||action=="row-"||action=="col-"){var p=new Paragraph();textEditor.Document.Blocks.InsertAfter(table,p);textEditor.CaretPosition=p.ContentStart;textEditor.Document.Blocks.Remove(table);}
        }finally{textEditor.EndChange();}SaveRichText();UpdateRichSelection();
    }
}
