using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Moji.Core;

namespace Moji.App;
public partial class MainWindow
{
    private void RenderDashboard()
    {
        editorRequest++;var previous=sharedEditor;sharedEditor=null;if(previous!=null)editorRelease=Release(previous);
        var state = VM.State!;
        var content = new StackPanel { Margin = new Thickness(45, 43, 45, 38) };
        var title = new DockPanel { Margin = new Thickness(0, 0, 0, 30) };
        title.Children.Add(Ui.Text($"我的记事本   {state.LiveBooks.Count()}", 27, null, true)); content.Children.Add(title);
        var cards = new WrapPanel { Margin = new Thickness(-10,0,-10,0) };
        foreach (var book in state.LiveBooks)
        {
            var face = new Grid { Width = 210, Height = 250, Margin = new Thickness(0, 0, 0, 14) };
            face.Children.Add(new NotebookCover { Mode = book.Mode, CoverColor=book.Color,CoverColorEnd=book.ColorEnd, Dark = state.Preferences.Theme == "dark", IsHitTestVisible = false, Effect = new DropShadowEffect { BlurRadius = 19, ShadowDepth = 10, Opacity = .2, Color = Color.FromRgb(47, 61, 40), Direction = 270 } });
            var contents = new StackPanel { Margin = new Thickness(28,28,42,25), IsHitTestVisible = false };
            contents.Children.Add(new NativeIcon { Kind = NativeIcon.ForNotebook(book), Width = 27, Height = 27, Foreground = Ui.Brush(NotebookAppearance.Ink(book.Color)), HorizontalAlignment = HorizontalAlignment.Left });
            var name = Ui.Text(book.Name, 21, "#283025", true); name.Margin = new Thickness(0,22,0,8); name.MaxHeight = 60; name.TextTrimming = TextTrimming.CharacterEllipsis; contents.Children.Add(name);
            contents.Children.Add(Ui.Text($"{state.LiveNotes.Count(n => n.NotebookId == book.Id && !n.Archived):D2} 条记录", 12, "#53604C"));
            contents.Children.Add(new NativeIcon { Kind = "Arrow", Width = 20, Height = 20, Foreground = Ui.Brush("#57654F"), Margin = new Thickness(0,14,0,0), HorizontalAlignment = HorizontalAlignment.Left }); face.Children.Add(contents);
            var button = Ui.Button("", () => Navigate(book.Id)); button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); button.Padding = new Thickness(0); button.Width=210;button.Height=264;button.Margin = new Thickness(10,0,10,12); button.Content = new Viewbox { Child = face, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
            var menu = new ContextMenu(); menu.Items.Add(MenuItem("编辑记事本", () => NotebookDialog(book))); menu.Items.Add(MenuItem("删除记事本", () => DeleteNotebook(book))); button.ContextMenu = menu; cards.Children.Add(button);
        }
        var addFace=new Grid{Width=210,Height=250};
        addFace.Children.Add(new System.Windows.Shapes.Rectangle { Stroke=Ui.Resource("Sub"),StrokeThickness=1.4,StrokeDashArray=new DoubleCollection{4,4},RadiusX=14,RadiusY=14,Margin=new Thickness(3,1,5,25) });
        addFace.Children.Add(new NativeIcon{Kind="Plus",Width=32,Height=32,Foreground=Ui.Resource("Accent"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,0,24)});
        var add=Ui.Button("",()=>NotebookDialog(),tip:"新建记事本");add.Name="AddNotebookCard";add.Content=addFace;add.Width=210;add.Height=264;add.Padding=new Thickness(0);add.Margin=new Thickness(10,0,10,12);add.Background=Brushes.Transparent;add.BorderThickness=new Thickness(0);System.Windows.Automation.AutomationProperties.SetName(add,"新建记事本");cards.Children.Add(add);
        content.Children.Add(cards);
        content.Children.Add(new Separator { Margin = new Thickness(0, 33, 0, 28) });
        var recentHeading = new DockPanel { Margin = new Thickness(0, 0, 0, 19) };
        var all = Ui.Button("查看全部  ›", () => Navigate("all")); all.Background = Brushes.Transparent; all.BorderThickness = new Thickness(0); all.FontSize = 13; DockPanel.SetDock(all, Dock.Right); recentHeading.Children.Add(all); recentHeading.Children.Add(Ui.Text("最近记录", 23, null, true)); content.Children.Add(recentHeading);
        var recentRows = new StackPanel(); var recentNotes = state.LiveNotes.Where(n => !n.Archived).OrderByDescending(n => n.UpdatedAt).Take(6).ToList();
        foreach (var note in recentNotes)
        {
            var book = state.Notebooks.First(b => b.Id == note.NotebookId);
            var row = new Grid(); row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(135) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(95) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(30) });
            var nameCell = new Grid(); nameCell.ColumnDefinitions.Add(new() { Width = new GridLength(32) }); nameCell.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            nameCell.Children.Add(new NativeIcon { Kind = "Document", Width = 19, Height = 21, Foreground = Ui.Resource("Sub"), HorizontalAlignment = HorizontalAlignment.Left });
            var nameText = Ui.Text(note.Title, 14); nameText.TextWrapping = TextWrapping.NoWrap; nameText.TextTrimming = TextTrimming.CharacterEllipsis; nameText.Margin = new Thickness(0,0,12,0); Grid.SetColumn(nameText, 1); nameCell.Children.Add(nameText); row.Children.Add(nameCell);
            var badge = new Border { Child = Ui.Text(book.Name, 11, book.Mode == NoteMode.Prompt ? "#8F7752" : book.Mode == NoteMode.Password ? "#667F90" : "#647D54"), Background = Ui.Brush(book.Mode == NoteMode.Prompt ? "#F2ECE2" : book.Mode == NoteMode.Password ? "#E9EEF2" : "#EAF0E3"), CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 5, 10, 5), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(badge, 1); row.Children.Add(badge);
            var at = Ui.Text(RecordTools.Relative(note.UpdatedAt), 11); at.Foreground = Ui.Resource("Sub"); at.HorizontalAlignment = HorizontalAlignment.Right; at.Margin = new Thickness(0, 0, 15, 0); Grid.SetColumn(at, 2); row.Children.Add(at);
            var dots = new Button { Content = new NativeIcon { Kind = "More", Width = 19, Height = 19, Foreground = Ui.Resource("Sub") }, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(4), ToolTip = "更多操作" }; dots.Click += (_, e) => { e.Handled = true; dots.ContextMenu = NoteMenu(note.Id); dots.ContextMenu.PlacementTarget = dots; dots.ContextMenu.IsOpen = true; }; Grid.SetColumn(dots, 3); row.Children.Add(dots);
            var click = Ui.Button("", () => Navigate(note.NotebookId, note.Id)); click.Content = row; click.HorizontalContentAlignment = HorizontalAlignment.Stretch; click.Background = Brushes.Transparent; click.BorderThickness = new Thickness(0); click.Padding = new Thickness(20, 16, 16, 16); click.ContextMenu = NoteMenu(note.Id); recentRows.Children.Add(click);
            if (note != recentNotes.Last()) recentRows.Children.Add(new Border { Height = 1, Background = Ui.Resource("Line") });
        }
        if (recentNotes.Count == 0) { var empty = Ui.Text("暂无记录", 13); empty.Foreground = Ui.Resource("Sub"); recentRows.Children.Add(Ui.Spaced(empty, new Thickness(20, 23, 20, 23))); }
        var recent = Ui.Card(recentRows, null, 12); recent.Background = Ui.Resource("Side"); content.Children.Add(recent);
        Body.Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }
}
