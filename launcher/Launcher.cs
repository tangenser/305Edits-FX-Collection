using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using Microsoft.Win32;
using System.Windows.Media.Imaging;

[assembly:AssemblyVersion("0.3.0.0")]
[assembly:AssemblyFileVersion("0.3.0.0")]
[assembly:AssemblyProduct("305Edits FX Collection")]
[assembly:AssemblyCompany("305Edits")]

namespace Edits305 {
 public class Product { public string id {get;set;} public string name {get;set;} public string description {get;set;} public string version {get;set;} public string panel {get;set;} public string runtime {get;set;} public string minVersion {get;set;} public string maxVersion {get;set;} public string[] testedVersions {get;set;} }
 public class Catalog { public int schema {get;set;} public string release {get;set;} public Product[] products {get;set;} public Dictionary<string,string> files {get;set;} }
 public class Host { public string Path; public Version Version; public int Major { get {return Version.Major;} } public bool Beta {get{return System.IO.Path.GetFileNameWithoutExtension(Path).IndexOf("Beta",StringComparison.OrdinalIgnoreCase)>=0;}} public string Label {get {return "Premiere Pro " + Version + (Beta?" Beta":"");}} }
 public class Receipt {public string version {get;set;} public string hostPath {get;set;} public string product {get;set;} }
 public class ReleaseAsset {public string name {get;set;} public string browser_download_url {get;set;} public long size {get;set;} }
 public class ReleaseInfo {public string tag_name {get;set;} public ReleaseAsset[] assets {get;set;} }
 public class Launcher {
  // LocalAppData\Programs is shared with ordinary desktop apps even when launched by an MSIX app.
  static string Root=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","305Edits FX Collection","Data");
  static string CepRoot=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Adobe","CEP","extensions");
  static JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=32*1024*1024 };
  Window window; Catalog catalog; string packageRoot; List<Host> hosts=new List<Host>(); bool working;
  const string Repository="tangenser/305Edits-FX-Collection";
  T UI<T>(string name) where T:class { return window.FindName(name) as T; }
  static string Exe {get{return Assembly.GetExecutingAssembly().Location;}}
  [STAThread] public static void Main(string[] args) {
   if(args.Contains("--self-test")){ SelfTest();return; }
   try {new Launcher().Run();} catch(Exception e){MessageBox.Show(e.Message,"305Edits FX Collection",MessageBoxButton.OK,MessageBoxImage.Error);}
  }
  void Run(){
   var app=new Application();using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml"))window=(Window)XamlReader.Load(stream);
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("305Edits.png")){var icon=new BitmapImage();icon.BeginInit();icon.CacheOption=BitmapCacheOption.OnLoad;icon.StreamSource=stream;icon.EndInit();window.Icon=icon;}
   UI<Button>("RefreshButton").Click+=delegate{if(!working)Refresh();};
   UI<Button>("PackageButton").Click+=async delegate{await ImportPackage();};
   UI<Button>("OnlineButton").Click+=async delegate{await CheckOnline();};
   UI<Button>("LocateButton").Click+=delegate{Locate();};
   window.Loaded+=async delegate { await Prepare(); if(catalog!=null)await CheckOnline(); };
   window.Closing+=delegate(object s,System.ComponentModel.CancelEventArgs e){if(working){e.Cancel=true;Status("Дождитесь окончания операции");}};
   app.Run(window);
  }
  void Status(string text){UI<TextBlock>("StatusLabel").Text=text;}
  void SetBusy(bool value){working=value;UI<Button>("OnlineButton").IsEnabled=!value;UI<Button>("PackageButton").IsEnabled=!value;UI<Button>("RefreshButton").IsEnabled=!value;UI<Button>("LocateButton").IsEnabled=!value;UI<StackPanel>("Cards").IsEnabled=!value;}
  static WebClient GitHubClient(){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var client=new WebClient();client.Headers[HttpRequestHeader.UserAgent]="305Edits-FX-Collection/0.3.0";client.Headers[HttpRequestHeader.Accept]="application/vnd.github+json";client.Encoding=Encoding.UTF8;return client;}
  static string AssetUrl(ReleaseAsset asset){var uri=new Uri(asset.browser_download_url);if(uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/"+Repository+"/releases/download/",StringComparison.Ordinal))throw new Exception("Неверный адрес пакета GitHub");return uri.AbsoluteUri;}
  async Task CheckOnline(){if(working)return;SetBusy(true);Status("Проверка GitHub Releases…");try{
   string nextRoot=null;Catalog next=null;bool downloaded=false;
   await Task.Run(delegate{using(var client=GitHubClient()){
    var release=Json.Deserialize<ReleaseInfo>(client.DownloadString("https://api.github.com/repos/"+Repository+"/releases/latest"));Version version;
    if(release==null||!Version.TryParse((release.tag_name??"").TrimStart('v'),out version)||release.assets==null)throw new Exception("Некорректный выпуск GitHub");
    if(version<new Version(catalog.release))return;
    var asset=release.assets.SingleOrDefault(a=>a.name=="305Edits-Tracking-"+version+".305fx");var hashes=release.assets.SingleOrDefault(a=>a.name=="SHA256.json");
    if(asset==null||hashes==null||asset.size<=0||asset.size>200L*1024*1024||hashes.size>1024*1024)throw new Exception("В выпуске нет корректного пакета обновления");
    string cache=System.IO.Path.Combine(Root,"packages","github-"+version),marker=System.IO.Path.Combine(cache,"verified.txt");
    if(File.Exists(marker)){next=ReadCatalog(cache);nextRoot=cache;return;}
    var expected=Json.Deserialize<Dictionary<string,string>>(client.DownloadString(AssetUrl(hashes)));string hash;
    if(!expected.TryGetValue(asset.name,out hash)||!Regex.IsMatch(hash,"^[0-9a-fA-F]{64}$"))throw new Exception("Нет SHA256 пакета");
    string stage=cache+".stage-"+Guid.NewGuid().ToString("N"),file=stage+".305fx";Directory.CreateDirectory(stage);
    try{window.Dispatcher.Invoke(new Action(delegate{Status("Скачивание Tracking "+version+" с GitHub…");}));
     client.DownloadFile(AssetUrl(asset),file);
     using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))if(!String.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""),hash,StringComparison.OrdinalIgnoreCase))throw new Exception("SHA256 скачанного пакета не совпадает");
     using(var stream=File.OpenRead(file))Extract(stream,stage);Verify(stage);next=ReadCatalog(stage);
     if(next.release!=version.ToString())throw new Exception("Версия пакета не совпадает с выпуском");
     if(Directory.Exists(cache))Directory.Delete(cache,true);Directory.Move(stage,cache);File.WriteAllText(marker,hash);nextRoot=cache;downloaded=true;
    }finally{if(File.Exists(file))File.Delete(file);if(Directory.Exists(stage))Directory.Delete(stage,true);}
   }});
   if(next!=null){catalog=next;packageRoot=nextRoot;File.WriteAllText(System.IO.Path.Combine(Root,"active-package.txt"),nextRoot);Refresh();}
   Status(downloaded?"Пакет скачан. Выберите Tracking и версии Premiere для установки.":"Каталог GitHub актуален. Выберите плагин для установки.");
  }catch(WebException){Status("GitHub недоступен. Доступны локальный каталог и установка пакета .305fx.");}catch(Exception e){Status("Ошибка обновления: "+e.Message);}finally{SetBusy(false);}}
  async Task Prepare(){SetBusy(true);Status("Подготовка локального каталога…");try{await Task.Run(delegate {
   Directory.CreateDirectory(Root);packageRoot=System.IO.Path.Combine(Root,"packages","0.3.0");
   var marker=System.IO.Path.Combine(packageRoot,"verified.txt");
   if(!File.Exists(marker)){
    string temp=packageRoot+".stage-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(temp);
    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("collection.zip"))Extract(stream,temp);
    Verify(temp);if(Directory.Exists(packageRoot))Directory.Delete(packageRoot,true);Directory.Move(temp,packageRoot);File.WriteAllText(marker,"ok");
   }
   var active=System.IO.Path.Combine(Root,"active-package.txt");if(File.Exists(active)){string candidate=File.ReadAllText(active).Trim();if(IsWithin(candidate,System.IO.Path.Combine(Root,"packages"))&&File.Exists(System.IO.Path.Combine(candidate,"catalog.json"))&&new Version(ReadCatalog(candidate).release)>=new Version("0.3.0"))packageRoot=candidate;}
   catalog=ReadCatalog(packageRoot);
  });Refresh();Status("Готов к работе. Выберите плагин для установки.");}catch(Exception e){Status("Ошибка подготовки: "+e.Message);}finally{SetBusy(false);}}
  static Catalog ReadCatalog(string root){var c=Json.Deserialize<Catalog>(File.ReadAllText(System.IO.Path.Combine(root,"catalog.json"),Encoding.UTF8));
   if(c==null||c.schema!=1||c.products==null||c.products.Length==0||c.files==null)throw new Exception("Неизвестный формат пакета");
   Version v;if(!Version.TryParse(c.release,out v))throw new Exception("Некорректная версия пакета");
   var ids=new HashSet<string>();foreach(var p in c.products){if(!Regex.IsMatch(p.id??"","^[a-z][a-z0-9-]{1,48}$")||!ids.Add(p.id)||!Version.TryParse(p.version,out v)||!Version.TryParse(p.minVersion,out v)||(!String.IsNullOrEmpty(p.maxVersion)&&!Version.TryParse(p.maxVersion,out v)))throw new Exception("Некорректный продукт в каталоге");SafePath(root,p.panel);SafePath(root,p.runtime);}
   return c;
  }
  static bool IsWithin(string path,string root){return System.IO.Path.GetFullPath(path).StartsWith(System.IO.Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);}
  static string SafePath(string root,string entry){if(String.IsNullOrWhiteSpace(entry)||System.IO.Path.IsPathRooted(entry)||entry.Contains(":"))throw new Exception("Недопустимый путь в пакете");string result=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,entry));if(!IsWithin(result,root))throw new Exception("Путь вне пакета");return result;}
  static void Extract(Stream input,string root){using(var archive=new ZipArchive(input,ZipArchiveMode.Read)){long total=0;foreach(var entry in archive.Entries){if(entry.FullName.EndsWith("/"))continue;total+=entry.Length;if(total>900L*1024*1024)throw new Exception("Пакет слишком большой");var dest=SafePath(root,entry.FullName);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));using(var ins=entry.Open())using(var outs=File.Create(dest))ins.CopyTo(outs);}}}
  static void Verify(string root){var c=ReadCatalog(root);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var pair in c.files){var f=SafePath(root,pair.Key);if(!File.Exists(f))throw new Exception("В пакете отсутствует "+pair.Key);using(var sha=SHA256.Create())using(var stream=File.OpenRead(f)){var hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(!String.Equals(hash,pair.Value,StringComparison.OrdinalIgnoreCase))throw new Exception("Нарушена целостность: "+pair.Key);}seen.Add(System.IO.Path.GetFullPath(f));}
   foreach(var f in Directory.GetFiles(root,"*",SearchOption.AllDirectories)){string rel=f.Substring(root.Length+1);if(rel!="catalog.json"&&!seen.Contains(System.IO.Path.GetFullPath(f)))throw new Exception("Файл отсутствует в манифесте: "+rel);}
   foreach(var p in c.products){if(!File.Exists(System.IO.Path.Combine(SafePath(root,p.panel),"CSXS","manifest.xml")))throw new Exception("Нет манифеста панели");if(!File.Exists(System.IO.Path.Combine(SafePath(root,p.runtime),"python.exe")))throw new Exception("Нет runtime");}
  }
  static List<Host> Detect(){var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})try{using(var root=RegistryKey.OpenBaseKey(hive,view)){
    using(var ap=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths")){if(ap!=null)foreach(var name in ap.GetSubKeyNames())if(name.IndexOf("Premiere",StringComparison.OrdinalIgnoreCase)>=0)using(var key=ap.OpenSubKey(name)){string v=key.GetValue(null) as string;if(!String.IsNullOrWhiteSpace(v))paths.Add(v.Trim('"'));}}
    using(var u=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")){if(u!=null)foreach(var key in u.GetSubKeyNames())using(var sub=u.OpenSubKey(key)){string name=sub.GetValue("DisplayName") as string,loc=sub.GetValue("InstallLocation") as string,icon=sub.GetValue("DisplayIcon") as string;if(name!=null&&name.IndexOf("Premiere",StringComparison.OrdinalIgnoreCase)>=0){if(!String.IsNullOrEmpty(loc))AddDirectory(paths,loc.Trim('"'));if(!String.IsNullOrEmpty(icon)){var match=Regex.Match(icon,"^\\\"?(.*?\\.exe)",RegexOptions.IgnoreCase);if(match.Success)paths.Add(match.Groups[1].Value);}}}}
    using(var adobe=root.OpenSubKey(@"SOFTWARE\Adobe\Premiere Pro")){if(adobe!=null)foreach(var name in adobe.GetSubKeyNames())using(var key=adobe.OpenSubKey(name))foreach(var field in key.GetValueNames()){string v=key.GetValue(field) as string;if(!String.IsNullOrWhiteSpace(v)&&Directory.Exists(v))AddDirectory(paths,v);}}
   }}catch{}
   foreach(var baseDir in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}){var adobe=System.IO.Path.Combine(baseDir,"Adobe");if(Directory.Exists(adobe))foreach(var dir in Directory.GetDirectories(adobe,"*Premiere*"))AddDirectory(paths,dir);}
   foreach(var menu in new[]{Environment.GetFolderPath(Environment.SpecialFolder.Programs),Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)})try{if(Directory.Exists(menu)){dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));foreach(var file in Directory.GetFiles(menu,"*Premiere*.lnk",SearchOption.AllDirectories)){dynamic link=shell.CreateShortcut(file);string target=link.TargetPath;if(!String.IsNullOrWhiteSpace(target))paths.Add(target);}}}catch{}
   var custom=System.IO.Path.Combine(Root,"premiere-paths.txt");if(File.Exists(custom))foreach(var path in File.ReadAllLines(custom))if(!String.IsNullOrWhiteSpace(path))paths.Add(path.Trim());
   var result=new List<Host>();foreach(var p in paths)if(File.Exists(p)&&Regex.IsMatch(System.IO.Path.GetFileName(p),@"^Adobe Premiere Pro( \(Beta\))?\.exe$",RegexOptions.IgnoreCase)){var info=FileVersionInfo.GetVersionInfo(p);Version version;var match=Regex.Match(info.ProductVersion??"",@"\d+\.\d+(\.\d+)?(\.\d+)?");if(match.Success&&Version.TryParse(match.Value,out version))result.Add(new Host{Path=System.IO.Path.GetFullPath(p),Version=version});}
   return result.OrderByDescending(h=>h.Version).ToList();
  }
  static void AddDirectory(HashSet<string> paths,string dir){if(Directory.Exists(dir))try{foreach(var file in Directory.GetFiles(dir,"Adobe Premiere Pro*.exe",SearchOption.TopDirectoryOnly))paths.Add(file);}catch{}}
  void Locate(){var dlg=new OpenFileDialog{Title="Укажите Premiere Pro",Filter="Adobe Premiere Pro|Adobe Premiere Pro*.exe"};if(dlg.ShowDialog(window)!=true)return;if(!Regex.IsMatch(System.IO.Path.GetFileName(dlg.FileName),@"^Adobe Premiere Pro( \(Beta\))?\.exe$",RegexOptions.IgnoreCase)){Status("Выберите исполняемый файл Premiere Pro");return;}File.AppendAllText(System.IO.Path.Combine(Root,"premiere-paths.txt"),dlg.FileName+Environment.NewLine);Refresh();}
  // Unverified hosts remain installable by explicit product requirement.
  bool Supported(Product p,Host h){return true;}
  string InstallDir(Product p,Host h){return System.IO.Path.Combine(CepRoot,"com.305edits."+p.id+".ppro"+h.Major);}
  Receipt Installed(Product p,Host h){try{var dir=InstallDir(p,h);if(!File.Exists(System.IO.Path.Combine(dir,"CSXS","manifest.xml"))||!File.Exists(System.IO.Path.Combine(Root,"runtimes",p.id,"python.exe")))return null;var r=Json.Deserialize<Receipt>(File.ReadAllText(System.IO.Path.Combine(dir,"305receipt.json")));Version v;return r!=null&&r.product==p.id&&Version.TryParse(r.version,out v)?r:null;}catch{return null;}}
  static string InstallStatus(int total,int installed,int current){return total>0&&current==total?"Установлен":installed>0&&current<installed?"Доступно обновление":installed>0?"Установлен в "+installed+" из "+total:"Не установлен";}
  void Refresh(){if(catalog==null)return;hosts=Detect();UI<TextBlock>("HostsLabel").Text=hosts.Count==0?"Premiere Pro не обнаружен":String.Join("    /    ",hosts.Select(h=>h.Label).ToArray());UI<TextBlock>("BuildLabel").Text="305Edits · "+catalog.release;
   var cards=UI<StackPanel>("Cards");cards.Children.Clear();foreach(var product in catalog.products){Product p=product;var compatible=hosts.Where(h=>Supported(p,h)).ToList();int installed=compatible.Count(h=>Installed(p,h)!=null),current=compatible.Count(h=>{var r=Installed(p,h);return r!=null&&new Version(r.version)>=new Version(p.version);});
    string label=InstallStatus(compatible.Count,installed,current);
    var border=new Border {CornerRadius=new CornerRadius(12),BorderThickness=new Thickness(1),BorderBrush=Brush("#2B5484"),Background=new LinearGradientBrush(Color.FromRgb(19,40,69),Color.FromRgb(12,19,33),35),Padding=new Thickness(26),Margin=new Thickness(0,0,0,14)};
    var stack=new StackPanel();border.Child=stack;
    stack.Children.Add(new TextBlock {Text="✦    MOTION TOOLS",FontSize=10,Foreground=Brush("#60ADFF"),Margin=new Thickness(0,0,0,22)});
    stack.Children.Add(new TextBlock {Text=p.name,FontSize=27,FontWeight=FontWeights.SemiBold});
    stack.Children.Add(new TextBlock {Text=p.description,TextWrapping=TextWrapping.Wrap,Foreground=Brush("#A0B4D1"),Margin=new Thickness(0,12,0,24),LineHeight=23});
    stack.Children.Add(new TextBlock {Text="v"+p.version+"  ·  "+label,Foreground=Brush(current>0?"#75C5FF":"#A3B2CC"),Margin=new Thickness(0,0,0,18)});
    var row=new StackPanel {Orientation=Orientation.Horizontal};var install=new Button {Content=label=="Доступно обновление"?"Обновить":compatible.Count>0&&current==compatible.Count?"Управлять":"Установить",Background=Brush("#1D74EE")};install.Click+=async delegate{await InstallProduct(p);};row.Children.Add(install);
    if(installed>0){var remove=new Button {Content="Удалить…",Margin=new Thickness(10,0,0,0)};remove.Click+=delegate{Remove(p);};row.Children.Add(remove);}stack.Children.Add(row);cards.Children.Add(border);
   }
  }
  static SolidColorBrush Brush(string color){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));}
  List<Host> Choose(Product p,string title){var compatible=hosts.Where(h=>Supported(p,h)).ToList();if(hosts.Count==0){MessageBox.Show("У вас не обнаружен установленный Premiere Pro.","305Edits",MessageBoxButton.OK,MessageBoxImage.Information);return null;}if(compatible.Count==0){MessageBox.Show("Premiere обнаружен, но "+p.name+" требует версию от "+p.minVersion+(String.IsNullOrEmpty(p.maxVersion)?"":" до "+p.maxVersion)+". Обнаружение версий работает независимо от совместимости плагина.","305Edits");return null;}
   var dlg=new Window {Title=title,Owner=window,Width=540,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Background=Brush("#101C30"),Foreground=Brush("#EEF4FF"),FontFamily=new FontFamily("Segoe UI")};dlg.Resources.Add(typeof(Button),window.FindResource(typeof(Button)));
   var panel=new StackPanel {Margin=new Thickness(28)};dlg.Content=panel;panel.Children.Add(new TextBlock{Text="Выберите версии Premiere Pro",FontSize=21,Margin=new Thickness(0,0,0,18)});var boxes=new List<CheckBox>();foreach(var h in hosts){var r=Installed(p,h);bool supported=Supported(p,h),tested=p.testedVersions!=null&&p.testedVersions.Contains(h.Version.ToString());var cb=new CheckBox{Content=h.Label+(!supported?"  /  несовместима":!tested?"  /  не проверена":"")+(r==null?"":"  /  установлен "+r.version),IsChecked=supported,IsEnabled=supported,Tag=h,Foreground=Brush("#DBE9FF"),Margin=new Thickness(0,10,0,10)};boxes.Add(cb);panel.Children.Add(cb);}panel.Children.Add(new TextBlock{Text="Закройте выбранные версии Premiere перед установкой.\nИзменение состава панелей требует перезапуска Premiere.",Foreground=Brush("#91A8CA"),Margin=new Thickness(0,18,0,20),TextWrapping=TextWrapping.Wrap});
   var go=new Button{Content="Продолжить",Background=Brush("#1D74EE"),Foreground=Brush("#FFFFFF"),Padding=new Thickness(16,10,16,10)};go.Click+=delegate{if(!boxes.Any(b=>b.IsChecked==true)){MessageBox.Show("Выберите хотя бы одну версию");return;}dlg.DialogResult=true;};panel.Children.Add(go);return dlg.ShowDialog()==true?boxes.Where(b=>b.IsChecked==true).Select(b=>(Host)b.Tag).ToList():null;
  }
  static void CopyTree(string source,string dest){Directory.CreateDirectory(dest);foreach(var f in Directory.GetFiles(source))File.Copy(f,System.IO.Path.Combine(dest,System.IO.Path.GetFileName(f)),true);foreach(var d in Directory.GetDirectories(source))CopyTree(d,System.IO.Path.Combine(dest,System.IO.Path.GetFileName(d)));}
  async Task InstallProduct(Product p){var selected=Choose(p,"Установка "+p.name);if(selected==null)return;SetBusy(true);Status("Установка "+p.name+"…");try{await Task.Run(delegate{
   var running=Process.GetProcesses().Where(pr=>pr.ProcessName.StartsWith("Adobe Premiere Pro",StringComparison.OrdinalIgnoreCase));foreach(var process in running)try{if(selected.Any(h=>String.Equals(h.Path,process.MainModule.FileName,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Закройте выбранную версию Premiere и повторите установку");}catch(System.ComponentModel.Win32Exception){throw new InvalidOperationException("Не удалось проверить запущенный Premiere. Закройте Premiere перед установкой");}
   // Verify original immutable package again, excluding our marker.
   foreach(var pair in catalog.files){var file=SafePath(packageRoot,pair.Key);using(var sha=SHA256.Create())using(var st=File.OpenRead(file)){if(BitConverter.ToString(sha.ComputeHash(st)).Replace("-","").ToLowerInvariant()!=pair.Value.ToLowerInvariant())throw new Exception("Повреждён пакет: "+pair.Key);}}
   Directory.CreateDirectory(System.IO.Path.Combine(Root,"runtimes"));string rt=System.IO.Path.Combine(Root,"runtimes",p.id),stage=rt+".stage-"+Guid.NewGuid().ToString("N"),backup=rt+".backup-"+Guid.NewGuid().ToString("N");CopyTree(SafePath(packageRoot,p.runtime),stage);
   var replacements=new List<string[]>();bool movedRuntime=false;
   try{
    if(Directory.Exists(rt))Directory.Move(rt,backup);Directory.Move(stage,rt);movedRuntime=true;
    foreach(var h in selected.GroupBy(h=>h.Major).Select(g=>g.First())){
     string dest=InstallDir(p,h),tmp=dest+".stage-"+Guid.NewGuid().ToString("N"),old=dest+".backup-"+Guid.NewGuid().ToString("N");CopyTree(SafePath(packageRoot,p.panel),tmp);
     string manifest=System.IO.Path.Combine(tmp,"CSXS","manifest.xml"),xml=File.ReadAllText(manifest);xml=xml.Replace("com.305edits.tracking","com.305edits."+p.id+".ppro"+h.Major).Replace("[26.5,26.999]","["+h.Major+".0,"+h.Major+".999]");xml=xml.Replace("Name=\"CSXS\" Version=\"12.0\"","Name=\"CSXS\" Version=\""+(h.Major<13?"8.0":h.Major<15?"9.0":"10.0")+"\"");File.WriteAllText(manifest,xml,new UTF8Encoding(false));
     File.WriteAllText(System.IO.Path.Combine(tmp,"305receipt.json"),Json.Serialize(new Receipt{version=p.version,hostPath=h.Path,product=p.id}));
     File.WriteAllText(System.IO.Path.Combine(tmp,"305runtime.json"),Json.Serialize(new{python=System.IO.Path.Combine(rt,"python.exe"),engine=System.IO.Path.Combine(rt,"mask_transfer.py")}));
     if(Directory.Exists(dest))Directory.Move(dest,old);try{Directory.Move(tmp,dest);}catch{if(Directory.Exists(old))Directory.Move(old,dest);throw;}replacements.Add(new[]{dest,old});
    }
    foreach(var csxs in new[]{8,9,10,11,12,13})using(var reg=Registry.CurrentUser.CreateSubKey(@"Software\Adobe\CSXS."+csxs))reg.SetValue("PlayerDebugMode","1",RegistryValueKind.String);
   }catch{foreach(var r in replacements.AsEnumerable().Reverse()){if(Directory.Exists(r[0]))Directory.Delete(r[0],true);if(Directory.Exists(r[1]))Directory.Move(r[1],r[0]);}if(movedRuntime&&Directory.Exists(rt))Directory.Delete(rt,true);if(Directory.Exists(backup))Directory.Move(backup,rt);throw;}
   foreach(var r in replacements)if(Directory.Exists(r[1]))Directory.Delete(r[1],true);if(Directory.Exists(backup))Directory.Delete(backup,true);
  });Refresh();Status("Установлено. Откройте Premiere → Окно → Расширения → "+p.name+". Для локальной CEP-панели включён PlayerDebugMode текущего пользователя.");}catch(Exception e){Status("Установка не завершена: "+e.Message);}finally{SetBusy(false);}}
  void Remove(Product p){var selected=Choose(p,"Удаление "+p.name);if(selected==null)return;if(MessageBox.Show("Удалить "+p.name+" для выбранных версий? Сохранённые треки и проекты останутся на месте.","305Edits",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;try{if(Process.GetProcessesByName("Adobe Premiere Pro").Length>0)throw new Exception("Закройте Premiere перед удалением панели");foreach(var h in selected){string d=InstallDir(p,h);if(IsWithin(d,CepRoot)&&File.Exists(System.IO.Path.Combine(d,"305receipt.json")))Directory.Delete(d,true);}Refresh();Status("Плагин удалён для выбранных версий");}catch(Exception e){Status(e.Message);}}
  async Task ImportPackage(){var dlg=new OpenFileDialog {Title="Пакет обновления 305Edits",Filter="305Edits FX package|*.305fx"};if(dlg.ShowDialog(window)!=true)return;SetBusy(true);Status("Проверка пакета…");string stage=System.IO.Path.Combine(Root,"packages","import-"+Guid.NewGuid().ToString("N"));try{Catalog newer=null;await Task.Run(delegate{Directory.CreateDirectory(stage);using(var stream=File.OpenRead(dlg.FileName))Extract(stream,stage);Verify(stage);newer=ReadCatalog(stage);if(new Version(newer.release)<new Version(catalog.release))throw new Exception("Понижение версии каталога не поддерживается");File.WriteAllText(System.IO.Path.Combine(Root,"active-package.txt"),stage);});catalog=newer;packageRoot=stage;Refresh();Status("Каталог обновлён. Нажмите «Обновить» на карточке нужного плагина.");}catch(Exception e){Status("Пакет отклонён: "+e.Message);if(Directory.Exists(stage))Directory.Delete(stage,true);}finally{SetBusy(false);}}
  static void SelfTest(){var test=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"305edits-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(test);try{bool rejected=false;try{SafePath(test,"../escape");}catch{rejected=true;}if(!rejected)throw new Exception("Traversal validation failed");if(InstallStatus(2,0,0)!="Не установлен"||InstallStatus(2,1,1)!="Установлен в 1 из 2"||InstallStatus(2,2,2)!="Установлен"||InstallStatus(2,2,1)!="Доступно обновление")throw new Exception("Status validation failed");using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("collection.zip"))Extract(stream,test);var c=ReadCatalog(test);Verify(test);var found=Detect();var launcher=new Launcher();if(!launcher.Supported(c.products[0],new Host{Path="Adobe Premiere Pro.exe",Version=new Version("14.0")}))throw new Exception("Unverified host was blocked");File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Exe),"self-test.json"),Json.Serialize(new{ok=true,release=c.release,products=c.products.Length,statusCases=4,unverifiedAllowed=true,hosts=found.Select(h=>new{path=h.Path,version=h.Version.ToString()}).ToArray()}));}finally{Directory.Delete(test,true);}}
 }
}
