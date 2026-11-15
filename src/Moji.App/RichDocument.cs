using Moji.Core;
namespace Moji.App;
internal static class RichDocument { public static string PlainText(Note note)=>PromptVariables.PlainText(note,note.Variables.ToDictionary(v=>v.Id,v=>v.Name),EditorContent.Plain); }
