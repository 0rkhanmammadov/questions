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
                    return FormatWithDots(ans + " ★", config.DotMode);
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
                "Sual:\n{0}\n\nQAYDA: Təkcə variant hərfini yazma! Doğru variantdakı tam cümləni / mətni yaz (məsələn: 'B) Bakı şəhəri' və ya 'Bakı şəhəri'). Əlavə izahat, şərh yazma.",
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
                "Sən ali dərəcəli imtahan ekspertisən. Şəkildəki sualı və variantları diqqətlə oxu, ən doğru cavabı tap.\n" +
                "QAYDALAR:\n" +
                "1. Təkcə variant hərfini (A, B, C...) yazma! MÜTLƏQ doğru variantın içindəki tam cümləni/mətni yaz (məsələn: 'B) Bakı şəhəri' və ya 'Bakı şəhəri').\n" +
                "2. Variant hərfləri yoxdursa: Birbaşa doğru variantın tam cümləsini yaz.\n" +
                "3. Çoxseçimlidirsə: Bütün doğru bəndlərin cümlələrini yaz.\n" +
                "4. Qətiyyən əlavə izahat və ya nəzəri şərh yazma. Yalnız doğru variantdakı cümləni ver!";

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
