"""Package fonts and the SVG assets referenced by the current WinUI client."""
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
concepts = root / 'docs/ui-concepts/assets'
native = root / 'src/CoffeeShop.WinUI/Assets'
sources = [path for path in native.parent.rglob('*')
           if path.is_file() and path.suffix in ('.cs', '.xaml')
           and not {'bin', 'obj'}.intersection(path.relative_to(native.parent).parts)]
referenced = {'Icons': set(), 'Ornaments': set(), 'Patterns': set()}
for source in sources:
    for folder, name in re.findall(r'ms-appx:///Assets/(Icons|Ornaments|Patterns)/([^"\s]+\.svg)',
                                   source.read_text(encoding='utf-8-sig')):
        referenced[folder].add(name)

(native / 'Fonts').mkdir(parents=True, exist_ok=True)
for asset in (concepts / 'fonts').iterdir():
    if asset.is_file():
        shutil.copy2(asset, native / 'Fonts' / asset.name)
for folder in ('Ornaments', 'Patterns'):
    (native / folder).mkdir(exist_ok=True)
    for name in referenced[folder]:
        shutil.copy2(concepts / folder.lower() / name, native / folder / name)

sprite = (concepts / 'icons.js').read_text(encoding='utf-8')
symbols = dict(re.findall(r'<symbol id="([^"]+)" viewBox="0 0 24 24">(.*?)</symbol>', sprite, re.S))
(native / 'Icons').mkdir(exist_ok=True)
for filename in referenced['Icons']:
    name = Path(filename).stem
    content = symbols[name]
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="#5f5750" stroke-width="1.25" stroke-linecap="round" stroke-linejoin="round">{content}</svg>'
    ET.fromstring(svg)
    (native / 'Icons' / filename).write_text(svg, encoding='utf-8')
print(f'Packaged concept fonts and {sum(map(len, referenced.values()))} referenced SVG assets. Menu photos remain in object storage.')
