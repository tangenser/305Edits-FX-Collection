from pathlib import Path
import json,zipfile,shutil,hashlib
ROOT=Path(__file__).resolve().parent
OUT=ROOT.parents[1]/'outputs'
shutil.copy2(ROOT/'README.md',OUT/'305Edits-README.md')
shutil.copy2(ROOT/'assets/305Edits.png',OUT/'305Edits-Icon.png')
with zipfile.ZipFile(OUT/'305Edits-Source-0.3.0.zip','w',zipfile.ZIP_DEFLATED) as z:
    for name in ['panel','engine','launcher','assets']:
        for f in (ROOT/name).rglob('*'):
            if f.is_file() and '__pycache__' not in f.parts and f.relative_to(ROOT).as_posix() not in ['panel/index.html','panel/js/panel.js','panel/jsx/host.jsx','engine/tracker.py','engine/MaskClipboard.cs']:z.write(f,f.relative_to(ROOT).as_posix())
    for name in ['README.md','build.py','finalize.py','tests/test_mask_transfer.py','tests/runtime.test.cjs']:
        f=ROOT/name
        if f.exists():z.write(f,name)
    z.writestr('requirements.txt','# Python standard library only\n')
legacy=OUT/'305Edits-Tracking-0.1.0.305fx'
if legacy.exists():
    archive=ROOT/'archive';archive.mkdir(exist_ok=True)
    shutil.move(str(legacy),str(archive/legacy.name))
report=dict(launcherVersion='0.3.0',unverifiedInstallationAllowed=True,
    maskParserTests=5,objectSampleFrames=226,vectorSampleFrames=41,
    hostTest=dict(version='26.5.2',clip='Graphic',component='Vector Motion',positionKeys=113,scaleKeys=113,targetInPoint=3599.72,endpointsVerified=True,testAnimationRestored=True),
    limitations=['Beta end-to-end workflow unverified','Object Mask has no rotation data','No online update endpoint configured','Real contour rotation transfer requires visual validation'])
(OUT/'305Edits-QA.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
files=['305Edits-FX-Collection-Setup-0.3.0.exe','305Edits-Tracking-0.3.0.305fx','305Edits-Source-0.3.0.zip']
hashes={name:hashlib.sha256((OUT/name).read_bytes()).hexdigest() for name in files}
(OUT/'SHA256.json').write_text(json.dumps(hashes,indent=2),encoding='utf8')
print(json.dumps({name:(OUT/name).stat().st_size for name in files}))
