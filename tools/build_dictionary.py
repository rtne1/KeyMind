#!/usr/bin/env python3
"""
KeyMind dictionary builder
==========================
Turns ANY pile of word lists / text corpora into two small files KeyMind loads instantly:

    <out>.kmd     sorted 64-bit word hashes + 1 frequency-class byte per word  (~9 bytes/word)
    <out>.ngram   character-trigram model + quality floor (used to judge unknown words)

Size: 10 million words  ->  ~90 MB on disk and in RAM, loaded in well under a second.

Inputs (repeat each option as often as you like; mix freely):
    --counts  FILE   lines like "word 12345" or "word<TAB>12345"  (frequency lists)
    --ordered FILE   one word per line, MOST FREQUENT FIRST       (rank lists, e.g. FrequencyWords)
    --list    FILE   plain word list, no frequency                (dictionaries, Hunspell .dic is OK)
    --corpus  FILE   raw running text; words are counted for you   (Wikipedia/OpenSubtitles/news text)

Examples:
    python tools/build_dictionary.py --lang ar --out data/ar-big \
        --ordered ar_50k.txt --counts ar_full.txt --corpus arwiki.txt --min-count 3
    python tools/build_dictionary.py --lang en --out data/en-big --counts en_full.txt --list words_alpha.txt

Then just drop data/ar-big.kmd / data/ar-big.ngram next to KeyMind.exe (or into
%LOCALAPPDATA%\\KeyMind\\dictionaries\\) and restart KeyMind. No code change needed.
"""
import argparse
import math
import re
import struct
import sys
from array import array
from collections import Counter
from pathlib import Path

try:
    import numpy as np
except ImportError:  # numpy only speeds up the final sort/merge
    np = None

MAGIC = b"KMD1"
FNV_OFFSET = 0xCBF29CE484222325
FNV_PRIME = 0x100000001B3
MASK = (1 << 64) - 1

_AR_STRIP = re.compile("[\u064B-\u0652\u0670\u0640]")
_AR_NORM = str.maketrans({"أ": "ا", "إ": "ا", "آ": "ا", "ٱ": "ا", "ى": "ي", "ة": "ه", "ؤ": "و", "ئ": "ي"})
_AR_OK = re.compile("^[\u0621-\u064a]{2,30}$")
_EN_OK = re.compile("^[a-z]{2,30}$")
_AR_TOKEN = re.compile("[\u0621-\u064a\u064B-\u0652\u0640]+")
_EN_TOKEN = re.compile("[A-Za-z]+")


def normalize_ar(s):
    """Must match WordConverter.NormalizeArabic in C#."""
    return _AR_STRIP.sub("", s).translate(_AR_NORM)


def clean(word, lang):
    if lang == "ar":
        w = normalize_ar(word.strip())
        return w if _AR_OK.match(w) else None
    w = word.strip().lower()
    return w if _EN_OK.match(w) else None


def fnv1a64(word):
    """FNV-1a over UTF-16 code units. Must match Lexicon.Hash in C#."""
    h = FNV_OFFSET
    data = word.encode("utf-16-le")
    for i in range(0, len(data), 2):
        h ^= data[i] | (data[i + 1] << 8)
        h = (h * FNV_PRIME) & MASK
    return h


def rank_class(rank):
    """Absolute scale: independent of list size. Must match Lexicon.RankClass in C#."""
    return max(1, min(255, 255 - int(34 * math.log10(max(1, rank)))))


class Collector:
    def __init__(self, lang, min_count):
        self.lang, self.min_count = lang, min_count
        self.best = {}  # word -> class (max across inputs)

    def add(self, word, cls):
        w = clean(word, self.lang)
        if w is not None and cls >= self.best.get(w, -1):
            self.best[w] = cls

    def add_ranked(self, pairs):
        """pairs: iterable of (word, count). Rank by count, then assign classes."""
        merged = Counter()
        for w, c in pairs:
            w = clean(w, self.lang)
            if w is not None and c >= self.min_count:
                merged[w] += c
        for rank, (w, _) in enumerate(sorted(merged.items(), key=lambda kv: (-kv[1], kv[0])), 1):
            self.add(w, rank_class(rank))


def read_counts(path):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        for line in fh:
            parts = line.replace("\t", " ").split()
            if len(parts) >= 2 and parts[-1].isdigit():
                yield parts[0], int(parts[-1])


def read_ordered(path, min_count_unused=0):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        n = 0
        for line in fh:
            parts = line.split()
            if parts:
                n += 1
                yield parts[0], 10 ** 12 - n  # strictly decreasing synthetic count keeps the file's order


def read_list(path):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        for line in fh:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            # Hunspell .dic: "word/FLAGS"; the first line (a bare number) is skipped by clean()
            yield line.split("/")[0].split()[0]


def read_corpus(path, lang):
    tok = _AR_TOKEN if lang == "ar" else _EN_TOKEN
    counts = Counter()
    with open(path, encoding="utf-8", errors="ignore") as fh:
        for line in fh:
            counts.update(tok.findall(line))
            if len(counts) > 30_000_000:  # keep memory bounded on huge dumps
                counts = Counter({w: c for w, c in counts.items() if c > 1})
    return counts.items()


def build_ngram(words, lang, path):
    """Trigram table + calibrated floor. Matches CharLm in WordConverter/Lexicon (C#)."""
    c3 = Counter()
    for w in words:
        p = "^^" + w + "$"
        for i in range(2, len(p)):
            c3[p[i - 2:i + 1]] += 1
    vocab = 32 if lang == "ar" else 28
    c2h, c2, c1h = Counter(), Counter(), Counter()
    for g, n in c3.items():
        c2h[g[:2]] += n
        c2[g[1:]] += n
    for g, n in c2.items():
        c1h[g[0]] += n

    def avg_lp(w):
        p = "^^" + w + "$"
        t = 0.0
        for i in range(2, len(p)):
            h2 = c2h.get(p[i - 2:i], 0)
            tri = c3.get(p[i - 2:i + 1], 0) / h2 if h2 else 0.0
            h1 = c1h.get(p[i - 1], 0)
            bi = c2.get(p[i - 1:i + 1], 0) / h1 if h1 else 0.0
            t += math.log(0.55 * tri + 0.35 * bi + 0.10 / vocab)
        return t / (len(p) - 2)

    sample = [w for w in words if len(w) >= 4]
    if len(sample) > 200_000:
        step = len(sample) // 200_000
        sample = sample[::step]
    scores = sorted(avg_lp(w) for w in sample)
    floor = scores[int(len(scores) * 0.05)] - 0.25 if scores else -3.5
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("#floor\t%.4f\n" % floor)
        for g, n in sorted(c3.items()):
            fh.write("%s\t%d\n" % (g, n))
    return floor, len(c3)


def write_kmd(best, path):
    n = len(best)
    hashes = array("Q", (fnv1a64(w) for w in best))
    classes = bytes(best.values())
    if np is not None:
        h = np.frombuffer(hashes, dtype=np.uint64)
        c = np.frombuffer(classes, dtype=np.uint8)
        order = np.argsort(h, kind="stable")
        h, c = h[order], c[order]
        first = np.concatenate(([True], h[1:] != h[:-1]))
        starts = np.nonzero(first)[0]
        h_u, c_u = h[starts], np.maximum.reduceat(c, starts)
        body = h_u.astype("<u8").tobytes() + c_u.astype(np.uint8).tobytes()
        count = len(h_u)
    else:
        pairs = {}
        for hv, cv in zip(hashes, classes):
            if cv >= pairs.get(hv, -1):
                pairs[hv] = cv
        keys = sorted(pairs)
        body = struct.pack("<%dQ" % len(keys), *keys) + bytes(pairs[k] for k in keys)
        count = len(keys)
    with open(path, "wb") as fh:
        fh.write(MAGIC + struct.pack("<I", count) + body)
    return count


def read_kmd(path):
    """Returns (sorted hash list, class list). Used by tests and --stats."""
    raw = Path(path).read_bytes()
    assert raw[:4] == MAGIC, "not a KMD1 file"
    (count,) = struct.unpack("<I", raw[4:8])
    hashes = struct.unpack("<%dQ" % count, raw[8:8 + 8 * count])
    classes = raw[8 + 8 * count:8 + 9 * count]
    return hashes, classes


def main(argv=None):
    ap = argparse.ArgumentParser(description="Build KeyMind .kmd/.ngram dictionaries")
    ap.add_argument("--lang", required=True, choices=["ar", "en"])
    ap.add_argument("--out", required=True, help="output path WITHOUT extension")
    ap.add_argument("--counts", action="append", default=[])
    ap.add_argument("--ordered", action="append", default=[])
    ap.add_argument("--list", action="append", default=[])
    ap.add_argument("--corpus", action="append", default=[])
    ap.add_argument("--min-count", type=int, default=2,
                    help="drop ranked/corpus words seen fewer times (kills typos in web crawls). default 2")
    ap.add_argument("--ngram-words", type=int, default=400_000, help="top-N ranked words used to train the n-gram model")
    a = ap.parse_args(argv)

    col = Collector(a.lang, a.min_count)
    for p in a.counts:
        col.add_ranked(read_counts(p))
    for p in a.ordered:
        col.add_ranked(read_ordered(p))
    for p in a.corpus:
        col.add_ranked(read_corpus(p, a.lang))
    for p in a.list:
        for w in read_list(p):
            col.add(w, 0)  # class 0 = "listed, frequency unknown"; never downgrades a ranked word
    if not col.best:
        sys.exit("No usable words found. Check the input files and --lang.")

    out = Path(a.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    count = write_kmd(col.best, str(out) + ".kmd")
    ranked = sorted((w for w, c in col.best.items() if c > 0), key=lambda w: -col.best[w])
    train = ranked[:a.ngram_words] or list(col.best)[:a.ngram_words]
    floor, grams = build_ngram(train, a.lang, str(out) + ".ngram")
    ranked_n = sum(1 for c in col.best.values() if c > 0)
    print("words: %d (ranked %d, list-only %d)  ->  %s.kmd (%.1f MB)" %
          (count, ranked_n, count - ranked_n, out, (12 + 9 * count) / 1e6))
    print("n-gram: %d trigrams, quality floor %.3f  ->  %s.ngram" % (grams, floor, out))


if __name__ == "__main__":
    main()
