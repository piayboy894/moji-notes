using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Moji.Core;

if(args.Length==2&&args[0]=="fixture")
{
    const string password="Interop-Save-Code-2026";
    var workspace=Seed.Create(true);var first=workspace.Notes[0];workspace=workspace.ChangeNote(first.Id,n=>n with{Content="跨设备全局存档 · café · 文本",Archived=true},true);
    var index=new SaveIndex("moji-saves",1,[new(new string('a',32),"中文存档 · 手机与平板",1794700000000,3,workspace.LiveNotes.Count(),new string('b',40),"Windows")]);
    using var key=CryptoService.NewKey(password);
    var value=new{password,indexEnvelope=CryptoService.Encrypt(index,key),dataEnvelope=CryptoService.Encrypt(GameSaves.Capture(workspace),key),expectedName=index.Entries[0].Name,expectedContent=workspace.Notes[0].Content,expectedNotes=workspace.Notes.Count};
    await File.WriteAllTextAsync(args[1],Json.Serialize(value));Console.WriteLine("Cross-platform encrypted save fixture created.");return;
}
if(args.Length==1&&args[0]=="cloud-smoke")
{
    var token=Environment.GetEnvironmentVariable("MOJI_TEST_TOKEN")??throw new Exception("Missing test authorization");
    var code=Environment.GetEnvironmentVariable("MOJI_TEST_CODE")??throw new Exception("Missing isolated test code");
    var config=new SyncSettings{Repository="piayboy894/xuxian-data",Token=token,Code=code};
    using var http=new HttpClient();http.DefaultRequestHeaders.UserAgent.ParseAdd("Moji-native-cloud-smoke/1.0");http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
    async Task<JsonElement> Get(string path){using var response=await http.GetAsync("https://api.github.com/repos/"+config.Repository+path);response.EnsureSuccessStatusCode();return Json.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());}
    async Task<Dictionary<string,string>> OtherFiles(){var tree=await Get("/git/trees/main?recursive=1");return tree.GetProperty("tree").EnumerateArray().Where(e=>e.GetProperty("type").GetString()=="blob"&&!e.GetProperty("path").GetString()!.StartsWith(CloudSaveService.Root(code))).ToDictionary(e=>e.GetProperty("path").GetString()!,e=>e.GetProperty("sha").GetString()!);}
    var before=await OtherFiles();var service=new CloudSaveService();var catalog=await service.ListAsync(config);if(catalog.Entries.Count!=0)throw new Exception("Test namespace must be empty");
    try
    {
        var workspace=Seed.Create(true) with{Sync=config};catalog=await service.WriteAsync(config,catalog,workspace,"Moji isolated test");var entry=catalog.Entries.Single();
        var loaded=await service.ReadAsync(config,catalog,entry);if(loaded.Notes.Count!=workspace.Notes.Count)throw new Exception("Incomplete global snapshot");
        workspace=workspace.ChangeNote(workspace.Notes[0].Id,n=>n with{Content="ISOLATED_MANUAL_SAVE_TEST"},true);
        catalog=await service.WriteAsync(config,catalog,workspace,"Moji isolated overwrite",entry);
        var refreshed=await service.ListAsync(config);loaded=await service.ReadAsync(config,refreshed,refreshed.Entries.Single());if(loaded.Notes[0].Content!="ISOLATED_MANUAL_SAVE_TEST")throw new Exception("Overwrite failed");
        catalog=await service.DeleteAsync(config,refreshed,refreshed.Entries.Single());if((await service.ListAsync(config)).Entries.Count!=0)throw new Exception("Delete failed");
        var after=await OtherFiles();if(before.Count!=after.Count||before.Any(e=>!after.TryGetValue(e.Key,out var sha)||sha!=e.Value))throw new Exception("Another application's file changed");
        Console.WriteLine("Live private GitHub save: create, read, overwrite, refresh and delete passed; all other files unchanged.");
    }
    finally
    {
        catalog=await service.ListAsync(config);foreach(var entry in catalog.Entries.ToList())catalog=await service.DeleteAsync(config,catalog,entry);
        var path=CloudSaveService.Root(code)+"index.moji";var file=await Get("/contents/"+path);
        using var request=new HttpRequestMessage(HttpMethod.Delete,"https://api.github.com/repos/"+config.Repository+"/contents/"+path){Content=new StringContent(Json.Serialize(new{message="moji: remove isolated test namespace",sha=file.GetProperty("sha").GetString(),branch="main"}),Encoding.UTF8,"application/json")};using var response=await http.SendAsync(request);response.EnsureSuccessStatusCode();
        Console.WriteLine("Isolated test namespace removed.");
    }
    return;
}
throw new ArgumentException("Use fixture <output> or cloud-smoke with isolated test authorization.");
