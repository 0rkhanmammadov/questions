using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace QuizAssistant
{
    public static class DohResolver
    {
        private class DnsCacheEntry
        {
            public string Ip;
            public DateTime Expires;
        }

        private static readonly Dictionary<string, DnsCacheEntry> Cache = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly object LockObj = new object();

        static DohResolver()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls12;
            }
            catch { }
        }

        public static string Resolve(string hostname)
        {
            if (string.IsNullOrEmpty(hostname)) return hostname;

            IPAddress ipParsed;
            if (IPAddress.TryParse(hostname, out ipParsed))
            {
                return hostname;
            }

            lock (LockObj)
            {
                DnsCacheEntry entry;
                if (Cache.TryGetValue(hostname, out entry) && DateTime.UtcNow < entry.Expires)
                {
                    return entry.Ip;
                }
            }

            string resolvedIp = QueryCloudflare(hostname);
            if (string.IsNullOrEmpty(resolvedIp))
            {
                resolvedIp = QueryGoogle(hostname);
            }

            if (!string.IsNullOrEmpty(resolvedIp))
            {
                lock (LockObj)
                {
                    Cache[hostname] = new DnsCacheEntry
                    {
                        Ip = resolvedIp,
                        Expires = DateTime.UtcNow.AddMinutes(10)
                    };
                }
                return resolvedIp;
            }

            try
            {
                IPAddress[] ips = Dns.GetHostAddresses(hostname);
                if (ips != null && ips.Length > 0)
                {
                    return ips[0].ToString();
                }
            }
            catch { }

            return hostname;
        }

        private static string QueryCloudflare(string hostname)
        {
            try
            {
                string url = "https://1.1.1.1/dns-query?name=" + Uri.EscapeDataString(hostname) + "&type=A";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Accept = "application/dns-json";
                req.Timeout = 4000;
                req.UserAgent = "Mozilla/5.0";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    return ParseIpFromJson(json);
                }
            }
            catch
            {
                return null;
            }
        }

        private static string QueryGoogle(string hostname)
        {
            try
            {
                string url = "https://8.8.8.8/resolve?name=" + Uri.EscapeDataString(hostname) + "&type=A";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Accept = "application/json";
                req.Timeout = 4000;
                req.UserAgent = "Mozilla/5.0";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    return ParseIpFromJson(json);
                }
            }
            catch
            {
                return null;
            }
        }

        private static string ParseIpFromJson(string json)
        {
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                Dictionary<string, object> root = ser.Deserialize<Dictionary<string, object>>(json);
                if (root != null && root.ContainsKey("Answer"))
                {
                    object[] answers = root["Answer"] as object[];
                    if (answers != null)
                    {
                        foreach (object item in answers)
                        {
                            Dictionary<string, object> dict = item as Dictionary<string, object>;
                            if (dict != null && dict.ContainsKey("type") && Convert.ToInt32(dict["type"]) == 1)
                            {
                                if (dict.ContainsKey("data"))
                                {
                                    string data = dict["data"].ToString().Trim();
                                    IPAddress testIp;
                                    if (IPAddress.TryParse(data, out testIp))
                                    {
                                        return data;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
