using Moji.Core;
namespace Moji.Tests;
internal static class TestWorkspace
{
    public static Workspace Create() { var w = Seed.Create(); return w with { Notes = w.Notebooks.Select(b => NoteCreation.InNotebook(w, b.Id) with { Content = "test-content", Title = "test-" + b.Mode }).ToList() }; }
}
