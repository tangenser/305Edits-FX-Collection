'use strict';
// One resolver for the panel and tests. The installer records the exact shared engine path.
module.exports=function resolveRuntime(fs,path,localAppData,extensionRoot){
 var candidates=[],config=path.join(extensionRoot,'305runtime.json');
 try{if(fs.existsSync(config)){var value=JSON.parse(fs.readFileSync(config,'utf8').replace(/^\uFEFF/,''));if(value.python&&value.engine&&path.isAbsolute(value.python)&&path.isAbsolute(value.engine))candidates.push(value);}}catch(ignored){}
 [path.join(localAppData,'Programs','305Edits FX Collection','Data','runtimes','tracking'),path.join(localAppData,'305Edits','runtimes','tracking'),path.join(localAppData,'305Edits','runtime')].forEach(function(root){candidates.push({python:path.join(root,'python.exe'),engine:path.join(root,fs.existsSync(path.join(root,'mask_transfer.py'))?'mask_transfer.py':'tracker.py')});});
 for(var i=0;i<candidates.length;i++)if(fs.existsSync(candidates[i].python)&&fs.existsSync(candidates[i].engine))return candidates[i];
 throw Error('Не найден движок Tracking. Откройте 305Edits FX Collection и обновите плагин. Проверен путь: '+candidates[0].python);
};
