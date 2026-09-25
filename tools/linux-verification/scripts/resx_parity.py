import xml.etree.ElementTree as ET, re, sys
import os
base=os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../src/Banccoon.App/Resources/Strings/')
def keys(f):
    r=ET.parse(base+f).getroot(); return {d.get('name'):d.find('value').text for d in r.findall('data')}
en,ru=keys('AppStrings.resx'),keys('AppStrings.ru.resx')
strip=lambda k: re.sub(r'_(One|Few|Many|Other)$','',k)
enb={strip(k) for k in en}; rub={strip(k) for k in ru}
print('EN-only:',sorted(enb-rub)); print('RU-only:',sorted(rub-enb))
# placeholder parity for non-plural keys
bad=[]
for k in en:
    if k in ru:
        pe=sorted(set(re.findall(r'\{\d+[^}]*\}',en[k] or ''))); pr=sorted(set(re.findall(r'\{\d+[^}]*\}',ru[k] or '')))
        if pe!=pr: bad.append((k,pe,pr))
print('placeholder mismatches:',bad)
