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

        public static string CleanAnswer(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string text = raw.Trim();

            // Match "Cavab: B) ..." or "Doğru cavab: B) ..."
            Match m = Regex.Match(text, @"(?:Cavab|Doğru\s+cavab|Correct\s+answer)\s*[:\-]?\s*(.+?)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success)
            {
                text = m.Groups[1].Value.Trim();
            }

            // Remove markdown asterisks
            text = text.Replace("**", "").Replace("*", "").Trim();

            // Clean lines
            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 0)
            {
                text = string.Join(" ", lines).Trim();
            }

            // Multi-select numbers check FIRST: "1, 3, 4" or "1, 2 və 5" or "1-4-5" -> "1-3-4"
            if (Regex.IsMatch(text, @"^\s*\d{1,2}(?:\s*[,;\-\s]\s*(?:və\s*)?\d{1,2})+\s*$", RegexOptions.IgnoreCase))
            {
                MatchCollection mc = Regex.Matches(text, @"\b\d{1,2}\b");
                List<string> digits = new List<string>();
                foreach (Match dm in mc)
                {
                    digits.Add(dm.Value);
                }
                if (digits.Count >= 2)
                {
                    return string.Join("-", digits.ToArray());
                }
            }

            // Strip leading question numbers e.g. "14) ", "14. "
            text = Regex.Replace(text, @"^\d{1,3}\s*[\.\)\:]\s*", "").Trim();

            // Strip leading option letters e.g. "A) ", "B. ", "C: ", "d - "
            text = Regex.Replace(text, @"^[A-Za-z\u0400-\u04FF]\s*[\.\)\-:]\s*", "").Trim();

            return text;
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

            string model = string.IsNullOrEmpty(config.GeminiModel) ? "gemini-3.5-flash-lite" : config.GeminiModel;
            string host = "generativelanguage.googleapis.com";
            string ip = DohResolver.Resolve(host);

            string url = string.Format("https://{0}/v1beta/models/{1}:generateContent?key={2}", ip, model, apiKey);

            string strictUser = string.Format(
                "Sual:\n{0}\n\nQAYDALAR:\n" +
                "1. Təkseçimlidirsə: QƏTİYYƏN variant hərfi (A, B, C...) yazma, yalnız variantdakı sözü/mətni yaz (məsələn: 'Bakı').\n" +
                "2. Çoxseçimlidirsə (məsələn 1, 2, 3, 4, 5 bəndlərindən bir neçəsi doğrudursa): Yalnız doğru rəqəmləri tire ilə birləşdirərək yaz (məsələn: '1-3-4' və ya '1-2-5'). Heç bir söz və hərf əlavə etmə!\n" +
                "3. Uyğunluqdursa: '1-a, 2-c, 3-b' kimi yaz.\n" +
                "4. Qətiyyən izahat, şərh yazma. Yalnız birbaşa cavabı ver.",
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
                    "generationConfig", new Dictionary<string, object>
                    {
                        { "temperature", 0.0 },
                        { "maxOutputTokens", 200 }
                    }
                }
            };

            JavaScriptSerializer ser = new JavaScriptSerializer();
            string jsonBody = ser.Serialize(payloadObj);

            string responseJson = PostJson(url, host, apiKey, jsonBody);
            string rawAnswer = ExtractGeminiAnswer(responseJson);
            string cleanedAnswer = CleanAnswer(rawAnswer);
            return FormatWithDots(cleanedAnswer, config.DotMode);
        }

        public static string SolveImage(byte[] imageBytes, AppConfig config)
        {
            if (imageBytes == null || imageBytes.Length == 0) throw new Exception("Boş şəkil");

            string apiKey = (config.GeminiApiKey ?? "").Trim();
            if (string.IsNullOrEmpty(apiKey)) throw new Exception("API açarı yoxdur (Ctrl+Shift+S)");

            string model = string.IsNullOrEmpty(config.GeminiModel) ? "gemini-3.5-flash-lite" : config.GeminiModel;
            string host = "generativelanguage.googleapis.com";
            string ip = DohResolver.Resolve(host);

            string url = string.Format("https://{0}/v1beta/models/{1}:generateContent?key={2}", ip, model, apiKey);
            string b64Img = Convert.ToBase64String(imageBytes);

            string userInstruction =
                "Sən imtahan ekspertisən. Şəkildəki sualı və variantları diqqətlə oxu, ən doğru cavabı tap.\n" +
                "QAYDALAR:\n" +
                "1. Təkseçimlidirsə: QƏTİYYƏN variant hərfi (A, B, C...) yazma, yalnız variantdakı sözü/mətni yaz (məsələn: 'Bakı').\n" +
                "2. Çoxseçimlidirsə (bir neçə bənd seçilməlidirsə): Yalnız doğru rəqəmləri tire ilə birləşdirərək yaz (məsələn: '1-3-4' və ya '1-2-5'). Heç bir söz və hərf əlavə etmə!\n" +
                "3. Uyğunluq sualıdırsa: '1-a, 2-c, 3-b' kimi yaz.\n" +
                "4. Qətiyyən əlavə izahat və ya nəzəri şərh yazma. Yalnız cavabı ver!";

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
                                                { "mime_type", "image/png" },
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
                    "generationConfig", new Dictionary<string, object>
                    {
                        { "temperature", 0.0 },
                        { "maxOutputTokens", 200 }
                    }
                }
            };

            JavaScriptSerializer ser = new JavaScriptSerializer();
            string jsonBody = ser.Serialize(payloadObj);

            string responseJson = PostJson(url, host, apiKey, jsonBody);
            string rawAnswer = ExtractGeminiAnswer(responseJson);
            string cleanedAnswer = CleanAnswer(rawAnswer);
            return FormatWithDots(cleanedAnswer, config.DotMode);
        }

        private static string PostJson(string url, string host, string apiKey, string jsonBody)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Host = host;
            req.Headers["x-goog-api-key"] = apiKey;
            req.Timeout = 12000;
            req.ReadWriteTimeout = 12000;

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
            JavaScriptSerializer ser = new JavaScriptSerializer();
            Dictionary<string, object> root = ser.Deserialize<Dictionary<string, object>>(json);
            if (root == null || !root.ContainsKey("candidates"))
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

            if (firstCand == null || !firstCand.ContainsKey("content"))
            {
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
                throw new Exception("Gemini boş cavab verdi");
            }
            return result;
        }
    }
}
