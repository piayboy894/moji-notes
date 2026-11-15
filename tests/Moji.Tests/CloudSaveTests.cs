using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Moji.Core;
using Xunit;

namespace Moji.Tests;
public class CloudSaveTests
{
    private static Workspace State() => Seed.Create() with { Sync = new() { Token = "fake-private-token", Code = "ManualSaveCode2026" } };
    [Fact] public void LoadingReplacesEveryProjectWithoutMergingAndKeepsDeviceSettings()
    {
        var local = State() with { Preferences = new() { Theme = "dark", AutoSync = true } };
        var source = Seed.Create(true);
        var loaded = GameSaves.Apply(local, GameSaves.Capture(source));
        Assert.Equal(source.Notebooks.Select(b=>b.Id), loaded.Notebooks.Select(b=>b.Id));
        Assert.Equal(source.Notes.Select(n=>n.Id), loaded.Notes.Select(n=>n.Id));
        Assert.DoesNotContain(loaded.Notes, n=>local.Notes.Any(old=>old.Id==n.Id));
        Assert.Equal(local.Sync, loaded.Sync); Assert.Equal("dark", loaded.Preferences.Theme); Assert.False(loaded.Preferences.AutoSync);
    }
    [Fact] public void FullSaveIncludesTrashArchiveAndHistoryAcrossAllModes()
    {
        var state = State(); var note = state.Notes[0];
        state = state with { Notes = state.Notes.Select(n=>n.Id==note.Id ? n.Apply(n with { DeletedAt = Clock.Now, Archived = true, Content = "saved-content" }, true) : n).ToList() };
        var save = GameSaves.Capture(state); Assert.Equal(state.Notes.Count, save.Notes.Count);
        var recovered = GameSaves.Apply(new(), save); Assert.NotNull(recovered.Notes[0].DeletedAt); Assert.True(recovered.Notes[0].Archived); Assert.NotEmpty(recovered.Notes[0].History);
        Assert.Equal(2, recovered.Notes.Select(n=>n.NotebookId).Distinct().Count());
        var encoded = Json.Serialize(save); Assert.DoesNotContain("fake-private-token", encoded); Assert.DoesNotContain("preferences", encoded);
    }
    [Fact] public void InvalidLoadDoesNotProduceAPartialWorkspace()
    { var state=State(); Assert.Throws<InvalidDataException>(()=>GameSaves.Apply(state,new(1,"bad",[],state.Notes))); Assert.Equal(2,state.Notes.Count); }
    [Fact] public async Task ManualCreateOverwriteReadDeleteAreAtomicAndPreserveOtherSoftware()
    {
        using var git = new MemoryGit(); var service = new CloudSaveService(new HttpClient(git)); var state = State();
        var catalog = await service.ListAsync(state.Sync); Assert.Empty(catalog.Entries); Assert.Equal(0,git.Writes);
        catalog = await service.WriteAsync(state.Sync,catalog,state,"桌面存档"); var first=catalog.Entries.Single();
        var before = git.Head; var changed = state.ChangeNote(state.Notes[0].Id,n=>n with{Content="replacement"},true);
        catalog = await service.WriteAsync(state.Sync,catalog,changed,"覆盖名称",first); Assert.Single(catalog.Entries); Assert.NotEqual(before,git.Head);
        var payload=await service.ReadAsync(state.Sync,catalog,catalog.Entries.Single()); Assert.Equal("replacement",payload.Notes[0].Content); Assert.NotEmpty(payload.Notes[0].History);
        catalog=await service.WriteAsync(state.Sync,catalog,state,"第二份"); Assert.Equal(2,catalog.Entries.Count);
        catalog=await service.ListAsync(state.Sync); Assert.Equal(2,catalog.Entries.Count);
        var removed=catalog.Entries.First(e=>e.Name=="覆盖名称"); catalog=await service.DeleteAsync(state.Sync,catalog,removed);
        Assert.Single(catalog.Entries); Assert.DoesNotContain(CloudSaveService.Root(state.Sync.Code)+removed.Id+".moji",git.Files.Keys);
        Assert.Equal("OTHER_APP_DATA",git.Text("other-app/data.json"));
        Assert.All(git.ChangedPaths,p=>Assert.StartsWith(CloudSaveService.Root(state.Sync.Code),p));
        Assert.All(git.Uploads,text=>{Assert.DoesNotContain("fake-private-token",text);Assert.DoesNotContain("replacement",text);Assert.DoesNotContain("覆盖名称",text);});
        Assert.False(git.Forced);
    }
    [Fact] public async Task AStaleSaveNeverOverridesRemoteChangesOrRetries()
    {
        using var git=new MemoryGit(); var service=new CloudSaveService(new HttpClient(git)); var state=State();var first=await service.ListAsync(state.Sync);
        var latest=await service.WriteAsync(state.Sync,first,state,"first"); var head=git.Head; var writes=git.RefWrites;
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>service.WriteAsync(state.Sync,first,state,"stale"));
        Assert.Contains("刷新",error.Message);Assert.Equal(head,git.Head);Assert.Equal(writes+1,git.RefWrites);Assert.False(git.Forced);
        Assert.Single((await service.ListAsync(state.Sync)).Entries);
    }
    [Fact] public async Task LegacySaveIsOnlyLoadedOrDeletedWhenExplicitlySelected()
    {
        using var git=new MemoryGit();var state=State();using(var key=CryptoService.NewKey(state.Sync.Code))git.Add(CloudSaveService.LegacyPath(state.Sync.Code),Json.Serialize(CryptoService.Encrypt(GameSaves.Capture(state),key)));
        var service=new CloudSaveService(new HttpClient(git));var catalog=await service.ListAsync(state.Sync);var entry=catalog.Entries.Single();Assert.True(entry.Legacy);Assert.Equal(0,git.Writes);
        Assert.Equal(state.Notes.Select(n=>n.Id),(await service.ReadAsync(state.Sync,catalog,entry)).Notes.Select(n=>n.Id));
        catalog=await service.DeleteAsync(state.Sync,catalog,entry);Assert.Empty(catalog.Entries);Assert.DoesNotContain(CloudSaveService.LegacyPath(state.Sync.Code),git.Files.Keys);
    }
    [Fact] public async Task EmptyRepositoryIsInitializedWithoutAnotherApplicationsFiles()
    {using var git=new MemoryGit(true);var state=State();var service=new CloudSaveService(new HttpClient(git));var catalog=await service.ListAsync(state.Sync);Assert.Null(catalog.HeadSha);catalog=await service.WriteAsync(state.Sync,catalog,state,"first");Assert.Single((await service.ListAsync(state.Sync)).Entries);Assert.All(git.Files.Keys,p=>Assert.StartsWith(CloudSaveService.Root(state.Sync.Code),p));}
    [Fact] public async Task ChangedConnectionCannotUseAnOldCatalog()
    {using var git=new MemoryGit();var state=State();var service=new CloudSaveService(new HttpClient(git));var catalog=await service.ListAsync(state.Sync);var count=git.Writes;await Assert.ThrowsAsync<InvalidOperationException>(()=>service.WriteAsync(state.Sync with{Code="OtherSaveCode2026"},catalog,state,"wrong"));Assert.Equal(count,git.Writes);}
    [Fact] public void CorruptCatalogCannotAddressArbitraryPaths()
    {Assert.Throws<InvalidDataException>(()=>GameSaves.Validate(new("moji-saves",1,[new("../../other-app/data.json","bad",1,0,0,new string('a',40),"test")])));}

    private sealed class MemoryGit : HttpMessageHandler
    {
        private int next=10; private readonly Dictionary<string,string> blobs=[];
        private readonly Dictionary<string,Dictionary<string,string>> trees=[];
        private readonly Dictionary<string,(string Tree,string? Parent)> commits=[];
        public string? Head {get;private set;}
        public Dictionary<string,string> Files=>Head==null?[]:trees[commits[Head].Tree];
        public int Writes {get;private set;} public int RefWrites {get;private set;} public bool Forced {get;private set;}
        public List<string> ChangedPaths {get;}=[]; public List<string> Uploads {get;}=[];
        private string Id()=> (next++).ToString("x40");
        public MemoryGit(bool empty=false){if(!empty){Head=Id();var t=Id();trees[t]=[];commits[Head]=(t,null);Add("other-app/data.json","OTHER_APP_DATA");}}
        public void Add(string path,string text){if(Head==null){Head=Id();var tree=Id();trees[tree]=[];commits[Head]=(tree,null);}var sha=Id();blobs[sha]=text;Files[path]=sha;}
        public string Text(string path)=>blobs[Files[path]];
        private static HttpResponseMessage Reply(object value,HttpStatusCode status=HttpStatusCode.OK)=>new(status){Content=new StringContent(Json.Serialize(value))};
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            var path=request.RequestUri!.AbsolutePath.Split("/xuxian-data")[1];var method=request.Method.Method;
            if(method=="GET")
            {
                if(path=="")return Reply(new{default_branch="main"});
                if(path.StartsWith("/git/ref/heads/"))return Head==null?Reply(new{},HttpStatusCode.NotFound):Reply(new{@object=new{sha=Head}});
                if(path.StartsWith("/git/commits/"))return Reply(new{tree=new{sha=commits[path.Split('/')[^1]].Tree}});
                if(path.StartsWith("/git/blobs/")){var text=blobs[path.Split('/')[^1]];return Reply(new{size=Encoding.UTF8.GetByteCount(text),content=Convert.ToBase64String(Encoding.UTF8.GetBytes(text))});}
                if(path.StartsWith("/contents/")){var key=path[10..];var query=System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);var head=query["ref"]??Head;var files=head==null?new Dictionary<string,string>():trees[commits[head].Tree];if(!files.TryGetValue(key,out var sha))return Reply(new{},HttpStatusCode.NotFound);var text=blobs[sha];return Reply(new{sha,size=Encoding.UTF8.GetByteCount(text),content=Convert.ToBase64String(Encoding.UTF8.GetBytes(text))});}
            }
            Writes++;var body=Json.Deserialize<JsonElement>(await request.Content!.ReadAsStringAsync(cancellationToken));
            if(path=="/git/blobs"){var sha=Id();blobs[sha]=Encoding.UTF8.GetString(Convert.FromBase64String(body.GetProperty("content").GetString()!));Uploads.Add(blobs[sha]);return Reply(new{sha},HttpStatusCode.Created);}
            if(path=="/git/trees"){var tree=body.TryGetProperty("base_tree",out var b)?new Dictionary<string,string>(trees[b.GetString()!]):new();foreach(var item in body.GetProperty("tree").EnumerateArray()){var p=item.GetProperty("path").GetString()!;ChangedPaths.Add(p);if(item.GetProperty("sha").ValueKind==JsonValueKind.Null)tree.Remove(p);else tree[p]=item.GetProperty("sha").GetString()!;}var sha=Id();trees[sha]=tree;return Reply(new{sha},HttpStatusCode.Created);}
            if(path=="/git/commits"){var sha=Id();var parents=body.GetProperty("parents").EnumerateArray().ToList();commits[sha]=(body.GetProperty("tree").GetString()!,parents.FirstOrDefault().ValueKind==JsonValueKind.Undefined?null:parents[0].GetString());return Reply(new{sha},HttpStatusCode.Created);}
            if(path.StartsWith("/git/refs/heads/")){RefWrites++;Forced|=body.GetProperty("force").GetBoolean();var sha=body.GetProperty("sha").GetString()!;if(commits[sha].Parent!=Head)return Reply(new{},HttpStatusCode.Conflict);Head=sha;return Reply(new{});}
            if(path.StartsWith("/contents/")&&method=="PUT"){var p=path[10..];if(Files.ContainsKey(p))return Reply(new{},HttpStatusCode.UnprocessableEntity);Add(p,Encoding.UTF8.GetString(Convert.FromBase64String(body.GetProperty("content").GetString()!)));return Reply(new{},HttpStatusCode.Created);}
            throw new InvalidOperationException(method+" "+path);
        }
    }
}
