/* 305Edits Mask Transfer: graphics and media use the same selected TrackItem interface. */
var E305Mask={};
E305Mask.stringify=function(v){if(v===null||v===undefined)return 'null';if(typeof v==='string')return '"'+v.replace(/\\/g,'\\\\').replace(/"/g,'\\"').replace(/\r/g,'\\r').replace(/\n/g,'\\n').replace(/\t/g,'\\t')+'"';if(typeof v==='number'||typeof v==='boolean')return String(v);var a=[],k;if(v instanceof Array){for(k=0;k<v.length;k++)a.push(E305Mask.stringify(v[k]));return '['+a.join(',')+']';}for(k in v)if(v.hasOwnProperty(k))a.push(E305Mask.stringify(k)+':'+E305Mask.stringify(v[k]));return '{'+a.join(',')+'}';};
E305Mask.run=function(method,args){try{return E305Mask.stringify({ok:true,data:E305Mask[method](args)});}catch(e){return E305Mask.stringify({ok:false,error:String(e)});}};
E305Mask.seq=function(){var s=app.project.activeSequence;if(!s)throw Error('Откройте секвенцию');return s;};
E305Mask.motion=function(c,graphics){var ordinary=null,vector=null;for(var i=0;i<c.components.numItems;i++){var x=c.components[i];if(x.matchName==='AE.ADBE Motion')ordinary=x;if(x.matchName==='AE.ADBE Graphic Group')vector=x;}var result=graphics?(vector||ordinary):(ordinary||vector);if(!result||result.properties.numItems<6)throw Error('У клипа нет доступных параметров трансформации');return result;};
E305Mask.selected=function(s){var sel=s.getSelection(),out=[];for(var i=0;i<sel.length;i++)if(sel[i].mediaType==='Video')out.push(sel[i]);if(out.length!==1)throw Error('Выделите один целевой клип на таймлайне: текст, графику, картинку, видео или вложенную секвенцию');return out[0];};
E305Mask.target=function(){var s=E305Mask.seq(),c=E305Mask.selected(s),m=E305Mask.motion(c,true);return {id:String(c.nodeId),name:c.name,start:c.start.seconds,end:c.end.seconds,sequence:String(s.sequenceID),component:m.displayName,playhead:s.getPlayerPosition().seconds};};
E305Mask.value=function(param,time){return param.isTimeVarying()?param.getValueAtTime(time):param.getValue();};
E305Mask.apply=function(a){
 var s=E305Mask.seq(),target=E305Mask.selected(s),source=null,t,i;
 for(t=0;t<s.videoTracks.numTracks;t++)for(i=0;i<s.videoTracks[t].clips.numItems;i++){
  var c=s.videoTracks[t].clips[i];
  if(String(c.nodeId)===String(target.nodeId)&&s.videoTracks[t].isLocked())throw Error('Целевая дорожка заблокирована');
  if(c.name===a.source.name&&Math.abs(c.start.seconds-a.source.start)<.00001&&Math.abs(c.end.seconds-a.source.end)<.00001){if(source)throw Error('Найдено несколько одинаковых исходных клипов. Используйте исходную секвенцию');source=c;}
 }
 if(!source)throw Error('Исходный клип маски не найден в активной секвенции. Вернитесь в секвенцию, где скопировали маску');
 if(String(source.nodeId)===String(target.nodeId))throw Error('Выберите клип для привязки, а не исходный клип с маской');
 if(source.isSpeedReversed()||Math.abs(source.getSpeed()-1)>.00001||target.isSpeedReversed()||Math.abs(target.getSpeed()-1)>.00001)throw Error('Изменение скорости и Reverse пока не поддерживаются');
 var rows=a.mask.rows;
 if(!rows||rows.length<2)throw Error('У маски нет достаточного числа кадров');
 if(a.rotation&&!a.mask.rotationAvailable)throw Error('Эта Object Mask не содержит данных Rotation. Уберите Rotation или используйте контурную маску');
 if(!a.position&&!a.scale&&!a.rotation)throw Error('Выберите Position, Scale или Rotation');
 var component=E305Mask.motion(target,true),p=component.properties,sp=E305Mask.motion(source,false).properties;
 var params=[];
 if(a.position)params.push({param:p[0],kind:'position',base:p[0].getValue()});
 if(a.scale){params.push({param:p[1],kind:'scale',base:p[1].getValue()});if(!p[3].getValue())params.push({param:p[2],kind:'scale',base:p[2].getValue()});}
 if(a.rotation)params.push({param:p[4],kind:'rotation',base:p[4].getValue()});
 for(i=0;i<params.length;i++)if(params[i].param.isTimeVarying())throw Error('В '+component.displayName+' уже есть анимация выбранного параметра. Отключите её или используйте отдельный клип');
 function at(seconds){var low=0,high=rows.length-1;if(seconds<=rows[0].sourceTime)return rows[0];if(seconds>=rows[high].sourceTime)return rows[high];while(high-low>1){var mid=Math.floor((high+low)/2);if(rows[mid].sourceTime<=seconds)low=mid;else high=mid;}var r=rows[low],q=rows[high],k=(seconds-r.sourceTime)/(q.sourceTime-r.sourceTime);return {sourceTime:seconds,x:r.x+(q.x-r.x)*k,y:r.y+(q.y-r.y)*k,scale:r.scale+(q.scale-r.scale)*k,rotation:r.rotation===null?null:r.rotation+(q.rotation-r.rotation)*k};}
 function world(row){var tm=new Time();tm.seconds=source.inPoint.seconds+row.sourceTime-a.source.inPoint;var pos=E305Mask.value(sp[0],tm),anchor=E305Mask.value(sp[5],tm),sy=Number(E305Mask.value(sp[1],tm))/100,sx=E305Mask.value(sp[3],tm)?sy:Number(E305Mask.value(sp[2],tm))/100,rot=Number(E305Mask.value(sp[4],tm));var x=(row.x-anchor[0]*a.mask.width)*sx,y=(row.y-anchor[1]*a.mask.height)*sy,angle=rot*Math.PI/180;return {x:pos[0]*s.frameSizeHorizontal+x*Math.cos(angle)-y*Math.sin(angle),y:pos[1]*s.frameSizeVertical+x*Math.sin(angle)+y*Math.cos(angle),scale:row.scale*Math.sqrt(Math.abs(sx*sy)),rotation:row.rotation===null?null:row.rotation+rot};}
 var offset=a.alignment==='playhead'?s.getPlayerPosition().seconds-rows[0].sourceTime:a.source.start-a.source.inPoint;
 var step=Number(s.timebase)/254016000000,first=Math.max(target.start.seconds,rows[0].sourceTime+offset),last=Math.min(target.end.seconds-step/1000,rows[rows.length-1].sourceTime+offset);
 if(last<first+step/2)throw Error('Маска не пересекается по времени с выбранным клипом. Выберите «От плейхеда» или расположите клип над исходником');
 var refTime=Math.max(first,Math.min(last,s.getPlayerPosition().seconds)),reference=world(at(refTime-offset));
 if(reference.scale<=0)throw Error('Нулевой масштаб исходного клипа');
 var keys=[];
 // Sample at actual sequence frame times, including graphic clips with a non-zero source in point.
 var begin=Math.ceil((first-target.start.seconds)/step-1e-6),end=Math.floor((last-target.start.seconds)/step+1e-6);
 if(end-begin>200000)throw Error('Слишком длинный диапазон');
 for(var n=begin;n<=end;n++){var timeline=target.start.seconds+n*step,row=world(at(timeline-offset)),key={time:target.inPoint.seconds+n*step,values:[]};
  for(i=0;i<params.length;i++){var spec=params[i],value;if(spec.kind==='position')value=[spec.base[0]+(row.x-reference.x)/s.frameSizeHorizontal,spec.base[1]+(row.y-reference.y)/s.frameSizeVertical];else if(spec.kind==='scale')value=spec.base*row.scale/reference.scale;else value=spec.base+row.rotation-reference.rotation;
   if(value instanceof Array){if(!isFinite(value[0])||!isFinite(value[1]))throw Error('Некорректные координаты маски');}else if(!isFinite(value))throw Error('Некорректные значения маски');key.values.push(value);
  }keys.push(key);
 }
 if(keys.length<2)throw Error('Для привязки нужны хотя бы два кадра');
 var changed=[];
 try{
  for(i=0;i<params.length;i++){params[i].param.setTimeVarying(true);changed.push(params[i]);}
  for(n=0;n<keys.length;n++){var time=new Time();time.seconds=keys[n].time;for(i=0;i<params.length;i++){var param=params[i].param,val=keys[n].values[i];param.addKey(time);if(param.setValueAtKey(time,val,0)===false)throw Error('Premiere отказал при записи ключа');param.setInterpolationTypeAtKey(time,0,false);}}
  // Verify endpoints, then return a concrete count rather than assuming host success.
  for(n=0;n<keys.length;n+=keys.length-1){var check=new Time();check.seconds=keys[n].time;for(i=0;i<params.length;i++){var expected=keys[n].values[i],actual=params[i].param.getValueAtTime(check);if(expected instanceof Array){if(Math.abs(actual[0]-expected[0])>.00001||Math.abs(actual[1]-expected[1])>.00001)throw Error('Проверка Position не пройдена');}else if(Math.abs(actual-expected)>Math.max(.001,Math.abs(expected)*.00001))throw Error('Проверка ключей не пройдена');}}
 }catch(e){for(i=0;i<changed.length;i++)try{changed[i].param.setTimeVarying(false);changed[i].param.setValue(changed[i].base,1);}catch(ignored){}throw e;}
 return {clip:target.name,component:component.displayName,keys:keys.length,first:first,last:last,parameters:params.length};
};
