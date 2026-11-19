using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace QuizAssistant
{
    public static class AiClient
    {
        private static readonly Dictionary<string, string> DotMap = new Dictionary<string, string>
        {
            {"A", "•"},
            {"B", "••"},
            {"C", "•••"},
            {"D", "••••"},
            {"E", "•••••"}
        };

        static AiClient()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls12;
                ServicePointManager.ServerCertificateValidationCallback = ValidateServerCertificate;
            }
            catch { }
        }

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // Allow SSL connection even if routed via direct DoH IP
            return true;
        }

        private const string FallbackModel = "gemini-3.5-flash-lite";

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            return ser;
        }

        private static object[] GetSafetySettings()
        {
            return new object[]
            {
                new Dictionary<string, object> { { "category", "HARM_CATEGORY_HARASSMENT" }, { "threshold", "BLOCK_NONE" } },
                new Dictionary<string, object> { { "category", "HARM_CATEGORY_HATE_SPEECH" }, { "threshold", "BLOCK_NONE" } },
                new Dictionary<string, object> { { "category", "HARM_CATEGORY_SEXUALLY_EXPLICIT" }, { "threshold", "BLOCK_NONE" } },
                new Dictionary<string, object> { { "category", "HARM_CATEGORY_DANGEROUS_CONTENT" }, { "threshold", "BLOCK_NONE" } }
            };
        }

        public static string FormatWithDots(string ans, string dotMode)
        {
            if (string.IsNullOrEmpty(ans)) return "";
            string mode = (dotMode ?? "off").ToLowerInvariant();
            if (mode == "off") return ans;

            Match m = Regex.Match(ans, @"\b([A-E])\b");
            if (!m.Success) return ans;

            string letter = m.Groups[1].Value.ToUpperInvariant();
            string dots;
            if (!DotMap.TryGetValue(letter, out dots)) return ans;

            if (mode == "dots_only") return dots;
            if (mode == "both") return ans + " " + dots;
            return ans;
        }

        private static string NormalizeIeltsToken(string val)
        {
            if (string.IsNullOrEmpty(val)) return "";
            string s = val.Trim();
            string lower = s.ToLowerInvariant();
            if (lower == "true" || lower == "t") return "TRUE";
            if (lower == "false" || lower == "f") return "FALSE";
            if (lower == "not given" || lower == "notgiven" || lower == "ng") return "NOT GIVEN";
            if (lower == "yes" || lower == "y") return "YES";
            if (lower == "no" || lower == "n") return "NO";
            return s;
        }

        public static string CleanAnswer(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string text = raw.Trim();

            // Match "Cavab: B) ..." or "Doğru cavab: B) ..." or "Answer: ..."
            Match m = Regex.Match(text, @"^(?:Cavab|Doğru\s+cavab|Correct\s+answer|Answer)\s*[:\-]?\s*(.+?)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success)
            {
                text = m.Groups[1].Value.Trim();
            }

            // Remove markdown formatting
            text = text.Replace("**", "").Replace("*", "").Replace("`", "").Trim();

            string[] rawLines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> validLines = new List<string>();

            foreach (string rawLine in rawLines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Skip conversational / intro headers
                if (Regex.IsMatch(line, @"^(?:Here\s+(?:are|is)|Answers?|Based\s+on|Sure|According\s+to)\b", RegexOptions.IgnoreCase))
                    continue;

                // Check for question numbering e.g. "1. TRUE", "1) library", "Q1: B", "Question 1: FALSE"
                Match qm = Regex.Match(line, @"^(?:(?:Q|Question)\s*)?(\d{1,2})\s*[\.\)\:\-]\s*(.+)$", RegexOptions.IgnoreCase);
                if (qm.Success)
                {
                    string qNum = qm.Groups[1].Value;
                    string qAns = qm.Groups[2].Value.Trim();
                    qAns = NormalizeIeltsToken(qAns);
                    validLines.Add(string.Format("{0}. {1}", qNum, qAns));
                }
                else
                {
                    // Line without leading number
                    string norm = NormalizeIeltsToken(line);
                    validLines.Add(norm);
                }
            }

            if (validLines.Count == 0) return text;

            // If only 1 line, check multi-select digits or clean up option letters if needed
            if (validLines.Count == 1)
            {
                string single = validLines[0];

                // Multi-select numbers check e.g. "1, 3, 4" -> "1-3-4"
                if (Regex.IsMatch(single, @"^\s*\d{1,2}(?:\s*[,;\-\s]\s*(?:və|and)?\s*\d{1,2})+\s*$", RegexOptions.IgnoreCase))
                {
                    MatchCollection mc = Regex.Matches(single, @"\b\d{1,2}\b");
                    List<string> digits = new List<string>();
                    foreach (Match dm in mc) { digits.Add(dm.Value); }
                    if (digits.Count >= 2) return string.Join("-", digits.ToArray());
                }

                return single;
            }

            // If multiple lines, return them joined by newline for clear multi-line display
            return string.Join(Environment.NewLine, validLines.ToArray());
        }

        public static string SolveText(string questionText, AppConfig config)
        {
            string cleaned = (questionText ?? "").Trim();
            if (string.IsNullOrEmpty(cleaned)) throw new Exception("Boş mətn");

            // 1. Check Local DB (baza.txt)
            if (config.LocalDbEnabled)
            {
                string localHit = LocalDb.Search(cleaned);
                if (!string.IsNullOrEmpty(localHit))
                {
                    string ans = CleanAnswer(localHit);
                    return ans + " ★";
                }
            }

            // 2. Query Gemini API with DoH
            string apiKey = (config.GeminiApiKey ?? "").Trim();
            if (string.IsNullOrEmpty(apiKey)) throw new Exception("API açarı yoxdur (Ctrl+Shift+S)");

            string host = "generativelanguage.googleapis.com";

            string strictUser = string.Format(
                "IELTS QUESTION:\n{0}\n\nRULES:\n" +
                "1. True/False/Not Given: Strictly TRUE, FALSE, or NOT GIVEN.\n" +
                "2. Yes/No/Not Given: Strictly YES, NO, or NOT GIVEN.\n" +
                "3. Sentence/Note Completion: Obey word limit (e.g. ONE WORD ONLY). Copy verbatim words only.\n" +
                "4. Multiple Choice: Output option letter (e.g. B or A, C).\n" +
                "5. If multiple questions, list each on a separate line (e.g. 1. TRUE\\n2. library).\n" +
                "6. No explanations. Direct answers only.",
                cleaned
            );

            var payloadObj = new Dictionary<string, object>
            {
                {
                    "systemInstruction", new Dictionary<string, object>
                    {
                        { "parts", new object[] { new Dictionary<string, object> { { "text", config.SystemPrompt } } } }
                    }
                },
                {
                    "contents", new object[]
                    {
                        new Dictionary<string, object>
                        {
                            { "parts", new object[] { new Dictionary<string, object> { { "text", strictUser } } } }
                        }
                    }
                },
                {
                    "safetySettings", GetSafetySettings()
                }
            };

            string responseJson = CallGeminiWithFallback(host, config.GeminiModel, apiKey, payloadObj);
            string rawAnswer = ExtractGeminiAnswer(responseJson);
            string cleanedAnswer = CleanAnswer(rawAnswer);
            return FormatWithDots(cleanedAnswer, config.DotMode);
        }

        public static string SolveImageAndAudio(byte[] imageBytes, byte[] audioWavBytes, AppConfig config)
        {
            if (audioWavBytes == null || audioWavBytes.Length == 0)
            {
                return SolveImage(imageBytes, config);
            }

            string apiKey = (config.GeminiApiKey ?? "").Trim();
            if (string.IsNullOrEmpty(apiKey)) throw new Exception("API açarı yoxdur (Ctrl+Shift+S)");

            string host = "generativelanguage.googleapis.com";
            string b64Img = (imageBytes != null && imageBytes.Length > 0) ? Convert.ToBase64String(imageBytes) : null;
            string b64Wav = Convert.ToBase64String(audioWavBytes);
            string imgMime = (imageBytes != null && imageBytes.Length > 2 && imageBytes[0] == 0xFF && imageBytes[1] == 0xD8) ? "image/jpeg" : "image/png";

            string userInstruction =
                "IELTS LISTENING & READING TASK:\n" +
                "Listen to the audio and inspect the questions on the screen image.\n" +
                "RULES:\n" +
                "1. True/False/Not Given: Output strictly TRUE, FALSE, or NOT GIVEN.\n" +
                "2. Yes/No/Not Given: Output strictly YES, NO, or NOT GIVEN.\n" +
                "3. Fill-in-the-blanks / Completion: Strictly obey word count (e.g. ONE WORD AND/OR A NUMBER). Extract exact spoken word verbatim. Do not add extra articles.\n" +
                "4. Multiple Choice: Output the option letter (e.g. A, B, C or A, D).\n" +
                "5. If multiple questions are visible, list each on a separate line (e.g. 1. library\\n2. 9:30 AM\\n3. TRUE).\n" +
                "6. Output ONLY direct answers. Zero explanations, no conversational filler.";

            var partsList = new List<object>();
            partsList.Add(new Dictionary<string, object> { { "text", userInstruction } });

            if (!string.IsNullOrEmpty(b64Img))
            {
                partsList.Add(new Dictionary<string, object>
                {
                    {
                        "inline_data", new Dictionary<string, object>
                        {
                            { "mime_type", imgMime },
                            { "data", b64Img }
                        }
                    }
                });
            }

            partsList.Add(new Dictionary<string, object>
            {
                {
                    "inline_data", new Dictionary<string, object>
                    {
                        { "mime_type", "audio/wav" },
                        { "data", b64Wav }
                    }
                }
            });

            var payloadObj = new Dictionary<string, object>
            {
                {
                    "systemInstruction", new Dictionary<string, object>
                    {
                        { "parts", new object[] { new Dictionary<string, object> { { "text", config.SystemPrompt } } } }
                    }
                },
                {
                    "contents", new object[]
                    {
                        new Dictionary<string, object>
                        {
                            { "parts", partsList.ToArray() }
                        }
                    }
                },
                {
                    "safetySettings", GetSafetySettings()
                }
            };

            string responseJson = CallGeminiWithFallback(host, config.GeminiModel, apiKey, payloadObj);
            string rawAnswer = ExtractGeminiAnswer(responseJson);
            string cleanedAnswer = CleanAnswer(rawAnswer);
            return FormatWithDots(cleanedAnswer, config.DotMode);
        }

        public static string SolveImage(byte[] imageBytes, AppConfig config)
        {
            if (imageBytes == null || imageBytes.Length == 0) throw new Exception("Boş şəkil");

            string apiKey = (config.GeminiApiKey ?? "").Trim();
            if (string.IsNullOrEmpty(apiKey)) throw new Exception("API açarı yoxdur (Ctrl+Shift+S)");

            string host = "generativelanguage.googleapis.com";
            string b64Img = Convert.ToBase64String(imageBytes);
            string imgMime = (imageBytes != null && imageBytes.Length > 2 && imageBytes[0] == 0xFF && imageBytes[1] == 0xD8) ? "image/jpeg" : "image/png";

            string userInstruction =
                "IELTS READING TASK:\n" +
                "Read the passage/questions on the screen image carefully and provide the exact answers.\n" +
                "RULES:\n" +
                "1. True/False/Not Given: Output strictly TRUE, FALSE, or NOT GIVEN.\n" +
                "2. Yes/No/Not Given: Output strictly YES, NO, or NOT GIVEN.\n" +
                "3. Sentence/Summary Completion: Strictly obey word count (e.g. ONE WORD ONLY). Copy verbatim from passage. No extra articles.\n" +
                "4. Multiple Choice: Output option letter (e.g. A, B, C or B, D).\n" +
                "5. Matching Headings / Info: Output roman numerals or letters (e.g. iv or C).\n" +
                "6. If multiple questions are visible, list each on a separate line (e.g. 1. TRUE\\n2. NOT GIVEN\\n3. FALSE).\n" +
                "7. Output ONLY direct answers. Zero explanations, no conversational filler.";

            var payloadObj = new Dictionary<string, object>
            {
                {
                    "systemInstruction", new Dictionary<string, object>
                    {
                        { "parts", new object[] { new Dictionary<string, object> { { "text", config.SystemPrompt } } } }
                    }
                },
                {
                    "contents", new object[]
                    {
                        new Dictionary<string, object>
                        {
                            {
                                "parts", new object[]
                                {
                                    new Dictionary<string, object> { { "text", userInstruction } },
                                    new Dictionary<string, object>
                                    {
                                        {
                                            "inline_data", new Dictionary<string, object>
                                            {
                                                { "mime_type", imgMime },
                                                { "data", b64Img }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                {
                    "safetySettings", GetSafetySettings()
                }
            };

            string responseJson = CallGeminiWithFallback(host, config.GeminiModel, apiKey, payloadObj);
            string rawAnswer = ExtractGeminiAnswer(responseJson);
            string cleanedAnswer = CleanAnswer(rawAnswer);
            return FormatWithDots(cleanedAnswer, config.DotMode);
        }

        private static Dictionary<string, object> BuildGenerationConfig(string model)
        {
            var genConfig = new Dictionary<string, object>
            {
                { "temperature", 0.0 },
                { "maxOutputTokens", 2048 }
            };

            if (!string.IsNullOrEmpty(model) && (model.IndexOf("hydrogen", StringComparison.OrdinalIgnoreCase) >= 0 || model.IndexOf("thinking", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                genConfig["thinkingConfig"] = new Dictionary<string, object>
                {
                    { "thinkingBudget", 0 }
                };
            }

            return genConfig;
        }

        private static string CallGeminiWithFallback(string host, string primaryModel, string apiKey, Dictionary<string, object> payloadObj)
        {
            string chosen = string.IsNullOrEmpty(primaryModel) ? FallbackModel : primaryModel.Trim();
            string ip = DohResolver.Resolve(host);
            string primaryUrl = string.Format("https://{0}/v1beta/models/{1}:generateContent?key={2}", ip, chosen, apiKey);

            payloadObj["generationConfig"] = BuildGenerationConfig(chosen);
            string jsonBody = CreateSerializer().Serialize(payloadObj);

            try
            {
                return PostJson(primaryUrl, host, apiKey, jsonBody);
            }
            catch (Exception)
            {
                if (!string.Equals(chosen, FallbackModel, StringComparison.OrdinalIgnoreCase))
                {
                    payloadObj["generationConfig"] = BuildGenerationConfig(FallbackModel);
                    string fallbackJson = CreateSerializer().Serialize(payloadObj);
                    string fallbackUrl = string.Format("https://{0}/v1beta/models/{1}:generateContent?key={2}", ip, FallbackModel, apiKey);
                    return PostJson(fallbackUrl, host, apiKey, fallbackJson);
                }
                throw;
            }
        }

        private static string PostJson(string url, string host, string apiKey, string jsonBody)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Host = host;
            req.Headers["x-goog-api-key"] = apiKey;
            req.Timeout = 15000;
            req.ReadWriteTimeout = 15000;

            byte[] bytes = Encoding.UTF8.GetBytes(jsonBody);
            req.ContentLength = bytes.Length;

            using (Stream reqStream = req.GetRequestStream())
            {
                reqStream.Write(bytes, 0, bytes.Length);
            }

            try
            {
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException wex)
            {
                if (wex.Response != null)
                {
                    using (StreamReader r = new StreamReader(wex.Response.GetResponseStream(), Encoding.UTF8))
                    {
                        string errBody = r.ReadToEnd();
                        throw new Exception("Gemini API Xətası: " + errBody);
                    }
                }
                throw;
            }
        }

        private static string ExtractGeminiAnswer(string json)
        {
            JavaScriptSerializer ser = CreateSerializer();
            Dictionary<string, object> root = ser.Deserialize<Dictionary<string, object>>(json);
            if (root == null)
            {
                throw new Exception("Gemini cavab qaytarmadı (JSON boşdur)");
            }

            if (root.ContainsKey("promptFeedback"))
            {
                Dictionary<string, object> pf = root["promptFeedback"] as Dictionary<string, object>;
                if (pf != null && pf.ContainsKey("blockReason"))
                {
                    throw new Exception("Gemini sualı blokladı: " + pf["blockReason"].ToString());
                }
            }

            if (!root.ContainsKey("candidates"))
            {
                throw new Exception("Gemini cavab qaytarmadı");
            }

            System.Collections.IEnumerable candidates = root["candidates"] as System.Collections.IEnumerable;
            if (candidates == null)
            {
                throw new Exception("Cavab namizədi tapılmadı");
            }

            Dictionary<string, object> firstCand = null;
            foreach (object c in candidates)
            {
                firstCand = c as Dictionary<string, object>;
                break;
            }

            if (firstCand == null)
            {
                throw new Exception("Cavab namizədi boşdur");
            }

            if (!firstCand.ContainsKey("content"))
            {
                if (firstCand.ContainsKey("finishReason"))
                {
                    throw new Exception("Gemini bitmə səbəbi: " + firstCand["finishReason"].ToString());
                }
                throw new Exception("Məzmun boşdur");
            }

            Dictionary<string, object> contentObj = firstCand["content"] as Dictionary<string, object>;
            if (contentObj == null || !contentObj.ContainsKey("parts"))
            {
                throw new Exception("Hissələr tapılmadı");
            }

            System.Collections.IEnumerable parts = contentObj["parts"] as System.Collections.IEnumerable;
            if (parts == null)
            {
                throw new Exception("Mətn hissəsi boşdur");
            }

            StringBuilder sb = new StringBuilder();
            foreach (object p in parts)
            {
                Dictionary<string, object> partDict = p as Dictionary<string, object>;
                if (partDict != null && partDict.ContainsKey("text"))
                {
                    sb.Append(partDict["text"].ToString());
                }
            }

            string result = sb.ToString().Trim();
            if (string.IsNullOrEmpty(result))
            {
                if (firstCand.ContainsKey("finishReason"))
                {
                    throw new Exception("Gemini cavabı: " + firstCand["finishReason"].ToString());
                }
                throw new Exception("Gemini boş cavab verdi");
            }
            return result;
        }
    }
}
