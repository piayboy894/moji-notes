using Moji.Core;
namespace Moji.Tests;
public class SharedEditorTests {
 [Xunit.Fact] public void SharedHtmlIsSearchableWithoutMarkup(){var content=EditorContent.Prefix+"<h2>标题</h2><p><strong>正文</strong> {{原样}}</p><img src='data:image/png;base64,AAAA' alt='图片'>";Xunit.Assert.Equal("标题\n正文 {{原样}}\n图片",EditorContent.Plain(content));Xunit.Assert.DoesNotContain("base64",RecordTools.Plain(content));}
 [Xunit.Fact] public void ExplicitVariablesFillOnceWithoutInterpretingUserInput(){var v=new PromptVariable{Name="受众"};var note=new Note{Content=EditorContent.Prefix+$"<p>{{{{原样}}}} <span data-moji-variable=\"{v.Id}\" data-name=\"受众\">受众</span></p>",Variables=[v]};Xunit.Assert.Single(PromptVariables.Used(note));var value="<script>& {{原样}}";Xunit.Assert.Equal("{{原样}} "+value,PromptVariables.PlainText(note,new Dictionary<string,string>{{v.Id,value}},EditorContent.Plain));Xunit.Assert.Empty(PromptVariables.Used(note with{Content="{{受众}}"}));}
 [Xunit.Fact] public void PreexistingTextRemainsLiteral(){Xunit.Assert.Equal("# Heading\n- [x] task",EditorContent.Plain("# Heading\n- [x] task"));}
}

public class PromptFieldTests {
 [Xunit.Fact] public void FilledFieldsCopyTheirContentsAndKeepNamesAsMetadata(){var v=new PromptVariable{Name="受众"};var value="程序员 <script> {{原样}}";var note=new Note{Content=EditorContent.Prefix+$"<p>给 <span data-moji-variable=\"{v.Id}\" data-name=\"受众\" data-moji-field=\"true\">{System.Net.WebUtility.HtmlEncode(value)}</span> 写作</p>",Variables=[v]};Xunit.Assert.Equal("给 "+value+" 写作",EditorContent.Plain(PromptVariables.Display(note)));Xunit.Assert.Single(PromptVariables.Used(note));Xunit.Assert.Equal(note.Content,Json.Deserialize<Note>(Json.Serialize(note)).Content);}
 [Xunit.Fact] public void NewFieldFormatAndPreviouslySavedEditorDocumentsBothRemainReadable(){Xunit.Assert.Equal("<!--moji:tiptap:2-->",EditorContent.Prefix);Xunit.Assert.True(EditorContent.IsDocument("<!--moji:tiptap:1--><p>以前的正文</p>"));Xunit.Assert.Equal("以前的正文",EditorContent.Plain("<!--moji:tiptap:1--><p>以前的正文</p>"));}
}
