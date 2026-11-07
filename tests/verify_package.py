from pathlib import Path
import re,zipfile,sys
r=Path(__file__).resolve().parent.parent
n=0

def check(name,pred):
 global n
 assert pred,name
 n+=1
 print('[PASS]',name)

en='qwertyuiop[]asdfghjkl;\'zxcvbnm,./`'
ar='ض ص ث ق ف غ ع ه خ ح ج د ش س ي ب ل ا ت ن م ك ط ئ ء ؤ ر لا ى ة و ز ظ ذ'.split()
assert len(en)==len(ar)
emap=dict(zip(en,ar))

def keys_to_ar(txt):return ''.join(emap[ch] for ch in txt)

def ar_to_en(txt):
 reverse={value:key for key,value in emap.items()}
 return ''.join(reverse[ch] for ch in txt)

for left,right in [('local','مخؤشم'),('key','نثغ'),('python','حغفاخى'),('code','ؤخيث'),('lpl]','محمد'),('sghl','سلام')]:
 check('layout ' + left,keys_to_ar(left)==right)
words=set((r/'data/en-extended.txt').read_text().splitlines())
freq={x.split()[0] for x in (r/'data/en-frequency.txt').read_text().splitlines()}
check('en vocabulary 150k+',len(words|freq)>150000)
check('arabic vocabulary 1400+',len((r/'data/ar-common.txt').read_text().splitlines())>1400)
for k in ['local','key','python','code']:
 check('known English '+k,k in words or k in freq)
for k in ['محمد','سلام']:
 check('known Arabic '+k,k in (r/'data/ar-common.txt').read_text())
for src in ['WordConverter.cs','Lexicon.cs','KeyboardEngine.cs','MainForm.cs','SettingsStore.cs','Program.cs']:
 txt=(r/'src'/src).read_text()
 check(src + ' exists and C# namespace',len(txt)>100 and 'namespace KeyMind' in txt)
 check(src + ' braces balanced',txt.count('{')==txt.count('}'))
check('launcher exists',(r/'START-KEYMIND.cmd').is_file())
check('logo icon exists',(r/'assets/keymind.ico').stat().st_size>1500)
check('Lexicon wired into build',all('Lexicon.cs' in (r/f).read_text() for f in ['Build-KeyMind.cmd','TEST-ENGINE.cmd']))
check('parity vectors present',(r/'tests/parity_vectors.tsv').stat().st_size>5000)
print(f'Passed {n} checks (static/mapping tests only; Windows build unverified).')
