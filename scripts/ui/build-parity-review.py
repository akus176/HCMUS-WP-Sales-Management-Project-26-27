"""Build an offline comparison deck from an existing native evidence run."""
import argparse
import hashlib
import html
import json
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("directory", type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
directory = args.directory.resolve()
report = json.loads((directory / "parity-verification.json").read_text(encoding="utf-8-sig"))
manifest_path = directory / "manifest.json"
manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
names = list(report["destinations"]) + ["menu-editor"]
cards = []
references = []
for name in names:
    concept = root / "docs/ui-concepts" / ("menu-picture-editor.png" if name == "menu-editor" else f"{name}.png")
    native = directory / ("native-menu-editor.png" if name == "menu-editor" else f"native-{name}-1440.png")
    if not concept.is_file() or not native.is_file():
        raise SystemExit(f"Missing comparison input for {name}")
    import os
    concept_link = Path(os.path.relpath(concept, directory)).as_posix()
    approved = root / "docs/ui-evidences" / native.name
    approved_link = Path(os.path.relpath(approved, directory)).as_posix() if approved.is_file() else ""
    references.append({"workspace": name, "concept": concept.relative_to(root).as_posix(), "conceptSha256": hashlib.sha256(concept.read_bytes()).hexdigest(), "nativeSha256": hashlib.sha256(native.read_bytes()).hexdigest()})
    option = f'<option value="{html.escape(approved_link)}">Current UI reference</option>' if approved_link else ""
    cards.append(f'''<section id="{name}"><h2>{name.replace('-', ' ').title()}</h2>
<p><a href="{concept_link}">Concept PNG</a> · <a href="{native.name}">Current native capture</a></p>
<label>Reference <select><option value="{concept_link}">HTML concept capture</option>{option}</select></label>
<label>View <select class="mode"><option>Side by side</option><option>Overlay</option></select></label>
<label>Native opacity <input type="range" min="0" max="1" step="0.05" value="0.5"></label>
<div class="images"><figure><figcaption>Reference</figcaption><img class="reference" src="{concept_link}" alt="Reference {name}"></figure>
<figure><figcaption>Native capture — targeted #119 revision applied</figcaption><img class="native" src="{native.name}" alt="Current {name}"></figure></div></section>''')

page = '''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>UI parity review — #119</title><style>
body{font:16px system-ui;margin:24px;background:#f3eee4;color:#262321}a{color:#76591f}h1{font-size:26px}
nav{position:sticky;top:0;background:#f3eee4;padding:12px;z-index:1;border:1px solid #c6bdb0}nav a{margin-right:18px}
section{margin:32px 0;padding:16px;border:1px solid #c6bdb0;scroll-margin-top:70px}label{display:inline-flex;gap:8px;align-items:center;margin:8px 20px 12px 0}
.images{display:grid;grid-template-columns:1fr 1fr;gap:12px;align-items:start}figure{margin:0}img{width:100%;height:auto;display:block}figcaption{margin-bottom:8px}
.overlay{position:relative;display:block}.overlay figure:first-child{width:100%}.overlay figure+figure{position:absolute;inset:0}.overlay figure+figure img{opacity:0.5}.overlay figure+figure figcaption{visibility:hidden}
</style><h1>Six-workspace parity review — #119</h1>
<p>Captures from the selected verification run. Real seeded values, the required operator selector, the existing payment boundary, and native editor framing are the recorded exceptions. Compare the original concepts and, where available, the current screenshots in docs/ui-evidences. Follow the latest user-approved polish instructions when references differ. Overlays do not normalize differing editor frames.</p>
<p><a href="manifest.json">Capture provenance</a> · <a href="parity-verification.json">Native checks and fixture state</a></p>
<nav>''' + "".join(f'<a href="#{name}">{name.replace("-", " ")}</a>' for name in names) + "</nav>" + "\n".join(cards) + '''
<script>document.querySelectorAll('section').forEach(section=>{
const selects=section.querySelectorAll('select'), images=section.querySelector('.images'), native=section.querySelector('.native');
selects[0].onchange=()=>section.querySelector('.reference').src=selects[0].value;
selects[1].onchange=()=>{images.classList.toggle('overlay',selects[1].selectedIndex===1);native.style.opacity=selects[1].selectedIndex===1?section.querySelector('input').value:1};
section.querySelector('input').oninput=event=>{if(images.classList.contains('overlay'))native.style.opacity=event.target.value};
});</script></html>'''
(directory / "comparison.html").write_text(page, encoding="utf-8")
manifest["comparisonInputs"] = references
manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print(f"Wrote {directory / 'comparison.html'}; review against current UI references.")
