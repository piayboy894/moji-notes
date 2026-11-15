from pathlib import Path
import xml.etree.ElementTree as E,json,shutil,re
r=Path(__file__).resolve().parents[1];src=r/'assets/icons'
catalog=json.loads((src/'catalog.json').read_text());mapping=catalog['windows'];android=catalog['android']
def data(name):
 svg=src/(name+'.svg');paths=[]
 for el in E.parse(svg).getroot():
  tag=el.tag.split('}')[-1];v=el.attrib
  if tag=='path':paths.append(v['d'])
  elif tag=='line':paths.append(f"M{v['x1']} {v['y1']}L{v['x2']} {v['y2']}")
  elif tag in ('polyline','polygon'):paths.append('M'+v['points']+('Z' if tag=='polygon' else ''))
  elif tag=='circle':
   x,y,rad=map(float,[v['cx'],v['cy'],v['r']]);paths.append(f'M{x-rad:g} {y:g}a{rad:g} {rad:g} 0 1 0 {2*rad:g} 0a{rad:g} {rad:g} 0 1 0 {-2*rad:g} 0')
  elif tag=='rect':
   x,y,w,h=map(float,[v.get('x',0),v.get('y',0),v['width'],v['height']]);rx=float(v.get('rx',0));ry=float(v.get('ry',rx));paths.append(f'M{x+rx:g} {y:g}H{x+w-rx:g}Q{x+w:g} {y:g} {x+w:g} {y+ry:g}V{y+h-ry:g}Q{x+w:g} {y+h:g} {x+w-rx:g} {y+h:g}H{x+rx:g}Q{x:g} {y+h:g} {x:g} {y+h-ry:g}V{y+ry:g}Q{x:g} {y:g} {x+rx:g} {y:g}Z')
  else:raise ValueError(tag)
 return ' '.join(paths)
paths={v:data(v)for v in set(mapping.values())|set(android.values())}
(r/'assets/icons/catalog.json').write_text(json.dumps({'version':'lucide-static 0.468.0','windows':mapping,'android':android},indent=2))

cs='// Lucide 0.468.0; see assets/icons/LICENSE. Generated from the bundled SVG files.\nnamespace Moji.App;\ninternal static class IconPaths\n{\n    internal static readonly Dictionary<string,string> Data = new()\n    {\n'+''.join('        ['+json.dumps(k)+'] = '+json.dumps(paths[v])+',\n'for k,v in mapping.items())+'    };\n}\n';(r/'src/Moji.App/IconPaths.cs').write_text(cs)
kt='''// Lucide 0.468.0; see assets/icons/LICENSE. Shared with the Windows icon set.
package app.moji.android.ui
import androidx.compose.ui.graphics.*
import androidx.compose.ui.graphics.vector.*
import androidx.compose.ui.unit.dp
object MojiIcons {
    private fun icon(name:String, data:String) = ImageVector.Builder(name,24.dp,24.dp,24f,24f).addPath(
        pathData=PathParser().parsePathString(data).toNodes(), fill=null, stroke=SolidColor(Color.Black),
        strokeLineWidth=1.8f, strokeLineCap=StrokeCap.Round, strokeLineJoin=StrokeJoin.Round).build()
'''+''.join('    val '+k+' by lazy { icon('+json.dumps(k)+','+json.dumps(paths[v])+') }\n'for k,v in android.items())+'}\n';(r/'android/app/src/main/java/app/moji/android/ui/MojiIcons.kt').write_text(kt)
