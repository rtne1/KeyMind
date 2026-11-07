using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyMind
{
    internal static class SmokeTests
    {
        private static int failed = 0;
        private static void Check(string name, bool condition)
        {
            Console.WriteLine((condition ? "[PASS] " : "[FAIL] ") + name);
            if (!condition) failed++;
        }
        private static void Expected(WordConverter c, string label, string source, bool arabic, string expected)
        {
            var result = c.Evaluate(source, arabic, 88, false);
            Check(label + " => " + expected, result.Accepted && result.Converted == expected);
        }
        private static string Find(string relative)
        {
            string[] roots = { Directory.GetCurrentDirectory(), AppDomain.CurrentDomain.BaseDirectory };
            foreach (string root in roots)
            {
                string path = Path.Combine(root, relative);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        // Replays tests/parity_vectors.tsv (written by tests/make_parity_vectors.py from the Python
        // reference engine). Any disagreement means the C# port drifted from the measured spec.
        private static void Parity(WordConverter c)
        {
            string path = Find(Path.Combine("tests", "parity_vectors.tsv"));
            if (path == null) { Check("parity_vectors.tsv present", false); return; }
            int total = 0, bad = 0;
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n];
                if (line.Length == 0 || line[0] == '#') continue;
                string[] f = line.Split('\t');
                if (f.Length != 6) continue;
                total++;
                bool srcArabic = f[1] == "1", wantAccepted = f[3] == "1";
                int wantConf = int.Parse(f[5]);
                ConversionResult r = c.EvaluatePhysical(f[0], srcArabic, 88, false, f[2]);
                bool borderline = Math.Abs(wantConf - 88) <= 1;   // rounding at exactly the threshold may differ
                bool ok = borderline || (r.Accepted == wantAccepted && (!wantAccepted || r.Converted == f[4]));
                if (ok && wantAccepted && !borderline && Math.Abs(r.Confidence - wantConf) > 1) ok = false;
                if (!ok)
                {
                    bad++;
                    if (bad <= 15) Console.WriteLine("   mismatch at line " + (n + 1) + ": keys=" + f[2] + " expected accepted=" + f[3] + "/" + wantConf + " got accepted=" + r.Accepted + "/" + r.Confidence);
                }
            }
            Check("C# engine == Python spec on " + total + " vectors (" + bad + " mismatches)", total > 100 && bad == 0);
        }

        public static int Main()
        {
            try
            {
                var settings = new KeyMindSettings();
                DateTime started = DateTime.UtcNow;
                var converter = new WordConverter(settings);
                Console.WriteLine("Dictionaries loaded in " + (int)(DateTime.UtcNow - started).TotalMilliseconds + " ms  (EN " + converter.EnglishWordCount + ", AR " + converter.ArabicWordCount + ")");

                // --- layout mapping and core decisions (unchanged from 2.3) ---
                Expected(converter, "local", "مخؤشم", true, "local");
                Expected(converter, "key", "نثغ", true, "key");
                Expected(converter, "python", "حغفاخى", true, "python");
                Expected(converter, "code", "ؤخيث", true, "code");
                Expected(converter, "محمد", "lpl]", false, "محمد");
                Expected(converter, "سلام", "sghl", false, "سلام");
                Check("Preserves valid Arabic", !converter.Evaluate("محمد", true, 88, false).Accepted);
                Check("Preserves valid English", !converter.Evaluate("hello", false, 88, false).Accepted);
                Check("Unknown gibberish stays untouched", !converter.Evaluate("zzzzzz", false, 88, false).Accepted);
                Check("Unknown forced conversion", converter.Evaluate("xxqq", false, 88, true).Accepted);
                int nativeInputSize = Marshal.SizeOf(typeof(WinApi.INPUT));
                Check("Native INPUT layout size", nativeInputSize == (IntPtr.Size == 8 ? 40 : 28));
                Check("Auto evaluation physical Chrome -> Arabic",
                    converter.EvaluatePhysical("lpl]", false, 88, false, "lpl]").Accepted &&
                    converter.EvaluatePhysical("lpl]", false, 88, false, "lpl]").Converted == "محمد");
                Check("Auto evaluation physical Arabic -> English",
                    converter.EvaluatePhysical("مخؤشم", true, 88, false, "local").Accepted &&
                    converter.EvaluatePhysical("مخؤشم", true, 88, false, "local").Converted == "local");
                Check("Physical keys 1", WordConverter.FromPhysicalKeys("local", true) == "local");
                Check("Physical keys 2", WordConverter.FromPhysicalKeys("lpl]", false) == "محمد");
                Check("English dictionary size", converter.EnglishWordCount > 100000);
                Check("Arabic common dictionary size", converter.ArabicWordCount > 1400);
                settings.ExtraEnglish = "neologism";
                converter.SetCustomWords(settings.ExtraEnglish, "");
                Check("Custom word accepted", converter.Evaluate("ىثخمخلهسة", true, 88, false).Accepted);
                converter.SetCustomWords("", "");

                // --- 3.0 additions ---
                Check("Contraction left alone (don't)", !converter.Evaluate("don't", false, 88, false).Accepted);
                Check("English + comma left alone (hello,)", !converter.Evaluate("hello,", false, 88, false).Accepted);
                Check("Normalize strips diacritics", WordConverter.NormalizeArabic("مُحَمَّد") == "محمد");
                Check("Shift+; is punctuation (0xBA)", WordConverter.IsPunctuationKey(0xBA) && !WordConverter.IsPunctuationKey(0x41));
                Check("FNV hash vector (abc)", Lexicon.Hash("abc") == 16654208175385433931UL);
                Check("FNV hash vector (محمد)", Lexicon.Hash(WordConverter.NormalizeArabic("محمد")) == 15506932698883412123UL);
                Check("Rank classes", Lexicon.RankClass(1) == 255 && Lexicon.RankClass(1000) == 153 && Lexicon.RankClass(10000000) == 17);

                string fixture = Find(Path.Combine("tests", "fixtures", "fixture.kmd"));
                if (fixture == null) Check("fixture.kmd present", false);
                else
                {
                    Lexicon.Builder builder = new Lexicon.Builder();
                    builder.AddKmd(fixture);
                    Lexicon lex = builder.Build();
                    Check("Binary .kmd compatible with Python builder",
                        lex.Count == 4 && lex.Find("zebra") == 200 && lex.Find("quokka") == 0 && lex.Find("apple") == 255 && lex.Find("mango") == -1);
                }
                Parity(converter);
            }
            catch (Exception e) { Console.WriteLine("[ERROR] " + e); failed++; }
            Console.WriteLine("Failures: " + failed);
            return failed == 0 ? 0 : 1;
        }
    }
}
