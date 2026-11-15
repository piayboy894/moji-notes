using System.Net;
using System.Text;
using System.Security.Cryptography;
using Moji.Core;
using Xunit;

namespace Moji.Tests;
public class CryptoTests
{
    [Fact] public void RoundTripAndWrongPassword()
    {var value=new{Password="secret-example-4817",Token="fake-test-token"};using var key=CryptoService.NewKey("Master-Password-2026");var encrypted=CryptoService.Encrypt(value,key);Assert.DoesNotContain(value.Password,Json.Serialize(encrypted));var clear=CryptoService.Decrypt<Dictionary<string,string>>(encrypted,key);Assert.Equal(value.Password,clear["password"]);using var wrong=CryptoService.Derive("wrong",encrypted.Salt,encrypted.Iterations);Assert.Throws<InvalidDataException>(()=>CryptoService.Decrypt<Workspace>(encrypted,wrong));}
    [Fact] public void RandomNonceAndSalt(){using var key=CryptoService.NewKey("password");using var other=CryptoService.NewKey("password");var one=CryptoService.Encrypt(new{Value="same"},key);var two=CryptoService.Encrypt(new{Value="same"},key);Assert.NotEqual(one.Iv,two.Iv);Assert.NotEqual(one.Data,two.Data);Assert.NotEqual(key.Salt,other.Salt);}
    [Fact] public void TamperFailsAuthentication(){using var key=CryptoService.NewKey("password");var e=CryptoService.Encrypt(TestWorkspace.Create(),key);var data=Convert.FromBase64String(e.Data);data[0]^=1;Assert.Throws<InvalidDataException>(()=>CryptoService.Decrypt<Workspace>(e with{Data=Convert.ToBase64String(data)},key));}
    [Fact] public void RejectsExcessiveKdf(){Assert.Throws<InvalidDataException>(()=>CryptoService.Derive("pw",Convert.ToBase64String(new byte[16]),int.MaxValue));}
    [Fact] public void PasswordsMeetComposition(){for(var i=0;i<60;i++){var pw=CryptoService.GeneratePassword(32);Assert.Equal(32,pw.Length);Assert.Contains(pw,char.IsLower);Assert.Contains(pw,char.IsUpper);Assert.Contains(pw,char.IsDigit);Assert.Contains(pw,c=>!char.IsLetterOrDigit(c));Assert.All(CryptoService.GeneratePassword(24,false),c=>Assert.True(char.IsAsciiLetterOrDigit(c)));}}
    [Fact] public void KeyIsClearedOnDispose(){var key=CryptoService.NewKey("password");key.Dispose();Assert.Throws<InvalidDataException>(()=>CryptoService.Decrypt<Workspace>(CryptoService.Encrypt(TestWorkspace.Create(),CryptoService.NewKey("different")),key));}
}
public class RecordVersionTests
{
    [Fact]public void HistoryCapsAndRecovers(){var note=new Note{NotebookId="book",Content="0"};for(var i=1;i<50;i++)note=note.Apply(note with{Content=i.ToString()},true);Assert.Equal(40,note.History.Count);Assert.Equal("48",note.History[0].Content);}
}
public class FeatureTests
{
    [Fact]public void TotpMatchesRfcVector(){Assert.Equal("287082",RecordTools.Totp("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",59000));}
    [Fact]public void SearchDoesNotLeakSecret(){var note=new Note{Title="服务",Username="user@example.com",Content="private-note",Password="secret-password",Otp="otp-secret"};Assert.True(RecordTools.Matches(note,"user@example",NoteMode.Password));Assert.False(RecordTools.Matches(note,"secret-password",NoteMode.Password));Assert.False(RecordTools.Matches(note,"private-note",NoteMode.Password));}
    [Fact]public void ValidationRejectsOrphans(){var v=TestWorkspace.Create();v.Validate();Assert.Throws<InvalidDataException>(()=>(v with{Notes=[v.Notes[0] with{NotebookId="absent"}]}).Validate());}
    [Fact]public async Task AtomicPlainStoreRetainsBackup(){var path=Path.Combine(Path.GetTempPath(),"moji-unit-"+Clock.Id);try{var store=new VaultStore(path);using var key=CryptoService.NewKey("Master-Password-2026");var workspace=TestWorkspace.Create();await store.SaveAsync(workspace);await store.SaveAsync(workspace.ChangeNote(workspace.Notes[0].Id,n=>n with{Content="new-private-value"}));Assert.True(File.Exists(store.VaultPath+".bak"));Assert.Contains("new-private-value",await File.ReadAllTextAsync(store.VaultPath));var read=await store.ReadAsync();Assert.NotNull(read);Assert.Equal("new-private-value",read.Notes[0].Content);await File.WriteAllTextAsync(store.VaultPath,"corrupted");Assert.NotNull(await store.ReadAsync());Assert.True(store.RecoveredBackup);}finally{Directory.Delete(path,true);}}
}
public class UpdateTests
{
    [Fact]public async Task PublicUpdateChecksNeverUseToken()
    {var handler=new FakeHandler(r=>{Assert.Null(r.Headers.Authorization);return new(HttpStatusCode.OK){Content=new StringContent("[]")};});Assert.Null(await new UpdateService(new HttpClient(handler)).CheckAsync(new(1,0,0),"x64"));}
    [Fact]public async Task InvalidDownloadHashNeverInstalls()
    {var asset=new UpdateAsset(1,"Moji-Notes-2.0.0-win-x64.exe","https://github.com/piayboy894/moji-notes/releases/download/v2.0.0/app.exe",4);var hashes=new UpdateAsset(2,"SHA256SUMS.json","https://github.com/piayboy894/moji-notes/releases/download/v2.0.0/SHA256SUMS.json",100);var handler=new FakeHandler(r=>new(HttpStatusCode.OK){Content=new StringContent(r.RequestUri!.AbsolutePath.EndsWith("json")?Json.Serialize(new Dictionary<string,string>{{asset.Name,new string('0',64)}}):"TEST")});var directory=Path.Combine(Path.GetTempPath(),"moji-update-test-"+Clock.Id);try{await Assert.ThrowsAsync<InvalidDataException>(()=>new UpdateService(new HttpClient(handler)).DownloadAsync(new(new(2,0,0),asset,hashes,"https://github.com/piayboy894/moji-notes/releases"),directory));Assert.False(File.Exists(Path.Combine(directory,"Moji.Notes.download.exe")));}finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}}
    private sealed class FakeHandler(Func<HttpRequestMessage,HttpResponseMessage> handle):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(handle(request));}
}
