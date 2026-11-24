# Zet Phosphor-icoontjes (duotone, MIT) om naar src/PhosphorData.cs.
# Gebruik: python tools/maak_phosphor.py <map met @phosphor-icons/core/assets/duotone>
# Elk icoon krijgt de naam die Kapture al gebruikte (links), zodat de rest van de code niet verandert.
# t = de zachte vulling (wordt licht getint), f = de vorm zelf. Raster 256x256.
import re, sys
MAP = {
    'square-dashed': 'selection', 'lasso': 'lasso', 'app-window': 'app-window', 'monitor': 'monitor',
    'sparkles': 'sparkle', 'chevron-down': 'caret-down', 'x': 'x', 'check': 'check', 'folder-open': 'folder-open',
    'camera': 'camera', 'pause': 'pause', 'power': 'power', 'alert': 'warning-circle', 'settings-2': 'sliders-horizontal',
    'sticky-note': 'note', 'history': 'clock-counter-clockwise', 'settings': 'gear-six', 'search': 'magnifying-glass',
    'keyboard': 'keyboard', 'sliders-horizontal': 'sliders-horizontal', 'folder': 'folder', 'info': 'info',
    'languages': 'translate', 'pencil': 'pencil-simple', 'copy': 'copy', 'trash-2': 'trash', 'bookmark': 'bookmark-simple',
    'send': 'paper-plane-tilt', 'list-checks': 'list-checks', 'plus': 'plus', 'file-text': 'file-text',
    'chevron-right': 'caret-right', 'image': 'image', 'external-link': 'arrow-square-out', 'file': 'file',
    'rotate-ccw': 'arrow-counter-clockwise', 'palette': 'palette', 'download': 'download-simple', 'refresh-cw': 'arrows-clockwise',
    'save': 'floppy-disk', 'mouse-pointer-2': 'cursor', 'highlighter': 'highlighter', 'arrow-up-right': 'arrow-up-right',
    'square': 'square', 'type': 'text-t', 'hash': 'hash', 'grid-3x3': 'checkerboard', 'crop': 'crop', 'eraser': 'eraser',
    'undo-2': 'arrow-u-up-left', 'redo-2': 'arrow-u-up-right',
}
folder = sys.argv[1]
out = ['// Phosphor-icoontjes, duotone (MIT-licentie, zie assets/LICENSE-Phosphor.txt). Gemaakt met tools/maak_phosphor.py.',
       '// t = zachte vulling (licht getint), f = de vorm. Raster 256x256.',
       'using System.Collections.Generic;', '', 'namespace Kapture', '{', '    static class PhosphorData', '    {',
       '        public static readonly Dictionary<string, string[]> All = new Dictionary<string, string[]>', '        {']
for key in sorted(MAP):
    svg = open('%s/%s-duotone.svg' % (folder, MAP[key])).read()
    parts = []
    for m in re.finditer(r'<path d="([^"]+)"([^>]*)/>', svg):
        kind = 't' if 'opacity' in m.group(2) else 'f'
        parts.append('"%s|%s"' % (kind, m.group(1)))
    out.append('            { "%s", new string[] { %s } },' % (key, ', '.join(parts)))
out[-1] = out[-1].rstrip(',')
out += ['        };', '    }', '}', '']
open('src/PhosphorData.cs', 'w', encoding='utf-8').write('\n'.join(out))
print(len(MAP), 'icoontjes')
