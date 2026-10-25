using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace QuizAssistant
{
    public class AppConfig
    {
        private static readonly string BuiltinKey = Encoding.ASCII.GetString(
            Convert.FromBase64String("QVEuQWI4Uk42SVdXa0ZfanFFWUV0SU93cmlEZlIxb0R2aHRES0FNVWNNYmVDeFpPN1RINGc=")
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
            GeminiModel = "gemini-3.5-flash-lite";
            Opacity = 0.82;
            FontSize = 10;
            PositionMode = "cursor";
            DotMode = "off";
            LocalDbEnabled = true;
            SystemPrompt = "QAYDALAR:\n" +
                "1. Təkseçimlidirsə: QƏTİYYƏN variant hərfi (A, B, C...) yazma, yalnız variantdakı sözü/mətni yaz (məsələn: 'Bakı').\n" +
                "2. Çoxseçimlidirsə (bir neçə bənd seçilməlidirsə): Doğru rəqəmləri tire ilə yaz (məsələn: '1-3-4' və ya '1-2-5').\n" +
                "3. Uyğunluq sualıdırsa: '1-a, 2-c, 3-b' kimi yaz.\n" +
                "4. Qətiyyən izahat, şərh yazma. Yalnız cavabı ver!";
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
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    Dictionary<string, object> dict = ser.Deserialize<Dictionary<string, object>>(json);
                    if (dict != null)
                    {
                        if (dict.ContainsKey("provider") && dict["provider"] != null) cfg.Provider = dict["provider"].ToString();
                        if (dict.ContainsKey("gemini_api_key") && dict["gemini_api_key"] != null && !string.IsNullOrEmpty(dict["gemini_api_key"].ToString()))
                            cfg.GeminiApiKey = dict["gemini_api_key"].ToString();
                        if (dict.ContainsKey("gemini_model") && dict["gemini_model"] != null) cfg.GeminiModel = dict["gemini_model"].ToString();
                        if (dict.ContainsKey("dot_mode") && dict["dot_mode"] != null) cfg.DotMode = dict["dot_mode"].ToString();
                        if (dict.ContainsKey("position_mode") && dict["position_mode"] != null) cfg.PositionMode = dict["position_mode"].ToString();
                        if (dict.ContainsKey("local_db_enabled")) cfg.LocalDbEnabled = Convert.ToBoolean(dict["local_db_enabled"]);
                        if (dict.ContainsKey("opacity")) cfg.Opacity = Convert.ToDouble(dict["opacity"]);
                        if (dict.ContainsKey("font_size")) cfg.FontSize = Convert.ToInt32(dict["font_size"]);
                    }
                }
                catch { }
            }

            string envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (!string.IsNullOrEmpty(envKey))
            {
                cfg.GeminiApiKey = envKey.Trim();
            }

            return cfg;
        }
    }
}
