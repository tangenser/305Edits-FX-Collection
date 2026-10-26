from pathlib import Path
import json,hashlib,zipfile,subprocess,shutil
ROOT=Path(__file__).resolve().parent
OUT=ROOT.parents[1]/'outputs'
OUT.mkdir(exist_ok=True)
shutil.copy2(ROOT/'engine/mask_transfer.py',ROOT/'runtime/mask_transfer.py')
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
subprocess.run([str(csc),'/nologo','/target:exe','/platform:x64','/reference:System.Web.Extensions.dll',f'/out:{ROOT/"runtime/MaskClipboard.exe"}',str(ROOT/'engine/MaskClipboardReader.cs')],check=True)
catalog=dict(schema=1,release='0.3.0',products=[dict(id='tracking',name='305Edits Tracking',description='Перенос анимации маски Premiere на видеоклип, текст и графику.',version='0.3.0',panel='panel',runtime='runtime',minVersion='26.5',maxVersion=None,testedVersions=['26.5.2'])],files={})
files=[]
for folder in ['panel','runtime']:
    for file in sorted((ROOT/folder).rglob('*')):
        if not file.is_file() or '__pycache__' in file.parts or file.suffix=='.pyc':continue
        rel=file.relative_to(ROOT).as_posix()
        if rel.startswith('runtime/Lib/') or rel in ['runtime/tracker.py','panel/index.html','panel/js/panel.js','panel/jsx/host.jsx']:continue
        rel=file.relative_to(ROOT).as_posix();catalog['files'][rel]=hashlib.sha256(file.read_bytes()).hexdigest();files.append((file,rel))
package=ROOT/'collection.zip'
with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    z.writestr('catalog.json',json.dumps(catalog,ensure_ascii=False,indent=2))
    for file,rel in files:z.write(file,rel)
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
wpf=csc.parent/'WPF'
exe=OUT/'305Edits FX Collection.exe'
args=[str(csc),'/nologo','/target:winexe','/platform:x64','/optimize+',f'/out:{exe}',f'/win32icon:{ROOT/"assets/305Edits.ico"}',f'/resource:{ROOT/"assets/window-icon.png"},305Edits.png',f'/resource:{package},collection.zip',f'/resource:{ROOT/"launcher/MainWindow.xaml"},MainWindow.xaml']
for r in ['System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','Microsoft.CSharp.dll']:
    args.append('/reference:'+str(csc.parent/r))
for r in ['PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll']:args.append('/reference:'+str(wpf/r))
args.append(str(ROOT/'launcher/Launcher.cs'))
subprocess.run(args,check=True)
shutil.copy2(package,OUT/'305Edits-Tracking-0.3.0.305fx')
print(json.dumps(dict(exe=str(exe),packageBytes=package.stat().st_size,unpackedBytes=sum(f.stat().st_size for f,_ in files),files=len(files))))
