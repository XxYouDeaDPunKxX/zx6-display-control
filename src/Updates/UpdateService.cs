using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZX6DisplayControl {
 public sealed class ReleaseVersion:IComparable<ReleaseVersion> {
  private Version core;private string[] preview;
  public string Text {get;private set;}
  public static ReleaseVersion Parse(string value) {
   if(value==null || value.Length>100)throw new FormatException("Invalid release version.");
   var match=Regex.Match(value,@"^v?((0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*))(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z");
   Version core;if(!match.Success || !Version.TryParse(match.Groups[1].Value,out core))throw new FormatException("Invalid release version.");
   string[] preview=match.Groups[5].Success?match.Groups[5].Value.Split('.'):new string[0];
   if(preview.Any(p=>Numeric(p) && p.Length>1 && p[0]=='0'))throw new FormatException("Invalid numeric prerelease identifier.");
   return new ReleaseVersion{core=core,preview=preview,Text=value[0]=='v'?value.Substring(1):value};
  }
  private static bool Numeric(string value) {return value.All(c=>c>='0' && c<='9');}
  public int CompareTo(ReleaseVersion other) {
   if(other==null)return 1;int compare=core.CompareTo(other.core);if(compare!=0)return compare;
   if(preview.Length==0 || other.preview.Length==0)return preview.Length==other.preview.Length?0:preview.Length==0?1:-1;
   for(int i=0;i<Math.Min(preview.Length,other.preview.Length);i++) {
    string a=preview[i],b=other.preview[i];bool an=Numeric(a),bn=Numeric(b);
    compare=an && bn?(a.Length==b.Length?string.CompareOrdinal(a,b):a.Length.CompareTo(b.Length)):an!=bn?(an?-1:1):string.CompareOrdinal(a,b);
    if(compare!=0)return compare;
   }
   return preview.Length.CompareTo(other.preview.Length);
  }
 }
 public sealed class UpdateResult {
  public string AvailableVersion {get;set;}
  public Uri ReleaseUrl {get;set;}
  public string Message {get;set;}
  internal string ReleaseTag {get;set;}
  internal bool Success {get;set;}
 }
 public sealed class UpdateService {
  public const string RepositoryUrl="https://github.com/XxYouDeaDPunKxX/zx6-display-control";
  public const string ApiUrl="https://api.github.com/repos/XxYouDeaDPunKxX/zx6-display-control/releases?per_page=100";
  public static string CurrentVersion {get{return typeof(UpdateService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;}}
  private readonly string cachePath;private readonly Func<CancellationToken,Task<byte[]>> fetch;private readonly Func<DateTime> now;
  private readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
  public UpdateService(string directory,Func<CancellationToken,Task<byte[]>> fetch=null,Func<DateTime> now=null) {
   cachePath=Path.Combine(Path.GetFullPath(directory),"update-check.json");this.fetch=fetch??FetchReleases;this.now=now??(()=>DateTime.UtcNow);
  }
  public Task<UpdateResult> CheckAsync(string current,bool automatic,CancellationToken token) {
   return Task.Run(()=>CheckCore(current,automatic,token),token);
  }
  private async Task<UpdateResult> CheckCore(string current,bool automatic,CancellationToken token) {
   await gate.WaitAsync(token).ConfigureAwait(false);
   try {
    var installed=ReleaseVersion.Parse(current);var stamp=now().ToUniversalTime();var cache=ReadCache();
    if(automatic && cache!=null && stamp.Ticks-cache.AttemptUtcTicks<TimeSpan.FromDays(1).Ticks) {
     if(!cache.Succeeded)return Failure("Could not check during the last attempt. Use Check for updates to try again.");
     return FromTag(cache.Tag,installed);
    }
    // Record the attempt before any request, including failed or canceled checks.
    cache=new UpdateCache{AttemptUtcTicks=stamp.Ticks};
    if(!SaveCache(cache) && automatic)return Failure("Automatic check skipped: the last-check time could not be saved. Use Check for updates.");
    token.ThrowIfCancellationRequested();UpdateResult result;
    try {result=SelectRelease(await fetch(token).ConfigureAwait(false),current);}
    catch(OperationCanceledException) {if(token.IsCancellationRequested)throw;result=Failure("Could not check for updates: GitHub did not respond in time.");}
    catch(UpdateCheckException e) {result=Failure(e.Message);}
    catch(Exception e) {
     if(!(e is IOException || e is HttpRequestException || e is SerializationException || e is System.Xml.XmlException || e is FormatException || e is UnauthorizedAccessException))throw;
     result=Failure("Could not check for updates. Check your connection and try again.");
    }
    token.ThrowIfCancellationRequested();cache.Succeeded=result.Success;cache.Tag=result.ReleaseTag;SaveCache(cache);return result;
   }finally{gate.Release();}
  }
  public static UpdateResult SelectRelease(byte[] json,string current) {
   if(json==null || json.Length==0 || json.Length>1048576)throw new InvalidDataException("Invalid release response size.");
   ReleaseEntry[] entries;using(var stream=new MemoryStream(json))entries=(ReleaseEntry[])new DataContractJsonSerializer(typeof(ReleaseEntry[])).ReadObject(stream);
   if(entries==null)throw new InvalidDataException("Invalid release response.");
   string newest=null;ReleaseVersion latest=null;
   foreach(var entry in entries) {
    if(entry==null || entry.Draft || entry.Assets==null || !entry.Assets.Any(a=>a!=null && a.Name=="ZX6DisplayControl.zip" && a.State=="uploaded"))continue;
    ReleaseVersion version;try{version=ReleaseVersion.Parse(entry.Tag);}catch(FormatException){continue;}
    if(latest==null || version.CompareTo(latest)>0){latest=version;newest=entry.Tag;}
   }
   return FromTag(newest,ReleaseVersion.Parse(current));
  }
  private static UpdateResult FromTag(string tag,ReleaseVersion current) {
   if(tag==null)return new UpdateResult{Success=true,Message="No published app release was found on GitHub."};
   ReleaseVersion version;try{version=ReleaseVersion.Parse(tag);}catch(FormatException){return Failure("Saved update information is invalid. Check again manually.");}
   bool newer=version.CompareTo(current)>0;
   return new UpdateResult{Success=true,ReleaseTag=tag,AvailableVersion=newer?version.Text:null,ReleaseUrl=newer?new Uri(RepositoryUrl+"/releases/tag/"+Uri.EscapeDataString(tag)):null,Message=newer?"Version "+version.Text+" is available on GitHub.":"No newer version is available on GitHub."};
  }
  private static UpdateResult Failure(string message) {return new UpdateResult{Message=message};}
  private UpdateCache ReadCache() {
   try {
    if(!File.Exists(cachePath))return null;
    using(var stream=File.OpenRead(cachePath)) {
     if(stream.Length>16384)return null;var cache=(UpdateCache)new DataContractJsonSerializer(typeof(UpdateCache)).ReadObject(stream);
     return cache!=null && cache.AttemptUtcTicks>0 && cache.AttemptUtcTicks<=DateTime.MaxValue.Ticks?cache:null;
    }
   }catch(Exception e){if(!(e is IOException || e is UnauthorizedAccessException || e is SerializationException || e is System.Xml.XmlException))throw;return null;}
  }
  private bool SaveCache(UpdateCache value) {
   string temporary=cachePath+"."+Guid.NewGuid().ToString("N")+".tmp";
   try {
    Directory.CreateDirectory(Path.GetDirectoryName(cachePath));using(var stream=File.Create(temporary))new DataContractJsonSerializer(typeof(UpdateCache)).WriteObject(stream,value);
    if(File.Exists(cachePath))File.Replace(temporary,cachePath,null);else File.Move(temporary,cachePath);return true;
   }catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
   finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
  }
  private static async Task<byte[]> FetchReleases(CancellationToken token) {
   using(var handler=new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false,AutomaticDecompression=DecompressionMethods.GZip | DecompressionMethods.Deflate})
   using(var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=1048576}) {
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ZX6-Display-Control/"+CurrentVersion);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");client.DefaultRequestHeaders.Add("X-GitHub-Api-Version","2022-11-28");
    using(var response=await client.GetAsync(ApiUrl,token).ConfigureAwait(false)) {
     if((int)response.StatusCode==403 || (int)response.StatusCode==429)throw new UpdateCheckException("Could not check: GitHub limited or refused the request. Try again later.");
     if(!response.IsSuccessStatusCode)throw new UpdateCheckException("Could not check for updates. GitHub returned HTTP "+(int)response.StatusCode+".");
     return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }
   }
  }
  private sealed class UpdateCheckException:Exception {public UpdateCheckException(string message):base(message){}}
  [DataContract] private sealed class UpdateCache {
   [DataMember] public long AttemptUtcTicks;
   [DataMember] public bool Succeeded;
   [DataMember] public string Tag;
  }
  [DataContract] private sealed class ReleaseEntry {
   [DataMember(Name="tag_name",IsRequired=true)] public string Tag {get;set;}
   [DataMember(Name="draft",IsRequired=true)] public bool Draft {get;set;}
   [DataMember(Name="assets",IsRequired=true)] public ReleaseAsset[] Assets {get;set;}
  }
  [DataContract] private sealed class ReleaseAsset {
   [DataMember(Name="name")] public string Name {get;set;}
   [DataMember(Name="state")] public string State {get;set;}
  }
 }
}
