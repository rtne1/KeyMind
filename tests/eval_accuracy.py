"""
A/B accuracy measurement: KeyMind 2.3 (legacy) vs 3.0 (new) decision engines.

Held-out protocol (so we measure generalisation, not dictionary lookup):
  * The Arabic dictionary is split 50/50. Both engines only ever see the TRAIN half.
  * Recall is measured on the TEST half (words the engine has never seen).
  * Precision is measured on words that are already correct in their own layout.

Run:  python3 tests/eval_accuracy.py
"""
import random
import sys
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import reference_engine as R

ROOT = Path(__file__).resolve().parent.parent
rng = random.Random(7)


def split_half(words):
    train, test = [], []
    for w in sorted(set(words)):
        (train if zlib.crc32(w.encode("utf8")) % 2 == 0 else test).append(w)
    return train, test


def typable(w):
    """Word can be produced by physical keys on the Arabic 101 layout (no أ إ آ / tashkeel)."""
    return len(w) >= 3 and all(c in R.AR2EN for c in w)


def main():
    en_new = R.load_repo_english(ROOT)
    ar_all = [w.strip() for w in (ROOT / "data/ar-common.txt").read_text(encoding="utf8").splitlines() if w.strip()]
    train, test = split_half(ar_all)

    common = {w.strip().lower() for w in (ROOT / "data/en-common.txt").read_text(encoding="utf8").splitlines() if len(w.strip()) >= 2}
    ext = {w.strip().lower() for w in (ROOT / "data/en-extended.txt").read_text(encoding="utf8").splitlines() if len(w.strip()) >= 2}
    counts = {}
    for line in (ROOT / "data/en-frequency.txt").read_text(encoding="utf8").splitlines():
        p = line.rsplit(" ", 1)
        if len(p) == 2 and p[1].isdigit():
            counts[p[0].lower()] = int(p[1])

    legacy = R.Legacy(common, ext, counts, train)
    new = R.Engine(en_new, R.load_repo_arabic(ROOT, train))

    # ---- test sets -------------------------------------------------------------------
    # A: Arabic intent typed on the English layout, word UNSEEN by the dictionary  -> should convert
    A = [(R.to_english(w), w) for w in test if typable(w)]
    # B: Arabic intent typed on EN layout, word seen by the dictionary            -> should convert
    B = [(R.to_english(w), w) for w in train if typable(w)]
    # C: English intent typed on the Arabic layout (common-ish words)           -> should convert
    eng_pool = [w for w, c in counts.items() if c >= 3 and 4 <= len(w) <= 9 and w.isalpha()]
    C = [(R.to_arabic(w), w) for w in rng.sample(eng_pool, 3000)]
    # D: correct English typed on the English layout                              -> must NOT change
    D = [(w, None) for w in rng.sample(eng_pool, 3000)]
    # D2: correct English followed by punctuation typed on the English layout     -> must NOT change
    D2 = [(w + rng.choice([",", ".", ";"]), None) for w in rng.sample(eng_pool, 2000)]
    # E: correct Arabic typed on the Arabic layout (train + unseen)               -> must NOT change
    E = [(w, None) for w in set(train + test) if all("\u0621" <= c <= "\u064a" for c in w) and len(w) >= 3]
    # F: keyboard-mash gibberish in either layout                                 -> must NOT change
    letters = "abcdefghijklmnopqrstuvwxyz"
    F_en = [("".join(rng.choice(letters) for _ in range(rng.randint(4, 8))), None) for _ in range(2000)]
    F_ar = [(R.to_arabic("".join(rng.choice(letters) for _ in range(rng.randint(4, 8)))), None) for _ in range(2000)]

    def physical_of(token, src_arabic):
        """The real app records the physical key stream; reproduce it exactly (handles the 'b' -> 'لا' ligature)."""
        if not src_arabic:
            return token.lower()
        keys, i = [], 0
        while i < len(token):
            if token[i:i + 2] == "لا":
                keys.append("b"); i += 2
            elif token[i] in R.AR2EN:
                keys.append(R.AR2EN[token[i]]); i += 1
            else:
                return None
        return "".join(keys)

    def rate(engine, items, src_arabic, expect_convert):
        ok = 0
        for token, target in items:
            # tokens are what appeared on screen; physical keys are what the hook recorded
            if src_arabic:
                phys = physical_of(token, True) if target is None else target  # target == English word typed
            else:
                phys = token.lower()
            r = engine.evaluate(token, src_arabic, 88, False, phys)
            if expect_convert:
                ok += 1 if (r["accepted"] and r["converted"] == target) else 0
            else:
                ok += 1 if not r["accepted"] else 0
        return 100.0 * ok / max(1, len(items))

    rows = [
        ("A  Arabic typed on EN layout, UNSEEN word (recall)", A, False, True),
        ("B  Arabic typed on EN layout, known word  (recall)", B, False, True),
        ("C  English typed on AR layout             (recall)", C, True, True),
        ("D  Correct English left alone         (precision)", D, False, False),
        ("D2 'word,' / 'word.' left alone       (precision)", D2, False, False),
        ("E  Correct Arabic left alone          (precision)", E, True, False),
        ("F1 English-layout gibberish left alone(precision)", F_en, False, False),
        ("F2 Arabic-layout gibberish left alone (precision)", F_ar, True, False),
    ]
    print(f"{'test (higher is better everywhere)':58s} {'n':>5s} {'2.3':>8s} {'3.0':>8s}  {'diff':>6s}")
    print("-" * 90)
    for name, items, src_ar, expect in rows:
        a = rate(legacy, items, src_ar, expect)
        b = rate(new, items, src_ar, expect)
        print(f"{name:58s} {len(items):5d} {a:7.1f}% {b:7.1f}%  {b - a:+6.1f}")
    return legacy, new


if __name__ == "__main__":
    main()
