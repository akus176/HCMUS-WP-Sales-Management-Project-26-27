from playwright.sync_api import sync_playwright
import json
from pathlib import Path
out = Path(__file__).resolve().parent
results = []
with sync_playwright() as p:
    browser = p.chromium.launch(executable_path='/usr/bin/chromium', args=['--no-sandbox'])
    for name, width, height, large, query in [
        ('phone',390,844,False,''), ('tablet',800,1024,False,''),
        ('phone-large-text',390,844,True,''), ('control-states',390,844,False,'?controls'),
    ]:
        page = browser.new_page(viewport={'width':width, 'height':height})
        errors = []
        page.on('pageerror', lambda e: errors.append(str(e)))
        page.goto('http://127.0.0.1:8194/'+query)
        page.get_by_role('heading').wait_for()
        if large:
            page.evaluate("""() => document.querySelectorAll('[dir="auto"]').forEach(el => { const size = parseFloat(getComputedStyle(el).fontSize); el.style.fontSize = `${size*2}px`; })""")
        buttons = page.get_by_role('button')
        bounds = [buttons.nth(i).bounding_box() for i in range(buttons.count())]
        assert all(b and b['width'] >=48 and b['height'] >=48 for b in bounds), bounds
        assert page.evaluate('document.documentElement.scrollWidth <= innerWidth'), 'horizontal overflow'
        if query:
            page.get_by_role('button',name='Retry access check').focus()
            focused_color = page.get_by_role('button',name='Retry access check').evaluate('(el) => getComputedStyle(el).borderTopColor')
            assert focused_color == 'rgb(48, 40, 32)', focused_color
        page.screenshot(path=str(out/(name+'.png')),full_page=True)
        results.append({'fixture': name, 'viewport': [width,height], 'text_scale_simulation': 2 if large else 1, 'buttons': len(bounds), 'minimum_target_dp':48, 'horizontal_overflow':False, 'browser_errors':errors})
        assert not errors, errors
        page.close()
    browser.close()
(out/'browser-checks.json').write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(results,ensure_ascii=False))
