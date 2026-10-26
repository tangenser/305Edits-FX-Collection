"""Read-only conversion of Premiere clipboard mask data into sampled transforms."""
import sys,json,base64,re,struct,math
from pathlib import Path
import xml.etree.ElementTree as ET

TICKS=254016000000
class FormatError(ValueError):pass
class Flat:
    def __init__(self,data):self.data=data
    def read(self,fmt,pos):
        if pos is None or pos<0 or pos+struct.calcsize(fmt)>len(self.data):raise FormatError('Повреждён индекс маски')
        return struct.unpack_from(fmt,self.data,pos)
    def u32(self,pos):return self.read('<I',pos)[0]
    def field(self,table,index):
        vt=table-self.read('<i',table)[0];length=self.read('<H',vt)[0];at=vt+4+index*2
        if at+2>vt+length:return None
        offset=self.read('<H',at)[0]
        return table+offset if offset else None
    def vector(self,table,index):
        p=self.field(table,index)
        if p is None:return []
        v=p+self.u32(p);count=self.u32(v)
        if count>1000000 or v+4+count*4>len(self.data):raise FormatError('Некорректный список кадров маски')
        return [v+4+i*4+self.u32(v+4+i*4) for i in range(count)]

def read_prmf(path):
    with open(path,'rb') as stream:
        head=stream.read(32)
        if len(head)!=32 or head[:4]!=b'prmf':raise FormatError('Неизвестный файл маски')
        version=struct.unpack_from('<I',head,4)[0]
        if version!=3:raise FormatError('Версия PRMF не поддерживается: '+str(version))
        offset,size,_=struct.unpack_from('<QQQ',head,8)
        if size>64*1024*1024 or offset+size>Path(path).stat().st_size:raise FormatError('Повреждён файл PRMF')
        stream.seek(offset);f=Flat(stream.read(size))
    rows=[]
    for t in f.vector(f.u32(0),2):
        p=f.field(t,1);ticks=f.read('<q',p)[0] if p is not None else 0
        box=f.read('<iiii',f.field(t,3));width,height=f.read('<ii',f.field(t,4))
        x,y,w,h=box
        if w<=0 or h<=0:continue
        if width<=0 or height<=0 or max(abs(x),abs(y),w,h)>1000000:raise FormatError('Некорректная область маски')
        rows.append(dict(sourceTime=ticks/TICKS,x=x+w/2,y=y+h/2,width=w,height=h,frameWidth=width,frameHeight=height))
    return sorted(rows,key=lambda row:row['sourceTime'])

def text(element,path,default=''):
    value=element.findtext(path)
    return default if value is None else value
def reference(element,path,objects):
    item=element.find(path)
    return objects.get(item.get('ObjectRef')) if item is not None else None
def number(element,path,default=0):
    try:return float(text(element,path,str(default)))
    except ValueError:raise FormatError('Некорректное числовое поле '+path)

def keyframes(param,binary=False):
    result=[]
    for entry in text(param,'Keyframes').split(';'):
        if not entry.strip():continue
        tick,value=entry.split(',',1)
        result.append((int(tick)/TICKS,base64.b64decode(value) if binary else value.split(',')[0]))
    return sorted(result,key=lambda pair:pair[0])

def scalar(param,time,default):
    if param is None:return default
    keys=keyframes(param)
    def value(raw):return tuple(float(v) for v in raw.split(':')) if ':' in raw else float(raw)
    if not keys:
        start=text(param,'StartKeyframe')
        return value(start.split(',')[1]) if start else default
    if time<=keys[0][0]:return value(keys[0][1])
    for (a,x),(b,y) in zip(keys,keys[1:]):
        if time<=b:
            k=(time-a)/(b-a);x=value(x);y=value(y)
            return tuple(v+(w-v)*k for v,w in zip(x,y)) if isinstance(x,tuple) else x+(y-x)*k
    return value(keys[-1][1])

def path_vertices(data):
    if len(data)<8:raise FormatError('Пустой контур маски')
    version,count=struct.unpack_from('<II',data)
    if version!=2 or not 1<=count<=128:raise FormatError('Формат контура пока не поддерживается')
    at=8;points=[]
    for _ in range(count):
        if at+13>len(data):raise FormatError('Повреждён контур')
        _,_,curve,n=struct.unpack_from('<IBII',data,at);at+=13
        if curve!=2 or not 2<=n<=100000 or at+n*28+1>len(data):raise FormatError('Неизвестный тип контура')
        for _ in range(n):
            kind,x,y,_,_,_,_=struct.unpack_from('<I6f',data,at);at+=28
            if not math.isfinite(x+y):raise FormatError('Некорректная вершина')
            points.append((x,y))
        at+=1
    return points

def similarity(reference,points):
    if len(reference)!=len(points):raise FormatError('Число вершин изменилось: невозможно надёжно перенести Rotation')
    n=len(points);rx=sum(x for x,y in reference)/n;ry=sum(y for x,y in reference)/n
    cx=sum(x for x,y in points)/n;cy=sum(y for x,y in points)/n
    norm=sum((x-rx)**2+(y-ry)**2 for x,y in reference)
    if norm<1e-9:raise FormatError('Вырожденная маска')
    real=sum((x-rx)*(u-cx)+(y-ry)*(v-cy) for (x,y),(u,v) in zip(reference,points))/norm
    imag=sum((x-rx)*(v-cy)-(y-ry)*(u-cx) for (x,y),(u,v) in zip(reference,points))/norm
    return cx,cy,math.hypot(real,imag),math.degrees(math.atan2(imag,real))

def vector_mask(params,width,height,source_in,duration):
    by_id={text(p,'ParameterID'):p for p in params.values()}
    tracker=by_id.get('6');path=by_id.get('7')
    if tracker is None or path is None:return None
    motion=keyframes(tracker,True)
    if len(motion)<2:return None
    shape_keys=keyframes(path,True)
    initial=base64.b64decode(text(path,'StartKeyframeValue')) if text(path,'StartKeyframeValue').strip() else None
    if not initial and shape_keys:initial=shape_keys[0][1]
    if not initial:return None
    decoded_shapes=[(t,path_vertices(blob)) for t,blob in shape_keys]
    initial_points=path_vertices(initial)
    def shape(time):
        if not decoded_shapes:return initial_points
        if time<=decoded_shapes[0][0]:return decoded_shapes[0][1]
        for (a,p),(b,q) in zip(decoded_shapes,decoded_shapes[1:]):
            if time<=b:
                if len(p)!=len(q):raise FormatError('Число вершин маски меняется')
                k=(time-a)/(b-a);return [(x+(u-x)*k,y+(v-y)*k) for (x,y),(u,v) in zip(p,q)]
        return decoded_shapes[-1][1]
    result=[];reference=None;previous_angle=None
    for time,blob in motion:
        if not source_in-1e-7<=time<source_in+duration-1e-7:continue
        if len(blob)!=104 or struct.unpack_from('<I',blob)[0]!=1:raise FormatError('Неизвестный формат матрицы трекера Premiere')
        matrix=struct.unpack_from('<25f',blob,4)[:9]
        position=scalar(by_id.get('9'),time,(0,0));anchor=scalar(by_id.get('10'),time,(0,0))
        sx=scalar(by_id.get('27'),time,100)/100;sy=scalar(by_id.get('26'),time,100)/100
        angle=math.radians(scalar(by_id.get('12'),time,0));points=[]
        for x,y in shape(time):
            dx=(x-anchor[0])*width*sx;dy=(y-anchor[1])*height*sy
            u=(dx*math.cos(angle)-dy*math.sin(angle))/width+position[0]
            v=(dx*math.sin(angle)+dy*math.cos(angle))/height+position[1]
            denominator=u*matrix[2]+v*matrix[5]+matrix[8]
            if abs(denominator)<1e-8:raise FormatError('Вырожденная матрица трекинга')
            points.append(((u*matrix[0]+v*matrix[3]+matrix[6])/denominator*width,(u*matrix[1]+v*matrix[4]+matrix[7])/denominator*height))
        if reference is None:reference=points
        x,y,scale,rotation=similarity(reference,points)
        if previous_angle is not None:
            while rotation-previous_angle>180:rotation-=360
            while rotation-previous_angle<-180:rotation+=360
        previous_angle=rotation
        result.append(dict(sourceTime=time,x=x,y=y,scale=scale,rotation=rotation))
    return result

def parse_clipboard(xml):
    if len(xml)>128*1024*1024 or '<!DOCTYPE' in xml.upper():raise FormatError('Неподдерживаемые данные буфера обмена')
    root=ET.fromstring(xml.rstrip('\x00'))
    if root.tag!='PremiereData':raise FormatError('В буфере нет маски Premiere')
    objects={e.get('ObjectID'):e for e in root if e.get('ObjectID')}
    items=root.findall('VideoClipTrackItem')
    if len(items)!=1:raise FormatError('Скопируйте одну маску одного клипа')
    item=items[0];sub=reference(item,'ClipTrackItem/SubClip',objects)
    clip=reference(sub,'Clip',objects) if sub is not None else None
    if clip is None:raise FormatError('Не найден исходный клип маски')
    source_in=number(clip,'Clip/InPoint')/TICKS
    start=number(item,'ClipTrackItem/TrackItem/Start')/TICKS
    end=number(item,'ClipTrackItem/TrackItem/End')/TICKS
    project_path=root.findtext('.//SourceProjectPath') or root.findtext('.//project.settings.lastknowngoodprojectpath') or ''
    project_path=project_path.removeprefix('\\\\?\\')
    project=Path(project_path) if project_path else None
    mask_folder=project.with_name(project.stem+' Masks') if project else None
    rect=[int(v) for v in text(item,'FrameRect','0,0,1920,1080').split(',')]
    width,height=rect[2]-rect[0],rect[3]-rect[1]
    chain=reference(item,'ClipTrackItem/ComponentOwner/Components',objects)
    if chain is None:raise FormatError('Не найдены эффекты исходного клипа')
    # Use only masks reachable from the copied clip, never arbitrary project entries.
    reachable=set()
    def visit(node):
        if node is None:return
        identity=node.get('ObjectID')
        if identity in reachable:return
        if identity:reachable.add(identity)
        for child in node.iter():
            if child.get('ObjectRef') in objects:visit(objects[child.get('ObjectRef')])
    visit(chain)
    masks=[]
    for component in root.findall('VideoFilterComponent'):
        if component.get('ObjectID') not in reachable:continue
        match=text(component,'MatchName')
        if 'mask' not in match.lower():continue
        params={}
        for param in component.findall('Component/Params/Param'):
            obj=objects.get(param.get('ObjectRef'))
            if obj is not None:params[text(obj,'Name')+':'+text(obj,'ParameterID')]=obj
        vector=vector_mask(params,width,height,source_in,end-start)
        if vector:
            masks.append(dict(id=component.get('ObjectID'),name=text(component,'Component/InstanceName') or 'Контурная маска',kind='vector',rotationAvailable=True,width=width,height=height,rows=vector))
            continue
        tracker=[p for p in params.values() if text(p,'Name')=='Tracker']
        ids=[]
        for param in tracker:
            for child in param:
                if child.get('Encoding')=='base64' and child.text:
                    try:data=base64.b64decode(child.text)
                    except ValueError:continue
                    ids.extend(s.decode('ascii') for s in re.findall(rb'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}',data))
        files=[mask_folder/(identifier+'.prmf') for identifier in dict.fromkeys(ids)] if mask_folder else []
        files=[file for file in files if file.is_file()]
        series=[read_prmf(file) for file in files]
        if not series:continue
        merged={}
        for rows in sorted(series,key=len):
            for row in rows:merged[round(row['sourceTime']*TICKS)]=row
        rows=[r for _,r in sorted(merged.items()) if source_in-1e-7<=r['sourceTime']<source_in+(end-start)-1e-7]
        if not rows:continue
        first=rows[0];area=first['width']*first['height']
        for row in rows:
            row['scale']=math.sqrt(row['width']*row['height']/area)
            row['rotation']=None
        masks.append(dict(id=component.get('ObjectID'),name=text(component,'Component/InstanceName') or 'Object Mask',kind='object',rotationAvailable=False,
                          width=first['frameWidth'],height=first['frameHeight'],rows=rows))
    if not masks:raise FormatError('Не найден трек маски. Скопируйте всю оттреченную маску. Для Object Mask проект должен быть сохранён вместе с папкой Masks.')
    masks=[m for m in masks if len(m['rows'])>=2]
    if not masks:raise FormatError('У маски нет анимации: найден только один кадр. Сначала выполните трекинг Premiere')
    return dict(format=1,source=dict(name=text(sub,'Name'),start=start,end=end,inPoint=source_in),masks=masks)

def smooth(rows,window):
    radius=max(0,min(7,int(window)//2))
    if not radius:return rows
    result=[]
    for i,row in enumerate(rows):
        out=dict(row)
        for key in ['x','y','scale','rotation']:
            if row[key] is None:continue
            values=[(rows[j][key],radius+1-abs(j-i)) for j in range(max(0,i-radius),min(len(rows),i+radius+1))]
            out[key]=sum(v*w for v,w in values)/sum(w for _,w in values)
        result.append(out)
    return result

def main(job):
    xml=base64.b64decode(job['xmlBase64']).decode('utf-8-sig') if 'xmlBase64' in job else job['xml']
    data=parse_clipboard(xml)
    for mask in data['masks']:mask['rows']=smooth(mask['rows'],job.get('smoothing',0))
    return data

if __name__=='__main__':
    sys.stdin.reconfigure(encoding='utf-8')
    try:print(json.dumps(dict(ok=True,data=main(json.loads(sys.stdin.readline()))),ensure_ascii=True),flush=True)
    except Exception as exc:print(json.dumps(dict(ok=False,error=str(exc)),ensure_ascii=True),flush=True);sys.exit(1)
