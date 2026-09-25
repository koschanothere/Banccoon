"""Usage: python3 resx_add.py entries.json
entries.json: {"anchor_en": key|null, "anchor_ru": key|null, "en": [[k,v],...], "ru": [[k,v],...]}
Inserts each entry after the anchor's </data> (or before </root> if anchor is null). XML-escapes values.
Refuses duplicates."""
import json, sys
from xml.sax.saxutils import escape
import os
base = os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../src/Banccoon.App/Resources/Strings/')
spec = json.load(open(sys.argv[1], encoding='utf-8'))
def add(fname, anchor, entries):
    path = base + fname
    s = open(path, encoding='utf-8').read()
    for k, _ in entries:
        assert f'<data name="{k}" ' not in s, f'duplicate key {k} in {fname}'
    block = ''.join(f'\n  <data name="{k}" xml:space="preserve">\n    <value>{escape(v)}</value>\n  </data>' for k, v in entries)
    if anchor:
        i = s.index(f'<data name="{anchor}" ')
        j = s.index('</data>', i) + len('</data>')
    else:
        j = s.rindex('\n</root>')
    s = s[:j] + block + s[j:]
    open(path, 'w', encoding='utf-8').write(s)
    print(f'{fname}: +{len(entries)}')
add('AppStrings.resx', spec.get('anchor_en'), spec['en'])
add('AppStrings.ru.resx', spec.get('anchor_ru', spec.get('anchor_en')), spec['ru'])
