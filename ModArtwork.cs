// [소스 안내] Modrinth 대표 이미지 다운로드·캐시입니다. 이미지 크기와 출처를 제한하고 표시용 크기로 변환합니다.
using System.Net.Http;
namespace NeoForgeServerManager;

public sealed class ModArtwork : IDisposable
{
    readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(8)};
    readonly Dictionary<string,Image> images=new();
    readonly SemaphoreSlim slots=new(5);
    readonly CancellationTokenSource stop=new();
    public Image? Find(string id)=>images.GetValueOrDefault(id);
    public async Task Load(ModPluginService.Result item,Action refresh)
    {
        if(images.ContainsKey(item.Id)||!Uri.TryCreate(item.IconUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="cdn.modrinth.com")return;
        string cached=Path.Combine(Storage.Root,"extension-artwork",Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(uri.AbsoluteUri)))+".png");
        bool entered=false;
        try{
            if(File.Exists(cached)){try{using var saved=Image.FromFile(cached);images[item.Id]=new Bitmap(saved);refresh();return;}catch(ArgumentException){}}
            await slots.WaitAsync(stop.Token);entered=true;
            using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,stop.Token);response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength>4*1024*1024)return;
            using var input=await response.Content.ReadAsStreamAsync(stop.Token);using var bytes=new MemoryStream();
            var buffer=new byte[8192];int n;
            while((n=await input.ReadAsync(buffer,stop.Token))>0){if(bytes.Length+n>4*1024*1024)return;bytes.Write(buffer,0,n);}
            bytes.Position=0;using var decoded=SkiaSharp.SKBitmap.Decode(bytes);
            if(decoded==null)return;
            using var png=decoded.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);
            using var pngStream=png.AsStream();using var original=Image.FromStream(pngStream);
            if(stop.IsCancellationRequested)return;
            var thumbnail=new Bitmap(96,96);using(var g=Graphics.FromImage(thumbnail)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(original,Artwork.Fit(original,new Rectangle(0,0,96,96)));}
            if(images.Remove(item.Id,out var old))old.Dispose();images[item.Id]=thumbnail;try{Directory.CreateDirectory(Path.GetDirectoryName(cached)!);thumbnail.Save(cached,System.Drawing.Imaging.ImageFormat.Png);}catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException){}refresh();
        }catch(Exception e) when(e is HttpRequestException or IOException or OperationCanceledException or ArgumentException or System.Runtime.InteropServices.ExternalException){ }
        finally{if(entered)slots.Release();}
    }
    public void Dispose(){stop.Cancel();http.Dispose();foreach(var image in images.Values)image.Dispose();images.Clear();}
}
