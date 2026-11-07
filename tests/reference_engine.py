"""
Reference model of KeyMind's decision engine (Python mirror of src/WordConverter.cs).

Purpose: the C# engine cannot be executed on Linux, so the *algorithm* is specified and
measured here first (tests/eval_accuracy.py), then ported 1:1 to C#. Keep both in sync.

Two engines live here:
  legacy_*  -> KeyMind 2.3 behaviour (for A/B comparison)
  Engine    -> KeyMind 3.0 behaviour
"""
import math
import re
from collections import Counter
from pathlib import Path

EN_KEYS = "qwertyuiop[]asdfghjkl;'zxcvbnm,./`"
AR_KEYS = "ض ص ث ق ف غ ع ه خ ح ج د ش س ي ب ل ا ت ن م ك ط ئ ء ؤ ر لا ى ة و ز ظ ذ".split()
EN2AR = dict(zip(EN_KEYS, AR_KEYS))
AR2EN = {a: e for e, a in EN2AR.items() if len(a) == 1}

_AR_NORM = str.maketrans({"أ": "ا", "إ": "ا", "آ": "ا", "ٱ": "ا", "ى": "ي", "ة": "ه", "ؤ": "و", "ئ": "ي"})
_AR_STRIP = re.compile("[\u064B-\u0652\u0670\u0640]")


def normalize_ar(s):
    return _AR_STRIP.sub("", s).translate(_AR_NORM)


def to_arabic(keys):
    return "".join(EN2AR[c] for c in keys)


def to_english(text):
    return "".join(AR2EN[c] for c in text)


def rank_class(rank):
    """Frequency rank (1 = most common) -> class 1..255. Absolute scale so it is
    independent of the list size (a 10M list and a 30k list agree on 'common')."""
    return max(1, min(255, 255 - int(34 * math.log10(max(1, rank)))))


def strength_from_class(cls):
    """0 = absent, 72 = listed but no frequency, otherwise 76..100."""
    if cls is None:
        return 0.0
    if cls == 0:
        return 72.0
    return 76.0 + cls * 0.094


# --------------------------------------------------------------------------------------
# Data loading (same files the C# app loads)
# --------------------------------------------------------------------------------------
def read_lines(path):
    return Path(path).read_text(encoding="utf8").splitlines()


_EN_OK = re.compile("^[a-z]{2,30}$")
_AR_OK = re.compile("^[\u0621-\u064a]{2,30}$")


def clean_en(w):
    w = w.strip().lower()
    return w if _EN_OK.match(w) else None


def clean_ar(w):
    w = normalize_ar(w.strip())
    return w if _AR_OK.match(w) else None


def load_repo_english(root):
    """Mirror of DictionaryLoader (C#) for the shipped data/en-*.txt files.
    en-extended = listed only (0); en-frequency = ranked by count; en-common = curated (190)."""
    lex = {}
    for w in read_lines(Path(root) / "data/en-extended.txt"):
        w = w.strip()
        if w.startswith("#"):
            continue
        w = clean_en(w.split("/")[0].split()[0]) if w else None
        if w:
            lex.setdefault(w, 0)
    counted = {}
    for line in read_lines(Path(root) / "data/en-frequency.txt"):
        parts = line.split()
        if len(parts) >= 2 and parts[-1].isdigit():
            w = clean_en(parts[0].split("/")[0])
            if w:
                counted[w] = counted.get(w, 0) + int(parts[-1])
    for rank, (w, _) in enumerate(sorted(counted.items(), key=lambda kv: (-kv[1], kv[0])), 1):
        lex[w] = max(lex.get(w, 0), rank_class(rank))
    for line in read_lines(Path(root) / "data/en-common.txt"):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        w = clean_en(line.split()[0].split("/")[0])
        if w:
            lex[w] = max(lex.get(w, 0), 190)
    return lex


def load_repo_arabic(root, words=None):
    lex = {}
    src = words if words is not None else read_lines(Path(root) / "data/ar-common.txt")
    for line in src:
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        w = clean_ar(line.split()[0].split("/")[0])
        if w:
            lex[w] = 170
    return lex


# --------------------------------------------------------------------------------------
# n-gram plausibility (language-likeness of a character sequence)
# --------------------------------------------------------------------------------------
class CharLm:
    """Interpolated character trigram/bigram model with '^^' start and '$' end padding.
    Only the trigram table is persisted (see *.ngram files); the bigram and history
    counts are *derived* from it, which the C# port does identically."""
    def __init__(self, words=None, trigrams=None, vocab=32):
        self.vocab = vocab
        self.floor = -3.5
        self.c3 = Counter()
        if trigrams is not None:
            self.c3.update(trigrams)
        for w in (words or ()):
            p = "^^" + w + "$"
            for i in range(2, len(p)):
                self.c3[p[i - 2:i + 1]] += 1
        self.c2h, self.c2, self.c1h = Counter(), Counter(), Counter()
        for g, n in self.c3.items():
            self.c2h[g[:2]] += n
            self.c2[g[1:]] += n
        for g, n in self.c2.items():
            self.c1h[g[0]] += n

    def calibrate_floor(self, words, quantile=0.05, margin=0.25):
        """Absolute quality floor: 5th percentile of real words' score, minus a margin.
        Computed by the dictionary builder and shipped in the *.ngram header (#floor)."""
        scores = sorted(self.avg_logprob(w) for w in words if len(w) >= 4)
        self.floor = scores[int(len(scores) * quantile)] - margin if scores else -3.5
        return self.floor

    def avg_logprob(self, w):
        p = "^^" + w + "$"
        total = 0.0
        for i in range(2, len(p)):
            h2 = self.c2h.get(p[i - 2:i], 0)
            t = self.c3.get(p[i - 2:i + 1], 0) / h2 if h2 else 0.0
            h1 = self.c1h.get(p[i - 1], 0)
            bg = self.c2.get(p[i - 1:i + 1], 0) / h1 if h1 else 0.0
            total += math.log(0.55 * t + 0.35 * bg + 0.10 / self.vocab)
        return total / (len(p) - 2)


# --------------------------------------------------------------------------------------
# Morphology
# --------------------------------------------------------------------------------------
AR_PREFIXES = ["وبال", "ولل", "وال", "فال", "بال", "كال", "وب", "ول", "وس", "فب", "لل", "ال",
               "و", "ف", "ب", "ل", "ك", "س"]
AR_SUFFIXES = ["هما", "كما", "تين", "تان", "هم", "هن", "كم", "كن", "ها", "نا", "ني", "ون", "ين", "ات", "ان",
               "ه", "ي", "ك"]
STAT_NEED_BASE = 2.0
EN_CONTRACTION_SUFFIX = {"s", "t", "re", "ll", "ve", "m", "d"}
EN_NT_STEMS = {"don", "doesn", "didn", "isn", "aren", "wasn", "weren", "hasn", "haven", "hadn", "won",
               "wouldn", "couldn", "shouldn", "can", "ain", "mustn", "needn"}


class Engine:
    def __init__(self, en_lex, ar_lex, en_ngram_words=None, ar_ngram_words=None):
        self.en, self.ar = en_lex, ar_lex
        self.en_lm = CharLm(en_ngram_words if en_ngram_words is not None else [w for w, c in en_lex.items() if c > 0], vocab=28)
        self.ar_lm = CharLm(ar_ngram_words if ar_ngram_words is not None else [w for w, c in ar_lex.items() if c > 0], vocab=32)
        self.en_lm.calibrate_floor([w for w, c in en_lex.items() if c > 0])
        self.ar_lm.calibrate_floor([w for w, c in ar_lex.items() if c > 0])
        self.custom_en, self.custom_ar = set(), set()

    # ---- lexical strength: (strength 0..100, listed_only?) ----
    def english_strength(self, w):
        w = w.lower()
        if w in self.custom_en:
            return 100.0, False
        if "'" in w:
            stem, _, suf = w.partition("'")
            if suf in EN_CONTRACTION_SUFFIX and stem.isalpha():
                if stem in self.en or (suf == "t" and stem in EN_NT_STEMS):
                    return 90.0, False
            return 0.0, False
        if len(w) >= 4 and w[-1] in ",.;/":
            # "hello," : the , . ; / keys are Arabic letters, but after a real English word they are punctuation
            core = w.rstrip(",.;/")
            if len(core) >= 3 and core.isalpha():
                return self.english_strength(core)
            return 0.0, False
        c = self.en.get(w)
        if c is not None:
            return strength_from_class(c), c == 0
        best = 0.0
        for stem in self._english_stems(w):
            c = self.en.get(stem)
            if c is not None:
                best = max(best, min(84.0, strength_from_class(c) - 2))
        return best, False

    @staticmethod
    def _english_stems(w):
        out = []
        for suf, add in (("ies", "y"), ("es", ""), ("s", ""), ("ed", ""), ("ed", "e"), ("ing", ""),
                         ("ing", "e"), ("ly", ""), ("er", ""), ("est", ""), ("ers", "")):
            if w.endswith(suf) and len(w) - len(suf) >= 3:
                stem = w[:-len(suf)]
                out.append(stem + add)
                if len(stem) >= 4 and stem[-1] == stem[-2]:
                    out.append(stem[:-1])
        return out

    def arabic_strength(self, w):
        n = normalize_ar(w)
        if n in self.custom_ar:
            return 100.0, False
        c = self.ar.get(n)
        if c is not None:
            return strength_from_class(c), c == 0
        best = 0.0
        for pre in [""] + AR_PREFIXES:
            if pre and not (n.startswith(pre) and len(n) >= len(pre) + 3):
                continue
            bare = n[len(pre):]
            for suf in [""] + AR_SUFFIXES:
                if suf and not (bare.endswith(suf) and len(bare) > len(suf) + 2):
                    continue
                stem = bare[:len(bare) - len(suf)] if suf else bare
                if not pre and not suf:
                    continue
                c = self.ar.get(stem)
                if c is not None:
                    best = max(best, min(86.0, strength_from_class(c) - 3))
        return best, False

    # ---- decision ----
    def evaluate(self, token, src_arabic, threshold=88, force=False, physical=None):
        res = dict(original=token, converted=token, confidence=0, accepted=False)
        if not token or len(token) > 55 or len(token) < (1 if force else 3):
            return res
        if src_arabic:
            if not all("\u0621" <= c <= "\u064a" for c in token):
                return res
            keys = physical if physical else "".join(AR2EN.get(c, "?") for c in token)
            if "?" in keys:
                return res
            converted = keys
        else:
            if not all(("a" <= c.lower() <= "z") or c in "[];',./`" for c in token):
                return res
            keys = (physical or token).lower()
            converted = to_arabic(keys)
        if converted == token:
            return res
        tgt_is_english = src_arabic
        if tgt_is_english and not re.fullmatch("[a-z]+", converted):
            if not force:
                return res
        src_s, src_listed = (self.arabic_strength if src_arabic else self.english_strength)(token)
        tgt_s, _ = (self.english_strength if tgt_is_english else self.arabic_strength)(converted)
        src_lm, tgt_lm = (self.ar_lm, self.en_lm) if src_arabic else (self.en_lm, self.ar_lm)
        src_form = normalize_ar(token) if src_arabic else token.lower()
        tgt_form = converted if tgt_is_english else normalize_ar(converted)
        # Log-likelihood ratio per character: how much better does the converted text fit its
        # language than the typed text fits its own?  >0 favours converting.
        delta = tgt_lm.avg_logprob(tgt_form) - src_lm.avg_logprob(src_form)
        if not force and src_s >= 90:
            res.update(converted=converted, confidence=10)
            return res
        pen = 0.0 if src_s == 0 else (4.0 if src_listed else max(0.0, src_s - 70))
        if tgt_s > 0:
            conf = tgt_s + max(-6.0, min(12.0, 3.0 * delta)) + (6.0 if src_s == 0 else 0.0) - pen
        elif len(converted) >= 4 and tgt_lm.avg_logprob(tgt_form) >= tgt_lm.floor:
            need = STAT_NEED_BASE - 0.1 * min(len(converted), 8)
            conf = min(94.0, 88.0 + 14.0 * (delta - need)) - pen
        else:
            conf = 0.0
        conf = max(0, min(99, int(round(conf))))
        res.update(converted=converted, confidence=conf, accepted=force or conf >= threshold)
        return res


# --------------------------------------------------------------------------------------
# Legacy 2.3 engine, kept verbatim in behaviour for A/B measurement
# --------------------------------------------------------------------------------------
class Legacy:
    """KeyMind 2.3 logic, faithful to src/WordConverter.cs (v2.3)."""
    def __init__(self, common, extended, counts, ar_words_raw):
        self.common, self.ext, self.counts = set(common), set(extended), dict(counts)
        self.known_en = self.common | self.ext | set(self.counts)
        self.ar = set(ar_words_raw) | {normalize_ar(w) for w in ar_words_raw}
        self.ar_bi = Counter()
        for w in self.ar:
            n = normalize_ar(w)
            for i in range(len(n) - 1):
                self.ar_bi[n[i:i + 2]] += 1
        self.en_tri = Counter()
        for w, c in self.counts.items():
            weight = min(20, max(1, c))
            for i in range(len(w) - 2):
                self.en_tri[w[i:i + 3]] += weight

    def _known_ar(self, text):
        w = normalize_ar(text)
        if text in self.ar or w in self.ar:
            return True
        pres = ["وال", "بال", "كال", "فال", "لل", "ال", "و", "ف", "ب", "ل", "ك"]
        sufs = ["هما", "هم", "هن", "كما", "كم", "كن", "ها", "نا", "ني", "ه", "ي", "ك", "ات", "ون", "ين"]
        for p in pres:
            if not w.startswith(p) or len(w) < len(p) + 3:
                continue
            bare = w[len(p):]
            if bare in self.ar:
                return True
            for s in sufs:
                if bare.endswith(s) and len(bare) > len(s) + 2 and bare[:-len(s)] in self.ar:
                    return True
        for s in sufs:
            if w.endswith(s) and len(w) > len(s) + 2 and w[:-len(s)] in self.ar:
                return True
        return False

    def _ar_bigram_ratio(self, word):
        n = normalize_ar(word)
        if len(n) < 3:
            return 0
        good = sum(1 for i in range(len(n) - 1) if self.ar_bi.get(n[i:i + 2], 0) >= 2)
        return good / (len(n) - 1)

    def _en_trigram_ratio(self, word):
        if len(word) < 4:
            return 0
        good = sum(1 for i in range(len(word) - 2) if self.en_tri.get(word[i:i + 3], 0) > 10)
        return good / (len(word) - 2)

    def _en_conf(self, word, orig_ar):
        if word in self.common:
            return 99
        c = self.counts.get(word)
        if c is not None:
            if c >= 250: return 98
            if c >= 50: return 96
            if c >= 10: return 92
            if c >= 3: return 88
            return 90 if (len(word) >= 6 and self._ar_bigram_ratio(orig_ar) < 0.82) else (85 if len(word) >= 6 else 78)
        if word in self.ext:
            return 90 if (len(word) >= 5 and self._ar_bigram_ratio(orig_ar) < 0.82) else (85 if len(word) >= 7 else 78)
        if len(word) >= 6 and self._en_trigram_ratio(word) > 0.91 and self._ar_bigram_ratio(orig_ar) < 0.72:
            return 88
        return 40

    def _ar_conf(self, word, orig_en):
        if self._known_ar(word):
            return 98
        if len(word) < 4:
            return 40
        r = self._ar_bigram_ratio(word)
        if r > 0.94 and self._en_trigram_ratio(orig_en) < 0.60:
            return 88
        return 82 if r > 0.88 else (75 if r > 0.70 else 45)

    def evaluate(self, token, src_arabic, threshold=88, force=False, physical=None):
        res = dict(original=token, converted=token, confidence=0, accepted=False)
        if not token or len(token) > 55 or len(token) < (1 if force else 3):
            return res
        if src_arabic:
            if not all("\u0621" <= c <= "\u064a" for c in token):
                return res
            converted = physical if physical else "".join(AR2EN.get(c, "?") for c in token)
            if "?" in converted:
                return res
        else:
            if not all(("a" <= c.lower() <= "z") or c in "[];',./`" for c in token):
                return res
            converted = to_arabic((physical or token).lower())
        if converted == token:
            return res
        if not force and ((src_arabic and self._known_ar(token)) or (not src_arabic and token.lower() in self.known_en)):
            return res
        if src_arabic:
            if not re.fullmatch("[a-z]+", converted):
                if not force:
                    return res
                conf = 68
            else:
                conf = self._en_conf(converted, token)
        else:
            conf = self._ar_conf(converted, token)
        res.update(converted=converted, confidence=conf, accepted=force or conf >= threshold)
        return res
