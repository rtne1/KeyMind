"""
How does accuracy scale with the size of the Arabic dictionary?
Fixed unseen TEST set (30% of words); the engine is trained on growing slices of the rest.
Run: python3 tests/eval_scaling.py
"""
import random, sys, zlib
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent))
import reference_engine as R

ROOT = Path(__file__).resolve().parent.parent
ar_all = sorted({w.strip() for w in (ROOT / "data/ar-common.txt").read_text(encoding="utf8").splitlines() if w.strip()})
def typable(w): return len(w) >= 3 and all(c in R.AR2EN for c in w)
bucket = lambda w: zlib.crc32(w.encode("utf8")) % 10
test = [w for w in ar_all if bucket(w) < 3 and typable(w)]
pool = [w for w in ar_all if bucket(w) >= 3]
rng = random.Random(1); rng.shuffle(pool)
en = R.load_repo_english(ROOT)
letters = "abcdefghijklmnopqrstuvwxyz"
gib = ["".join(rng.choice(letters) for _ in range(rng.randint(4, 8))) for _ in range(3000)]
print(f"unseen test words: {len(test)}")
print(f"{'Arabic words in dictionary':>28s} {'recall on unseen':>18s} {'gibberish false-positive':>26s}")
for n in (100, 200, 400, 800, len(pool)):
    e = R.Engine(en, R.load_repo_arabic(ROOT, pool[:n]))
    rec = sum(1 for w in test if (lambda r: r["accepted"] and r["converted"] == w)(e.evaluate(R.to_english(w), False, 88, False, R.to_english(w)))) / len(test)
    fp = sum(1 for g in gib if e.evaluate(g, False, 88, False, g)["accepted"]) / len(gib)
    print(f"{n:28d} {100*rec:17.1f}% {100*fp:25.1f}%")
