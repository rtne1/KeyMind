using System;
using System.Collections.Generic;
using System.Text;

namespace KeyMind
{
    public sealed class ConversionResult
    {
        public string Original { get; set; }
        public string Converted { get; set; }
        public string Reason { get; set; }
        public string TargetLanguage { get; set; }
        public int Confidence { get; set; }
        public bool Accepted { get; set; }
    }

    // KeyMind 3.0 decision engine. Specified and measured in tests/reference_engine.py
    // (Python mirror); keep the two in sync. tests/parity_vectors.tsv checks they agree.
    //
    // Idea: the physical key stream is exact, so we always know BOTH readings of what the user typed
    // (as shown on screen, and as the other layout would have produced). We score both:
    //   * lexical strength - is the word in the dictionary (and how common)? clitics/inflections too
    //   * language likelihood ratio - does the converted text fit its language better than the typed
    //     text fits its own?  This is what lets KeyMind fix words that are NOT in any dictionary.
    public sealed class WordConverter
    {
        private const string EnglishKeys = "qwertyuiop[]asdfghjkl;'zxcvbnm,./`";
        private static readonly string[] ArabicKeys = {
            "ض","ص","ث","ق","ف","غ","ع","ه","خ","ح","ج","د",
            "ش","س","ي","ب","ل","ا","ت","ن","م","ك","ط",
            "ئ","ء","ؤ","ر","لا","ى","ة","و","ز","ظ","ذ"
        };
        private static readonly string[] ArabicPrefixes = {
            "وبال","ولل","وال","فال","بال","كال","وب","ول","وس","فب","لل","ال","و","ف","ب","ل","ك","س"
        };
        private static readonly string[] ArabicSuffixes = {
            "هما","كما","تين","تان","هم","هن","كم","كن","ها","نا","ني","ون","ين","ات","ان","ه","ي","ك"
        };
        private static readonly string[] ContractionSuffixes = { "s", "t", "re", "ll", "ve", "m", "d" };
        private static readonly string[] NotStems = {
            "don","doesn","didn","isn","aren","wasn","weren","hasn","haven","hadn","won",
            "wouldn","couldn","shouldn","can","ain","mustn","needn"
        };
        private static readonly string[] EnglishSuffixes = { "ies", "es", "s", "ed", "ed", "ing", "ing", "ly", "er", "est", "ers" };
        private static readonly string[] EnglishRestore = { "y", "", "", "", "e", "", "e", "", "", "", "" };

        // Statistical-only conversions (target word not in any dictionary) need
        // delta >= StatNeedBase - 0.1 * min(length, 8).  Calibrated in tests/eval_accuracy.py.
        private const double StatNeedBase = 2.0;

        private static readonly Dictionary<char, string> EnToAr = new Dictionary<char, string>();
        private static readonly Dictionary<char, char> ArToEn = new Dictionary<char, char>();

        private Lexicon _english, _arabic;
        private CharLm _englishLm, _arabicLm;
        private readonly HashSet<string> _customEnglish = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _customArabic = new HashSet<string>(StringComparer.Ordinal);
        public int EnglishWordCount { get { return _english.Count; } }
        public int ArabicWordCount { get { return _arabic.Count; } }

        static WordConverter()
        {
            if (EnglishKeys.Length != ArabicKeys.Length) throw new InvalidOperationException("Keyboard layout map is invalid.");
            for (int i = 0; i < EnglishKeys.Length; i++)
            {
                EnToAr[EnglishKeys[i]] = ArabicKeys[i];
                if (ArabicKeys[i].Length == 1) ArToEn[ArabicKeys[i][0]] = EnglishKeys[i];
            }
        }

        public WordConverter(KeyMindSettings settings)
        {
            _english = DictionaryLoader.Load("en", false, out _englishLm);
            _arabic = DictionaryLoader.Load("ar", true, out _arabicLm);
            SetCustomWords(settings.ExtraEnglish, settings.ExtraArabic);
        }

        public void SetCustomWords(string english, string arabic)
        {
            _customEnglish.Clear(); _customArabic.Clear();
            char[] separators = { ' ', ',', '،', '\r', '\n', ';', '\t' };
            foreach (string word in (english ?? "").Split(separators, StringSplitOptions.RemoveEmptyEntries))
                _customEnglish.Add(word.ToLowerInvariant());
            foreach (string word in (arabic ?? "").Split(separators, StringSplitOptions.RemoveEmptyEntries))
                _customArabic.Add(NormalizeArabic(word));
        }

        public static string KeyForVirtualKey(uint vk)
        {
            if (vk >= 0x41 && vk <= 0x5a) return ((char)('a' + (vk - 0x41))).ToString();
            switch (vk)
            {
                case 0xDB: return "["; case 0xDD: return "]";
                case 0xBA: return ";"; case 0xDE: return "'";
                case 0xBC: return ","; case 0xBE: return ".";
                case 0xBF: return "/"; case 0xC0: return "`";
                default: return null;
            }
        }
        // True for the eight punctuation keys that are letters on the Arabic layout.
        public static bool IsPunctuationKey(uint vk)
        {
            string key = KeyForVirtualKey(vk);
            return key != null && !(key[0] >= 'a' && key[0] <= 'z');
        }
        public static string CharacterForKey(uint vk, bool arabic)
        {
            string physical = KeyForVirtualKey(vk);
            if (physical == null || !arabic) return physical;
            string output;
            return EnToAr.TryGetValue(physical[0], out output) ? output : null;
        }
        public static string MapCharacters(string text, bool sourceArabic)
        {
            if (text == null) return null;
            StringBuilder mapped = new StringBuilder();
            foreach (char c in text.ToLowerInvariant())
            {
                if (!sourceArabic)
                {
                    string ch;
                    if (!EnToAr.TryGetValue(c, out ch)) return null;
                    mapped.Append(ch);
                }
                else
                {
                    // Text-only conversion is ambiguous for 'لا'. Physical key capture is exact.
                    char key;
                    if (!ArToEn.TryGetValue(c, out key)) return null;
                    mapped.Append(key);
                }
            }
            return mapped.ToString();
        }
        public static string FromPhysicalKeys(string keys, bool sourceArabic)
        {
            if (keys == null) return null;
            if (sourceArabic) return keys.ToLowerInvariant();
            return MapCharacters(keys, false);
        }
        public ConversionResult Evaluate(string token, bool sourceArabic, int threshold, bool force)
        {
            return EvaluatePhysical(token, sourceArabic, threshold, force, null);
        }

        public ConversionResult EvaluatePhysical(string token, bool sourceArabic, int threshold, bool force, string physicalKeys)
        {
            ConversionResult result = new ConversionResult {
                Original = token ?? "", Converted = token ?? "", Confidence = 0,
                TargetLanguage = sourceArabic ? "English" : "العربية",
                Reason = "لم يتم العثور على تحويل موثوق", Accepted = false
            };
            if (string.IsNullOrEmpty(token) || token.Length > 55 || token.Length < (force ? 1 : 3)) return result;
            if (!AllInLanguage(token, sourceArabic))
            {
                result.Reason = "النص يحتوي أحرفاً من أكثر من لغة أو رموزاً";
                return result;
            }
            string converted = physicalKeys != null && physicalKeys.Length > 0
                ? FromPhysicalKeys(physicalKeys, sourceArabic) : MapCharacters(token, sourceArabic);
            if (string.IsNullOrEmpty(converted) || converted == token || converted.Length > 55) return result;

            bool targetEnglish = sourceArabic;
            if (targetEnglish && !AllEnglishLetters(converted))
            {
                // Digits/symbols on the English side are only converted by the explicit force shortcut.
                if (!force) return result;
                result.Converted = converted; result.Confidence = 68; result.Accepted = true;
                result.Reason = "تحويل حرفي حسب مواقع أزرار لوحة المفاتيح";
                return result;
            }

            bool srcListed, tgtListed;
            double srcStrength = sourceArabic ? ArabicStrength(token, out srcListed) : EnglishStrength(token, out srcListed);
            double tgtStrength = targetEnglish ? EnglishStrength(converted, out tgtListed) : ArabicStrength(converted, out tgtListed);
            CharLm srcLm = sourceArabic ? _arabicLm : _englishLm;
            CharLm tgtLm = targetEnglish ? _englishLm : _arabicLm;
            string srcForm = sourceArabic ? NormalizeArabic(token) : token.ToLowerInvariant();
            string tgtForm = targetEnglish ? converted : NormalizeArabic(converted);
            // Per-character log-likelihood ratio: how much better does the converted text fit its
            // language than the typed text fits its own? Positive favours converting.
            double tgtScore = tgtLm.AvgLogProb(tgtForm);
            double delta = tgtScore - srcLm.AvgLogProb(srcForm);

            // A common word that is correct as typed is never touched automatically.
            if (!force && srcStrength >= 90)
            {
                result.Reason = "الكلمة الأصلية صحيحة؛ تُركت دون تعديل";
                return result;
            }
            double penalty = srcStrength <= 0 ? 0 : (srcListed ? 4 : Math.Max(0, srcStrength - 70));
            double conf;
            if (tgtStrength > 0)
                conf = tgtStrength + Math.Max(-6, Math.Min(12, 3 * delta)) + (srcStrength <= 0 ? 6 : 0) - penalty;
            else if (converted.Length >= 4 && tgtScore >= tgtLm.Floor)
            {
                double need = StatNeedBase - 0.1 * Math.Min(converted.Length, 8);
                conf = Math.Min(94, 88 + 14 * (delta - need)) - penalty;
            }
            else conf = 0;

            result.Converted = converted;
            result.Confidence = (int)Math.Max(0, Math.Min(99, Math.Round(conf)));
            result.Accepted = force || result.Confidence >= threshold;
            result.Reason = force ? "تحويل حرفي حسب مواقع أزرار لوحة المفاتيح" :
                (result.Accepted ? "تطابق قوي مع اللغة الأخرى" : "اقتراح غير مؤكد؛ لن يُطبق تلقائياً");
            return result;
        }

        // ---- lexical strength: 0 = unknown, 72 = listed only, 76..100 by frequency ----
        private double EnglishStrength(string word, out bool listedOnly)
        {
            listedOnly = false;
            string w = word.ToLowerInvariant();
            if (_customEnglish.Contains(w)) return 100;
            int apostrophe = w.IndexOf('\'');
            if (apostrophe >= 0)
            {
                string stem = w.Substring(0, apostrophe), suffix = w.Substring(apostrophe + 1);
                if (stem.Length > 0 && AllLowerLetters(stem) && Array.IndexOf(ContractionSuffixes, suffix) >= 0 &&
                    (_english.Find(stem) >= 0 || (suffix == "t" && Array.IndexOf(NotStems, stem) >= 0)))
                    return 90;
                return 0;
            }
            if (w.Length >= 4 && ",.;/".IndexOf(w[w.Length - 1]) >= 0)
            {
                // "hello," - after a real English word the , . ; / keys are punctuation, not Arabic letters.
                string core = w.TrimEnd(',', '.', ';', '/');
                if (core.Length >= 3 && AllLowerLetters(core)) return EnglishStrength(core, out listedOnly);
                return 0;
            }
            int cls = _english.Find(w);
            if (cls >= 0) { listedOnly = cls == 0; return Lexicon.Strength(cls); }
            double best = 0;
            for (int i = 0; i < EnglishSuffixes.Length; i++)
            {
                string suf = EnglishSuffixes[i];
                if (!w.EndsWith(suf, StringComparison.Ordinal) || w.Length - suf.Length < 3) continue;
                string stem = w.Substring(0, w.Length - suf.Length);
                best = Math.Max(best, StemStrength(stem + EnglishRestore[i]));
                if (stem.Length >= 4 && stem[stem.Length - 1] == stem[stem.Length - 2])
                    best = Math.Max(best, StemStrength(stem.Substring(0, stem.Length - 1)));
            }
            return best;
        }
        private double StemStrength(string stem)
        {
            int c = _english.Find(stem);
            return c >= 0 ? Math.Min(84, Lexicon.Strength(c) - 2) : 0;
        }

        private double ArabicStrength(string word, out bool listedOnly)
        {
            listedOnly = false;
            string n = NormalizeArabic(word);
            if (_customArabic.Contains(n)) return 100;
            int cls = _arabic.Find(n);
            if (cls >= 0) { listedOnly = cls == 0; return Lexicon.Strength(cls); }
            double best = 0;
            // Arabic attaches conjunctions/prepositions/article in front and pronouns behind the stem.
            for (int p = -1; p < ArabicPrefixes.Length; p++)
            {
                string pre = p < 0 ? "" : ArabicPrefixes[p];
                if (pre.Length > 0 && !(n.StartsWith(pre, StringComparison.Ordinal) && n.Length >= pre.Length + 3)) continue;
                string bare = n.Substring(pre.Length);
                for (int s = -1; s < ArabicSuffixes.Length; s++)
                {
                    string suf = s < 0 ? "" : ArabicSuffixes[s];
                    if (suf.Length > 0 && !(bare.EndsWith(suf, StringComparison.Ordinal) && bare.Length > suf.Length + 2)) continue;
                    if (pre.Length == 0 && suf.Length == 0) continue;
                    string stem = suf.Length > 0 ? bare.Substring(0, bare.Length - suf.Length) : bare;
                    int c = _arabic.Find(stem);
                    if (c >= 0) best = Math.Max(best, Math.Min(86, Lexicon.Strength(c) - 3));
                }
            }
            return best;
        }

        private static bool AllLowerLetters(string s)
        {
            foreach (char c in s) if (c < 'a' || c > 'z') return false;
            return s.Length > 0;
        }
        private static bool AllEnglishLetters(string s)
        {
            foreach (char c in s) if (c < 'a' || c > 'z') return false;
            return true;
        }
        private static bool AllInLanguage(string s, bool arabic)
        {
            foreach (char c in s)
            {
                if (arabic)
                {
                    if (c < '\u0621' || c > '\u064A') return false;
                }
                else if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || "[];',./`".IndexOf(c) >= 0)) return false;
            }
            return true;
        }

        // Drops diacritics and tatweel, folds alef/hamza/ya/ta-marbuta variants. Must match normalize_ar()
        // in tools/build_dictionary.py.
        public static string NormalizeArabic(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if ((c >= '\u064B' && c <= '\u0652') || c == '\u0670' || c == '\u0640') continue;
                switch (c)
                {
                    case 'أ': case 'إ': case 'آ': case 'ٱ': sb.Append('ا'); break;
                    case 'ى': sb.Append('ي'); break;
                    case 'ة': sb.Append('ه'); break;
                    case 'ؤ': sb.Append('و'); break;
                    case 'ئ': sb.Append('ي'); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
