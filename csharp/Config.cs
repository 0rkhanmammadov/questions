using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace QuizAssistant
{
    public class AppConfig
    {
        private static readonly string BuiltinKey = Encoding.ASCII.GetString(
            Convert.FromBase64String("QVEuQWI4Uk42SlN1X0xuZGxZMUNxeEhIYm1ySjY5QlgxSkpsaGxZQVVTY1dNbzMwTm1IM3c=")
        );

        public string Provider { get; set; }
        public string GeminiApiKey { get; set; }
        public string GeminiModel { get; set; }
        public double Opacity { get; set; }
        public int FontSize { get; set; }
        public string PositionMode { get; set; }
        public string DotMode { get; set; }
        public bool LocalDbEnabled { get; set; }
        public string SystemPrompt { get; set; }

        public AppConfig()
        {
            Provider = "gemini";
            GeminiApiKey = BuiltinKey;
            GeminiModel = "gemini-4-hydrogen";
            Opacity = 0.82;
            FontSize = 10;
            PositionMode = "cursor";
            DotMode = "off";
            LocalDbEnabled = true;
            SystemPrompt = "You are an expert IELTS examiner and solver for Academic and General Training.\n" +
                "RULES FOR READING & LISTENING:\n" +
                "1. True/False/Not Given: Output ONLY 'TRUE', 'FALSE', or 'NOT GIVEN' in full uppercase.\n" +
                "2. Yes/No/Not Given: Output ONLY 'YES', 'NO', or 'NOT GIVEN' in full uppercase.\n" +
                "3. Sentence/Note/Summary/Table completion: Strictly obey the word limit (e.g., 'ONE WORD ONLY' or 'NO MORE THAN TWO WORDS'). Copy the exact verbatim word(s) from text or audio. Do not add unnecessary articles.\n" +
                "4. Multiple Choice: Output the option letter(s) (e.g., 'B' or 'A, C').\n" +
                "5. Matching Headings / Info: Output roman numerals or letters (e.g., 'iv' or 'C').\n" +
                "6. Multiple questions on screen: Output each question number and answer on a separate line (e.g., '1. TRUE\\n2. library\\n3. B').\n" +
                "7. Absolutely NO explanations, introductory remarks, or conversational filler. Output only direct answers.";
        }

        public void ApplyJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                Dictionary<string, object> dict = ser.Deserialize<Dictionary<string, object>>(json);
                if (dict != null)
                {
                    if (dict.ContainsKey("provider") && dict["provider"] != null) this.Provider = dict["provider"].ToString();
                    if (dict.ContainsKey("gemini_api_key") && dict["gemini_api_key"] != null && !string.IsNullOrEmpty(dict["gemini_api_key"].ToString()))
                        this.GeminiApiKey = dict["gemini_api_key"].ToString().Trim();
                    if (dict.ContainsKey("gemini_model") && dict["gemini_model"] != null && !string.IsNullOrEmpty(dict["gemini_model"].ToString()))
                        this.GeminiModel = dict["gemini_model"].ToString().Trim();
                    if (dict.ContainsKey("dot_mode") && dict["dot_mode"] != null) this.DotMode = dict["dot_mode"].ToString();
                    if (dict.ContainsKey("position_mode") && dict["position_mode"] != null) this.PositionMode = dict["position_mode"].ToString();
                    if (dict.ContainsKey("local_db_enabled")) this.LocalDbEnabled = Convert.ToBoolean(dict["local_db_enabled"]);
                    if (dict.ContainsKey("opacity")) this.Opacity = Convert.ToDouble(dict["opacity"]);
                    if (dict.ContainsKey("font_size")) this.FontSize = Convert.ToInt32(dict["font_size"]);
                    if (dict.ContainsKey("system_prompt") && dict["system_prompt"] != null && !string.IsNullOrEmpty(dict["system_prompt"].ToString()))
                        this.SystemPrompt = dict["system_prompt"].ToString();
                }
            }
            catch { }
        }

        private static void FetchGitConfig(AppConfig cfg, int timeoutMs)
        {
            try
            {
                string url = "https://raw.githubusercontent.com/0rkhanmammadov/questions/main/config.json";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "curl/7.88.1";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    if (!string.IsNullOrEmpty(json))
                    {
                        cfg.ApplyJson(json);
                        try
                        {
                            string tempPath = Path.Combine(Path.GetTempPath(), "config.json");
                            File.WriteAllText(tempPath, json, Encoding.UTF8);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static AppConfig Load()
        {
            AppConfig cfg = new AppConfig();
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string configPath = Path.Combine(appDir, "config.json");
            if (!File.Exists(configPath))
            {
                configPath = Path.Combine(Path.GetTempPath(), "config.json");
            }

            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath, Encoding.UTF8);
                    cfg.ApplyJson(json);
                }
                catch { }
            }

            string envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (!string.IsNullOrEmpty(envKey)) cfg.GeminiApiKey = envKey.Trim();

            string envModel = Environment.GetEnvironmentVariable("GEMINI_MODEL");
            if (!string.IsNullOrEmpty(envModel)) cfg.GeminiModel = envModel.Trim();

            // Auto-fetch latest model & config directly from Git in background
            ThreadPool.QueueUserWorkItem(state => FetchGitConfig(cfg, 4000));

            return cfg;
        }
    }
}
