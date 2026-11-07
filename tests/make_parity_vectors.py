"""
Writes tests/parity_vectors.tsv from the Python reference engine (full shipped dictionaries).
KeyMind.Tests.exe (TEST-ENGINE.cmd) replays every row through the C# engine and fails on any
disagreement, proving the C# port behaves like the measured Python spec.
Run:  python3 tests/make_parity_vectors.py
"""
import random, sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent))
import reference_engine as R

ROOT = Path(__file__).resolve().parent.parent
rng = random.Random(2024)
en = R.load_repo_english(ROOT)
ar = R.load_repo_arabic(ROOT)
eng = R.Engine(en, ar)


def keys_of_arabic(text):
    out, i = [], 0
    while i < len(text):
        if text[i:i + 2] == "لا":
            out.append("b"); i += 2
        elif text[i] in R.AR2EN:
            out.append(R.AR2EN[text[i]]); i += 1
        else:
            return None
    return "".join(out)


rows = []  # (token, src_is_arabic, physical)
ar_raw = [w for w in (ROOT / "data/ar-common.txt").read_text(encoding="utf8").splitlines() if w.strip()]
ar_typable = [w for w in ar_raw if len(w) >= 3 and all(c in R.AR2EN for c in w)]
common_en = [w.strip() for w in (ROOT / "data/en-common.txt").read_text(encoding="utf8").splitlines() if len(w.strip()) >= 4 and w.strip().isalpha()]
ranked_en = [w for w, c in en.items() if 1 <= c < 190 and 4 <= len(w) <= 10]
listed_en = [w for w, c in en.items() if c == 0 and 4 <= len(w) <= 10]

for w in rng.sample(ar_typable, 70):                    # Arabic intent typed on EN layout
    rows.append((R.to_english(w), 0, R.to_english(w)))
for w in rng.sample(common_en, 40) + rng.sample(ranked_en, 50) + rng.sample(listed_en, 30):   # English intent typed on AR layout
    rows.append((R.to_arabic(w), 1, w))
for w in rng.sample(common_en, 40) + rng.sample(ranked_en, 30):                              # correct English
    rows.append((w, 0, w))
for w in rng.sample(ar_typable, 50):                                                          # correct Arabic
    rows.append((w, 1, keys_of_arabic(w)))
L = "abcdefghijklmnopqrstuvwxyz"
for _ in range(40):                                                                           # gibberish on EN layout
    g = "".join(rng.choice(L) for _ in range(rng.randint(4, 8))); rows.append((g, 0, g))
for _ in range(40):                                                                           # gibberish on AR layout
    g = "".join(rng.choice(L) for _ in range(rng.randint(4, 8))); rows.append((R.to_arabic(g), 1, g))
for w in rng.sample(common_en, 30):                                                           # English + punctuation
    t = w + rng.choice([",", ".", ";", "/"]); rows.append((t, 0, t))
for t in ["don't", "it's", "i'm", "can't", "they're", "we'll", "isn't", "you've", "that's", "wouldn't"]:
    rows.append((t, 0, t))                                                                    # contractions
for w in rng.sample([w for w in ar_typable if len(w) >= 3], 30):                              # clitics: ال / و + known word
    for pre in ("ال", "و"):
        t = pre + w
        if all(c in R.AR2EN for c in t):
            rows.append((R.to_english(t), 0, R.to_english(t))); break
for t in ["مستشفيات", "برمجيات", "حاسوب", "مدرسون", "مكتبات", "تطبيقات", "شاشات", "لوحات", "ملفات", "رسائل",
          "خوارزمية", "متصفحات", "بطارية", "كاميرات", "مبرمجين"]:                            # plausible Arabic NOT in the dictionary
    k = keys_of_arabic(t)
    if k:
        rows.append((k, 0, k))
for t in ["مرحبا", "اهلا", "شكرا", "محمد", "احمد", "سلام"]:
    k = keys_of_arabic(t)
    rows.append((k, 0, k))

out = ["# token\tsource_is_arabic\tphysical_keys\taccepted\tconverted\tconfidence"]
seen = set()
for tok, src, phys in rows:
    if (tok, src) in seen or phys is None:
        continue
    seen.add((tok, src))
    r = eng.evaluate(tok, bool(src), 88, False, phys)
    out.append("%s\t%d\t%s\t%d\t%s\t%d" % (tok, src, phys, 1 if r["accepted"] else 0, r["converted"], r["confidence"]))
Path(ROOT / "tests/parity_vectors.tsv").write_text("\n".join(out) + "\n", encoding="utf8")
acc = sum(1 for l in out[1:] if l.split("\t")[3] == "1")
print(f"wrote {len(out)-1} vectors ({acc} converted, {len(out)-1-acc} left alone)")
