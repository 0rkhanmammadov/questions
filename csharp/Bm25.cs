using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace QuizAssistant
{
    public class Bm25Doc
    {
        public string RawQuestion;
        public string Answer;
        public string NormQuestion;
        public string QNum;
        public List<string> Tokens;
        public HashSet<string> TokenSet;
        public Dictionary<string, int> TermFreq;
        public int Length;
    }

    public class Bm25Index
    {
        public double K1 = 1.5;
        public double B = 0.75;
        public List<Bm25Doc> Docs = new List<Bm25Doc>();
        public int N = 0;
        public double Avgdl = 0.0;
        public Dictionary<string, double> Idf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        public void Build(List<Bm25Doc> documents)
        {
            Docs = documents;
            N = documents.Count;
            if (N == 0)
            {
                Avgdl = 0.0;
                Idf.Clear();
                return;
            }

            long totalWords = 0;
            Dictionary<string, int> docFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var doc in documents)
            {
                totalWords += doc.Length;
                foreach (var term in doc.TokenSet)
                {
                    int count;
                    docFreq.TryGetValue(term, out count);
                    docFreq[term] = count + 1;
                }
            }

            Avgdl = (double)totalWords / (double)N;
            Idf.Clear();

            foreach (var kvp in docFreq)
            {
                string term = kvp.Key;
                int df = kvp.Value;
                // Lucene BM25 IDF: ln((N - df + 0.5) / (df + 0.5) + 1.0)
                double idfVal = Math.Log(((double)(N - df) + 0.5) / ((double)df + 0.5) + 1.0);
                Idf[term] = Math.Max(0.01, idfVal);
            }
        }

        public double Score(List<string> queryTokens, Bm25Doc doc)
        {
            if (doc.Length == 0 || Avgdl == 0.0) return 0.0;
            double score = 0.0;

            foreach (string token in queryTokens)
            {
                int tf;
                if (!doc.TermFreq.TryGetValue(token, out tf)) continue;
                double idfVal;
                if (!Idf.TryGetValue(token, out idfVal)) continue;

                double num = (double)tf * (K1 + 1.0);
                double denom = (double)tf + K1 * (1.0 - B + B * ((double)doc.Length / Avgdl));
                score += idfVal * (num / denom);
            }
            return score;
        }
    }

    public static class LocalDb
    {
        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "və", "ve", "ilə", "ile", "bu", "o", "bir", "üçün", "ucun",
            "hansı", "hansi", "nə", "ne", "necə", "nece", "hansıdır", "hansidir",
            "ki", "da", "də", "de", "haqqında", "haqqinda", "vəya", "veya",
            "is", "the", "a", "an", "of", "in", "to", "for", "which", "what"
        };

        private static Bm25Index _cachedIndex = null;
        private static readonly object _indexLock = new object();

        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string cleaned = Regex.Replace(text.ToLowerInvariant(), @"[^\w\s]", " ");
            return Regex.Replace(cleaned, @"\s+", " ").Trim();
        }

        public static string ExtractQNum(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            Match m = Regex.Match(text, @"^\s*(?:Sual\s*|№\s*|#\s*)?(\d{1,3})\s*[\.\)\-:]", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : "";
        }

        public static List<string> Tokenize(string text, bool removeStop = false)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(text)) return tokens;

            MatchCollection matches = Regex.Matches(text.ToLowerInvariant(), @"\w+");
            foreach (Match m in matches)
            {
                string word = m.Value;
                if (removeStop)
                {
                    if (word.Length >= 2 && !StopWords.Contains(word))
                    {
                        tokens.Add(word);
                    }
                }
                else
                {
                    if (word.Length >= 1)
                    {
                        tokens.Add(word);
                    }
                }
            }
            return tokens;
        }

        public static double StringSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;
            if (s1 == s2) return 1.0;

            int n = s1.Length;
            int m = s2.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (s2[j - 1] == s1[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }
            int maxLen = Math.Max(n, m);
            return 1.0 - ((double)d[n, m] / (double)maxLen);
        }

        private static List<Bm25Doc> ParseBazaText(string content)
        {
            List<Bm25Doc> docs = new List<Bm25Doc>();
            if (string.IsNullOrEmpty(content)) return docs;

            string[] blocks = Regex.Split(content, @"\r?\n\s*\r?\n");
            foreach (string block in blocks)
            {
                string trimmed = block.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                // Check pipe format: "Sual | Cavab"
                if (trimmed.Contains("|") && !trimmed.Contains("\n"))
                {
                    string[] parts = trimmed.Split(new char[] { '|' }, 2);
                    string qPart = parts[0].Trim();
                    string aPart = parts[1].Trim();
                    if (!string.IsNullOrEmpty(qPart) && !string.IsNullOrEmpty(aPart))
                    {
                        AddDoc(docs, qPart, aPart);
                        continue;
                    }
                }

                // Block format: check "Cavab:" or "Düzgün cavab:"
                Match ansMatch = Regex.Match(trimmed, @"(?:Düzgün\s+cavab|Cavab|Doğru\s+cavab|Correct\s+answer)\s*[:\-]\s*(.+?)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                if (ansMatch.Success)
                {
                    string qPart = trimmed.Substring(0, ansMatch.Index).Trim();
                    string aPart = ansMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(qPart) && !string.IsNullOrEmpty(aPart))
                    {
                        AddDoc(docs, qPart, aPart);
                        continue;
                    }
                }
            }
            return docs;
        }

        private static void AddDoc(List<Bm25Doc> docs, string question, string answer)
        {
            string normQ = Normalize(question);
            if (string.IsNullOrEmpty(normQ)) return;

            List<string> tokens = Tokenize(question, false);
            List<string> tokensNoStop = Tokenize(question, true);
            List<string> indexTokens = (tokensNoStop.Count >= 2) ? tokensNoStop : tokens;

            Dictionary<string, int> tf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string t in indexTokens)
            {
                int count;
                tf.TryGetValue(t, out count);
                tf[t] = count + 1;
            }

            Bm25Doc doc = new Bm25Doc
            {
                RawQuestion = question,
                Answer = answer,
                NormQuestion = normQ,
                QNum = ExtractQNum(question),
                Tokens = indexTokens,
                TokenSet = new HashSet<string>(indexTokens, StringComparer.OrdinalIgnoreCase),
                TermFreq = tf,
                Length = indexTokens.Count
            };
            docs.Add(doc);
        }

        private static Bm25Index GetOrBuildIndex()
        {
            lock (_indexLock)
            {
                if (_cachedIndex != null) return _cachedIndex;

                string content = "";
                // 1. Check local file next to exe or in temp
                string localFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "baza.txt");
                if (!File.Exists(localFile))
                {
                    localFile = Path.Combine(Path.GetTempPath(), "baza.txt");
                }

                if (File.Exists(localFile))
                {
                    try { content = File.ReadAllText(localFile, Encoding.UTF8); } catch { }
                }

                // 2. Fallback to embedded resource
                if (string.IsNullOrEmpty(content))
                {
                    try
                    {
                        Assembly asm = Assembly.GetExecutingAssembly();
                        using (Stream stream = asm.GetManifestResourceStream("baza.txt"))
                        {
                            if (stream != null)
                            {
                                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                                {
                                    content = reader.ReadToEnd();
                                }
                            }
                        }
                    }
                    catch { }
                }

                List<Bm25Doc> docs = ParseBazaText(content);
                Bm25Index idx = new Bm25Index();
                idx.Build(docs);
                _cachedIndex = idx;
                return _cachedIndex;
            }
        }

        public static string Search(string questionText)
        {
            if (string.IsNullOrEmpty(questionText)) return null;
            string cleanedQ = questionText.Trim();
            string qNorm = Normalize(cleanedQ);
            if (qNorm.Length < 3) return null;

            Bm25Index index = GetOrBuildIndex();
            if (index == null || index.N == 0) return null;

            List<string> qTokens = Tokenize(cleanedQ, false);
            List<string> qTokensNoStop = Tokenize(cleanedQ, true);
            List<string> searchTokens = (qTokensNoStop.Count >= 2) ? qTokensNoStop : qTokens;
            if (searchTokens.Count == 0) searchTokens = qTokens;

            string qNum = ExtractQNum(cleanedQ);
            HashSet<string> querySet = new HashSet<string>(searchTokens, StringComparer.OrdinalIgnoreCase);

            Bm25Doc bestDoc = null;
            double bestScore = 0.0;

            double maxPossibleBm25 = 0.0;
            foreach (string t in querySet)
            {
                double idfVal;
                if (index.Idf.TryGetValue(t, out idfVal)) maxPossibleBm25 += idfVal;
            }

            foreach (var doc in index.Docs)
            {
                string bNorm = doc.NormQuestion;
                if (string.IsNullOrEmpty(bNorm)) continue;

                // 1. Substring / Exact match
                if (bNorm.Contains(qNorm) || qNorm.Contains(bNorm))
                {
                    return doc.Answer;
                }

                // 2. Direct similarity
                double sim = StringSimilarity(qNorm, bNorm);
                if (sim >= 0.82)
                {
                    return doc.Answer;
                }

                // 3. BM25 keyword score
                double bm25Raw = index.Score(searchTokens, doc);
                double normBm25 = (maxPossibleBm25 > 0.0) ? (bm25Raw / maxPossibleBm25) : 0.0;

                // 4. Token overlap
                int overlapCount = 0;
                foreach (string term in doc.TokenSet)
                {
                    if (querySet.Contains(term)) overlapCount++;
                }
                double overlapRatio = (doc.TokenSet.Count > 0) ? ((double)overlapCount / (double)doc.TokenSet.Count) : 0.0;

                // Number bonus
                double numBonus = (!string.IsNullOrEmpty(qNum) && qNum == doc.QNum) ? 0.15 : 0.0;

                double totalScore = (0.50 * normBm25) + (0.35 * overlapRatio) + (0.15 * sim) + numBonus;

                if (overlapRatio >= 0.70 && overlapCount >= 3)
                {
                    totalScore = Math.Max(totalScore, 0.80);
                }

                if (totalScore > bestScore)
                {
                    bestScore = totalScore;
                    bestDoc = doc;
                }
            }

            if (bestDoc != null && bestScore >= 0.58)
            {
                return bestDoc.Answer;
            }

            return null;
        }
    }
}
