from pathlib import Path
import xml.etree.ElementTree as E
root=Path(__file__).resolve().parents[1]/'src/MusicDownloaderPro'
files=list(root.rglob('*.xaml'))
ns='{http://schemas.microsoft.com/winfx/2006/xaml}'
keys={n.attrib[ns+'Key'] for f in files for n in E.parse(f).iter() if ns+'Key' in n.attrib}
required={'TextPrimaryBrush','TextSecondaryBrush','SurfaceBrush','PrimaryBrush','OnlineResultCard','DarkComboBox'}
assert required <= keys, f'Missing resources: {required-keys}'
for f in files:
    import re
    for name in re.findall(r'\{(?:Static|Dynamic)Resource ([\w]+)\}',f.read_text()):
        assert name in keys, f'{f.name}: unresolved {name}'
print('XAML parses and every named resource reference resolves.')
