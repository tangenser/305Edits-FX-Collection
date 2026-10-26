import unittest,sys,math,struct,base64
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'engine'))
import mask_transfer as m
import xml.etree.ElementTree as ET

class MaskTests(unittest.TestCase):
    def test_similarity(self):
        p=[(10,20),(50,20),(50,40),(10,40)]
        a=math.radians(35)
        q=[(2*(x*math.cos(a)-y*math.sin(a))+100,2*(x*math.sin(a)+y*math.cos(a))-70) for x,y in p]
        x,y,s,r=m.similarity(p,q)
        self.assertAlmostEqual(s,2);self.assertAlmostEqual(r,35)
        self.assertAlmostEqual(x,sum(v[0] for v in q)/4)
    def test_vector_non_square_frame(self):
        points=[(.2,.3),(.4,.3),(.4,.5),(.2,.5)]
        blob=struct.pack('<IIIBII',2,1,0,0,2,4)+b''.join(struct.pack('<I6f',0,x,y,0,0,0,0) for x,y in points)+b'\1'
        path=ET.Element('Param');ET.SubElement(path,'ParameterID').text='7';ET.SubElement(path,'StartKeyframeValue').text=base64.b64encode(blob).decode()
        tracker=ET.Element('Param');ET.SubElement(tracker,'ParameterID').text='6'
        angle=math.radians(25);c=1.2*math.cos(angle);s=1.2*math.sin(angle)
        matrices=[[1,0,0,0,1,0,0,0,1],[c,s*1920/1080,0,-s*1080/1920,c,0,.1,-.2,1]]
        ET.SubElement(tracker,'Keyframes').text=';'.join(str(int(t*m.TICKS))+','+base64.b64encode(struct.pack('<I25f',1,*(mat+[0]*16))).decode() for t,mat in zip([64.64,64.68],matrices))+';'
        rows=m.vector_mask({'path':path,'tracker':tracker},1920,1080,64.64,1)
        self.assertEqual(len(rows),2);self.assertAlmostEqual(rows[1]['scale'],1.2,places=5);self.assertAlmostEqual(rows[1]['rotation'],25,places=5)
        self.assertAlmostEqual(rows[0]['sourceTime'],64.64)
    def test_rejects_invalid(self):
        for xml in ['<other/>','<!DOCTYPE a><PremiereData/>']:
            with self.assertRaises(m.FormatError):m.parse_clipboard(xml)
        with self.assertRaises(m.FormatError):m.path_vertices(b'bad')
        with self.assertRaises(m.FormatError):m.Flat(b'123').u32(0)
    def test_smoothing_preserves_null_rotation(self):
        rows=[dict(sourceTime=i,x=v,y=v,scale=1,rotation=None) for i,v in enumerate([0,10,0])]
        out=m.smooth(rows,3)
        self.assertEqual(out[1]['x'],5);self.assertIsNone(out[1]['rotation']);self.assertEqual(out[1]['sourceTime'],1)
    def test_actual_samples(self):
        root=Path(__file__).parent
        for folder,kind,count in [('mask-clipboard','object',226),('vector-mask-clipboard','vector',41)]:
            sample=root/folder/'clipboard-49968.bin'
            if not sample.exists():continue
            result=m.parse_clipboard(sample.read_text(encoding='utf-8-sig'))
            mask=result['masks'][0]
            self.assertEqual(mask['kind'],kind);self.assertEqual(len(mask['rows']),count)
            self.assertEqual(mask['rotationAvailable'],kind=='vector')
            self.assertTrue(all(a['sourceTime']<b['sourceTime'] for a,b in zip(mask['rows'],mask['rows'][1:])))

if __name__=='__main__':unittest.main(verbosity=2)
