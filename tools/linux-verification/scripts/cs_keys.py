# Every Translator.Get("Key") / Translator.GetPlural("Key", n) in App C# code must exist in the
# English resx (a plural base needs at least its _Other form). The XAML checker only covers
# {loc:Translate} keys, so a key used only from code could otherwise ship missing and show up raw.
import os, re, sys
import xml.etree.ElementTree as ET

here = os.path.dirname(os.path.abspath(__file__))
app = os.path.join(here, '../../../src/Banccoon.App')
resx = os.path.join(app, 'Resources/Strings/AppStrings.resx')
keys = {d.get('name') for d in ET.parse(resx).getroot().findall('data')}

get = re.compile(r'Translator\.Get\(\s*"([A-Za-z0-9_]+)"')
plural = re.compile(r'Translator\.GetPlural\(\s*"([A-Za-z0-9_]+)"')
missing, checked = [], 0
for root, dirs, files in os.walk(app):
    dirs[:] = [d for d in dirs if d not in ('bin', 'obj')]
    for name in files:
        if not name.endswith('.cs'):
            continue
        path = os.path.join(root, name)
        text = open(path, encoding='utf-8').read()
        for key in get.findall(text):
            checked += 1
            if key not in keys:
                missing.append((os.path.relpath(path, app), key))
        for key in plural.findall(text):
            checked += 1
            if key + '_Other' not in keys:
                missing.append((os.path.relpath(path, app), key + '_Other'))

print(f'C# translation keys: {checked} checked, missing: {missing}')
sys.exit(1 if missing else 0)
