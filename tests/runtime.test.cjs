const assert=require('node:assert/strict');
const path=require('node:path').win32;
const resolve=require('../panel/js/runtime.js');
const local='C:\\Users\\Editor\\AppData\\Local',extension='C:\\Users\\Editor\\AppData\\Roaming\\Adobe\\CEP\\extensions\\tracking';
function fake(files){return {existsSync:p=>Object.hasOwn(files,p),readFileSync:p=>files[p]};}
const shared=path.join(local,'Programs','305Edits FX Collection','Data','runtimes','tracking');
const files={[path.join(shared,'python.exe')]:'exe',[path.join(shared,'tracker.py')]:'python'};
// Reproduce a launcher-visible AppData runtime that Premiere cannot see.
assert.equal(resolve(fake(files),path,local,extension).python,path.join(shared,'python.exe'));
const configured={python:'D:\\305Edits\\engine\\python.exe',engine:'D:\\305Edits\\engine\\tracker.py'};
const custom={...files,[path.join(extension,'305runtime.json')]:JSON.stringify(configured),[configured.python]:'exe',[configured.engine]:'py'};
assert.deepEqual(resolve(fake(custom),path,local,extension),configured);
// A stale configured path must fall back to the shared installation.
delete custom[configured.python];assert.equal(resolve(fake(custom),path,local,extension).python,path.join(shared,'python.exe'));
assert.throws(()=>resolve(fake({}),path,local,extension),/Не найден движок/);
console.log('PASS: 4 runtime visibility and relocation checks');
