"""Package the approved concept assets for the native WinUI client."""
from pathlib import Path
import re
import shutil
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
concepts = root / 'docs/ui-concepts/assets'
native = root / 'src/CoffeeShop.WinUI/Assets'
for folder in ('fonts', 'ornaments', 'patterns', 'photos'):
    target = native / ('Fonts' if folder == 'fonts' else folder.title())
    target.mkdir(parents=True, exist_ok=True)
    for asset in (concepts / folder).iterdir():
        if asset.is_file():
            shutil.copy2(asset, target / asset.name)

sprite = (concepts / 'icons.js').read_text(encoding='utf-8')
for name, content in re.findall(r'<symbol id="([^"]+)" viewBox="0 0 24 24">(.*?)</symbol>', sprite, re.S):
    for variant, color in (('Icons', '#5f5750'), ('IconsLight', '#f3eee4')):
        target = native / variant
        target.mkdir(exist_ok=True)
        svg = f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="{color}" stroke-width="1.25" stroke-linecap="round" stroke-linejoin="round">{content}</svg>'
        ET.fromstring(svg)
        (target / f'{name}.svg').write_text(svg, encoding='utf-8')
print('Packaged concept fonts, photographs, ornaments, patterns, and standalone icons.')
