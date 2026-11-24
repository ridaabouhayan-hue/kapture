# Zet Lucide-icoontjes (lucide-static, ISC) om naar regels voor src/IconData.cs.
# Gebruik: python tools/maak_icoontjes.py <map met lucide-static/icons> naam1 naam2 ...
# p = pad, c = cirkel (cx cy r), e = ellips, r = rechthoek (x y b h hoek), l = lijn, y = polyline, g = polygon.
import re, sys, xml.etree.ElementTree as ET

def conv(path):
    root = ET.parse(path).getroot()
    out = []
    for el in root:
        tag = el.tag.split('}')[-1]
        a = el.attrib
        if tag == 'path': out.append('p|' + a['d'])
        elif tag == 'circle': out.append('c|%s %s %s' % (a['cx'], a['cy'], a['r']))
        elif tag == 'ellipse': out.append('e|%s %s %s %s' % (a['cx'], a['cy'], a['rx'], a['ry']))
        elif tag == 'rect': out.append('r|%s %s %s %s %s' % (a.get('x', '0'), a.get('y', '0'), a['width'], a['height'], a.get('rx', '0')))
        elif tag == 'line': out.append('l|%s %s %s %s' % (a['x1'], a['y1'], a['x2'], a['y2']))
        elif tag == 'polyline': out.append('y|' + ' '.join(re.split(r'[\s,]+', a['points'].strip())))
        elif tag == 'polygon': out.append('g|' + ' '.join(re.split(r'[\s,]+', a['points'].strip())))
        else: raise SystemExit('onbekend element %s in %s' % (tag, path))
    return out

folder = sys.argv[1]
for name in sys.argv[2:]:
    parts = conv('%s/%s.svg' % (folder, name))
    print('            { "%s", new string[] { %s } },' % (name, ', '.join('"%s"' % p for p in parts)))
