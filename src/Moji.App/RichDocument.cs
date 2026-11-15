using Moji.Core;
namespace Moji.App;
internal static class RichDocument { public static string PlainText(Note note)=>EditorContent.Plain(PromptVariables.Display(note)); }
