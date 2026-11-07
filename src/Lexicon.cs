using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace KeyMind
{
    // Compact word set: one sorted 64-bit hash + one frequency-class byte per word (~9 bytes/word).
    // 10 million words ~ 90 MB RAM, binary-search lookup in ~24 steps, loads from .kmd in well under a second.
    // Class byte: 0 = listed (no frequency known), 1..255 = frequency class (255 = most common).
    // Must stay byte-for-byte compatible with tools/build_dictionary.py.
    public sealed class Lexicon
    {
        private readonly ulong[] _keys;
        private readonly byte[] _classes;

        private Lexicon(ulong[] keys, byte[] classes) { _keys = keys; _classes = classes; }
        public int Count { get { return _keys.Length; } }

        // FNV-1a 64-bit over UTF-16 code units. Test vector: Hash("abc") == 16654208175385433931.
        public static ulong Hash(string word)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                for (int i = 0; i < word.Length; i++)
                {
                    h ^= (ulong)word[i];
                    h *= 1099511628211UL;
                }
                return h;
            }
        }

        // Absolute scale, independent of list size: rank 1 -> 255, 1,000 -> 153, 100,000 -> 85, 10,000,000 -> 17.
        public static int RankClass(int rank)
        {
            return Math.Max(1, Math.Min(255, 255 - (int)(34 * Math.Log10(Math.Max(1, rank)))));
        }

        // Lexical strength in points: 0 = unknown, 72 = listed only, 76..100 = by frequency class.
        public static double Strength(int cls)
        {
            if (cls < 0) return 0;
            if (cls == 0) return 72;
            return 76 + cls * 0.094;
        }

        // Returns -1 when absent, otherwise the class byte (0..255).
        public int Find(string word)
        {
            if (_keys.Length == 0 || string.IsNullOrEmpty(word)) return -1;
            int index = Array.BinarySearch(_keys, Hash(word));
            return index >= 0 ? _classes[index] : -1;
        }

        public sealed class Builder
        {
            private sealed class Chunk { public ulong[] Keys; public byte[] Classes; }
            private readonly List<Chunk> _chunks = new List<Chunk>();
            private readonly List<ulong> _keys = new List<ulong>();
            private readonly List<byte> _classes = new List<byte>();

            public void AddWord(string normalizedWord, int cls)
            {
                _keys.Add(Hash(normalizedWord));
                _classes.Add((byte)Math.Max(0, Math.Min(255, cls)));
            }

            public void AddKmd(string path)
            {
                byte[] raw = File.ReadAllBytes(path);
                if (raw.Length < 8 || raw[0] != (byte)'K' || raw[1] != (byte)'M' || raw[2] != (byte)'D' || raw[3] != (byte)'1')
                    throw new InvalidDataException("Not a KMD1 dictionary: " + path);
                int count = BitConverter.ToInt32(raw, 4);
                if (count < 0 || raw.Length < 8L + 9L * count)
                    throw new InvalidDataException("Truncated dictionary: " + path);
                Chunk chunk = new Chunk();
                chunk.Keys = new ulong[count];
                chunk.Classes = new byte[count];
                Buffer.BlockCopy(raw, 8, chunk.Keys, 0, count * 8);
                Buffer.BlockCopy(raw, 8 + count * 8, chunk.Classes, 0, count);
                _chunks.Add(chunk);
            }

            public Lexicon Build()
            {
                int total = _keys.Count;
                foreach (Chunk c in _chunks) total += c.Keys.Length;
                ulong[] keys = new ulong[total];
                byte[] cls = new byte[total];
                int pos = 0;
                foreach (Chunk c in _chunks)
                {
                    Array.Copy(c.Keys, 0, keys, pos, c.Keys.Length);
                    Array.Copy(c.Classes, 0, cls, pos, c.Classes.Length);
                    pos += c.Keys.Length;
                }
                _keys.CopyTo(keys, pos);
                _classes.CopyTo(cls, pos);
                bool sorted = true;
                for (int i = 1; i < total; i++) if (keys[i] < keys[i - 1]) { sorted = false; break; }
                if (!sorted) Array.Sort(keys, cls);
                // Collapse duplicates, keeping the best (highest) class.
                int w = 0;
                for (int i = 0; i < total; i++)
                {
                    if (w > 0 && keys[w - 1] == keys[i]) { if (cls[i] > cls[w - 1]) cls[w - 1] = cls[i]; }
                    else { keys[w] = keys[i]; cls[w] = cls[i]; w++; }
                }
                if (w != total) { Array.Resize(ref keys, w); Array.Resize(ref cls, w); }
                return new Lexicon(keys, cls);
            }
        }
    }

    // Interpolated character trigram/bigram language model with '^^' start and '$' end padding.
    // Only the trigram table is stored (*.ngram); bigram and history counts are derived from it.
    // Used to judge how "language-like" an UNKNOWN word is and to compare the typed text
    // against its converted form (log-likelihood ratio). Mirror of CharLm in tests/reference_engine.py.
    public sealed class CharLm
    {
        private readonly Dictionary<string, int> _c3 = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _c2 = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _c2h = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _c1h = new Dictionary<string, int>();
        private readonly double _vocab;
        public double Floor = -3.5;

        public CharLm(int vocab) { _vocab = vocab; }

        public void AddTrigram(string gram, int n)
        {
            int old;
            _c3.TryGetValue(gram, out old);
            _c3[gram] = old + n;
        }

        public void AddWord(string word)
        {
            string p = "^^" + word + "$";
            for (int i = 2; i < p.Length; i++) AddTrigram(p.Substring(i - 2, 3), 1);
        }

        private static void Inc(Dictionary<string, int> map, string key, int n)
        {
            int old;
            map.TryGetValue(key, out old);
            map[key] = old + n;
        }

        // Call once after all AddWord/AddTrigram calls.
        public void Finish()
        {
            _c2.Clear(); _c2h.Clear(); _c1h.Clear();
            foreach (KeyValuePair<string, int> kv in _c3)
            {
                Inc(_c2h, kv.Key.Substring(0, 2), kv.Value);
                Inc(_c2, kv.Key.Substring(1), kv.Value);
            }
            foreach (KeyValuePair<string, int> kv in _c2)
                Inc(_c1h, kv.Key.Substring(0, 1), kv.Value);
        }

        public double AvgLogProb(string word)
        {
            string p = "^^" + word + "$";
            double total = 0;
            for (int i = 2; i < p.Length; i++)
            {
                int h2, t3, h1, b2;
                _c2h.TryGetValue(p.Substring(i - 2, 2), out h2);
                _c3.TryGetValue(p.Substring(i - 2, 3), out t3);
                double tri = h2 > 0 ? (double)t3 / h2 : 0.0;
                _c1h.TryGetValue(p.Substring(i - 1, 1), out h1);
                _c2.TryGetValue(p.Substring(i - 1, 2), out b2);
                double bi = h1 > 0 ? (double)b2 / h1 : 0.0;
                total += Math.Log(0.55 * tri + 0.35 * bi + 0.10 / _vocab);
            }
            return total / (p.Length - 2);
        }

        // Quality floor: 5th percentile of real words' score minus 0.25.
        public void CalibrateFloor(IEnumerable<string> words)
        {
            List<double> scores = new List<double>();
            foreach (string w in words) if (w.Length >= 4) scores.Add(AvgLogProb(w));
            if (scores.Count == 0) { Floor = -3.5; return; }
            scores.Sort();
            Floor = scores[(int)(scores.Count * 0.05)] - 0.25;
        }
    }

    // Finds and merges every dictionary source for one language:
    //   <app>\data\, <app>\data\extra\, %LOCALAPPDATA%\KeyMind\dictionaries\   files named en-* / ar-*
    //   *.kmd    compiled by tools/build_dictionary.py (fast; use this for millions of words)
    //   *.ngram  trigram model from the same tool
    //   *.txt    "word", or "word count" (frequency); a file name containing "common" is treated as curated.
    internal static class DictionaryLoader
    {
        public const int CuratedEnglishClass = 190;
        public const int CuratedArabicClass = 170;

        public static Lexicon Load(string prefix, bool arabic, out CharLm lm)
        {
            Lexicon.Builder builder = new Lexicon.Builder();
            lm = new CharLm(arabic ? 32 : 28);
            HashSet<string> trained = new HashSet<string>(StringComparer.Ordinal);
            double fileFloor = double.NaN;
            bool anyWord = false;

            List<string> dirs = new List<string>();
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            dirs.Add(baseDir);
            dirs.Add(Path.Combine(baseDir, "extra"));
            dirs.Add(Path.Combine(SettingsStore.SettingsDir, "dictionaries"));

            foreach (string dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                string[] files = Directory.GetFiles(dir, prefix + "-*");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    try
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext == ".kmd") { builder.AddKmd(file); anyWord = true; }
                        else if (ext == ".ngram") LoadNgram(file, lm, ref fileFloor);
                        else if (ext == ".txt") { if (LoadText(file, arabic, builder, trained)) anyWord = true; }
                    }
                    catch (Exception ex)
                    {
                        // One broken file must never disable the whole dictionary.
                        Program.LogError(new Exception("Skipped dictionary file " + Path.GetFileName(file), ex));
                    }
                }
            }
            if (!anyWord) throw new FileNotFoundException("No " + prefix + "-* dictionary files found in " + baseDir);
            foreach (string w in trained) lm.AddWord(w);
            lm.Finish();
            if (!double.IsNaN(fileFloor)) lm.Floor = fileFloor;
            else lm.CalibrateFloor(trained);
            return builder.Build();
        }

        private static void LoadNgram(string path, CharLm lm, ref double floor)
        {
            foreach (string line in File.ReadLines(path, Encoding.UTF8))
            {
                if (line.Length == 0) continue;
                string[] parts = line.Split('\t');
                if (parts.Length != 2) continue;
                if (parts[0] == "#floor")
                {
                    double value;
                    if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                        floor = double.IsNaN(floor) ? value : Math.Min(floor, value);
                    continue;
                }
                int count;
                if (parts[0].Length == 3 && int.TryParse(parts[1], out count)) lm.AddTrigram(parts[0], count);
            }
        }

        // Returns true when at least one word was read.
        private static bool LoadText(string path, bool arabic, Lexicon.Builder builder, HashSet<string> trained)
        {
            string name = Path.GetFileName(path).ToLowerInvariant();
            bool curated = name.IndexOf("common", StringComparison.Ordinal) >= 0;
            int curatedClass = arabic ? CuratedArabicClass : CuratedEnglishClass;
            Dictionary<string, long> counted = new Dictionary<string, long>(StringComparer.Ordinal);
            bool any = false;
            foreach (string raw in File.ReadLines(path, Encoding.UTF8))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                string word = Clean(parts[0].Split('/')[0], arabic);
                if (word == null) continue;
                long count = -1;
                if (parts.Length >= 2 && !curated) { long parsed; if (long.TryParse(parts[parts.Length - 1], out parsed)) count = parsed; }
                any = true;
                if (curated) { builder.AddWord(word, curatedClass); trained.Add(word); }
                else if (count >= 0) { long old; counted.TryGetValue(word, out old); counted[word] = old + count; }
                else builder.AddWord(word, 0);
            }
            if (counted.Count > 0)
            {
                List<KeyValuePair<string, long>> ranked = new List<KeyValuePair<string, long>>(counted);
                ranked.Sort(delegate (KeyValuePair<string, long> a, KeyValuePair<string, long> b)
                {
                    int c = b.Value.CompareTo(a.Value);
                    return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
                });
                for (int i = 0; i < ranked.Count; i++)
                {
                    builder.AddWord(ranked[i].Key, Lexicon.RankClass(i + 1));
                    trained.Add(ranked[i].Key);
                }
            }
            return any;
        }

        // Same rules as clean() in tools/build_dictionary.py.
        private static string Clean(string word, bool arabic)
        {
            if (arabic)
            {
                string n = WordConverter.NormalizeArabic(word.Trim());
                if (n.Length < 2 || n.Length > 30) return null;
                foreach (char c in n) if (c < '\u0621' || c > '\u064A') return null;
                return n;
            }
            string w = word.Trim().ToLowerInvariant();
            if (w.Length < 2 || w.Length > 30) return null;
            foreach (char c in w) if (c < 'a' || c > 'z') return null;
            return w;
        }
    }
}
