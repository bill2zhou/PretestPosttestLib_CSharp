using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Windows.Forms;

namespace PretestPosttestLib_CSharp
{
    public class Refun
    {
        public static string getbomstr(string pattern, string matstr)
        {
            string output = "";
            Regex re = new Regex(pattern, RegexOptions.IgnoreCase);
            MatchCollection matches = re.Matches(matstr);
            foreach (Match match in matches)
            {
                GroupCollection groups = match.Groups;
                output = groups[1].Value;
                if (groups[2].Value.Length > 0)
                {
                    output = output + "," + groups[2].Value;
                }
            }
            return output;
        }
    }

    public class Getbom
    {
        public static string model;
        public static string sku;
        public static string batcode_all;
        public static string station_check;

        public static bool getbom(string sn)
        {
            bool ackok = false;
            batcode_all = "";
            station_check = "";
            model = "";
            sku = "";

            List<string> boards = new List<string>();
            List<string> subBoards = new List<string>();
            List<string> stationList = new List<string>();  // 优化：使用 List 收集站别

            try
            {
                string configPath = PretestPosttest.IniPath;
                Iniconfig iniconfig = new Iniconfig();
                iniconfig.INIFile(configPath);

                Dictionary<string, string> barMacs = new Dictionary<string, string>();
                Dictionary<string, string> barPpids = new Dictionary<string, string>();
                Dictionary<string, string> barStations = new Dictionary<string, string>();

                // 构建 FMDI 请求 URL
                string text = $"{iniconfig.IniReadValue("FMDI", "URL")}?sn={sn}" +
                    $"&customer={iniconfig.IniReadValue("FMDI", "CUSTOMER")}" +
                    $"&group={iniconfig.IniReadValue("FMDI", "GROUP")}" +
                    $"&line={iniconfig.IniReadValue("FMDI", "LINE")}" +
                    $"&site={iniconfig.IniReadValue("FMDI", "SITE")}" +
                    $"&intl=0.0.0.11-StandardClientImpl.dll-7.0.0.0";
                Console.WriteLine($"[FMDI] Request URL: {text}");

                HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(text);
                httpWebRequest.Method = "GET";
                httpWebRequest.Proxy = null;
                httpWebRequest.UserAgent = "FMDICLIENT";
                httpWebRequest.Timeout = 10000;
                httpWebRequest.ReadWriteTimeout = 10000;

                WebResponse response = httpWebRequest.GetResponse();
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(response.GetResponseStream());

                foreach (XmlNode childNode in xmlDocument.ChildNodes)
                {
                    string output = childNode.InnerText;

                    // 正则表达式定义
                    string skuPattern = @".*SET SKU=(\w+).*";
                    string modPattern = @".*SET MODELNAME=\s*(\w+).*";
                    string sn1Pattern = @".*SET SN=(\d+).*";
                    string barsPattern = @".*SET SNMULTI=(.*);";
                    string mac1Pattern = @".*SET MAC_LAN_1=(\w+).*";
                    string macsPattern = @".*SET MACMULTI_LAN_1=(.*);";
                    string barMacPattern = @".*SET SNMACMULTI=(.*);";
                    string ppid1Pattern = @".*SET SSN=(\w+).*";
                    string ppidsPattern = @".*SET SSNMULTI=(.*);";
                    string stationPattern = @".*SET GROUPMULTI=(.*);";

                    // 解析 FMDI 返回数据
                    sku = Refun.getbomstr(skuPattern, output);
                    model = Refun.getbomstr(modPattern, output);
                    string sn1 = Refun.getbomstr(sn1Pattern, output);
                    string bars = Refun.getbomstr(barsPattern, output);
                    string mac1 = Refun.getbomstr(mac1Pattern, output);
                    string macs = Refun.getbomstr(macsPattern, output);
                    string barMac = Refun.getbomstr(barMacPattern, output);
                    string ppid1 = Refun.getbomstr(ppid1Pattern, output);
                    string ppids = Refun.getbomstr(ppidsPattern, output);
                    string station = Refun.getbomstr(stationPattern, output);

                    Console.WriteLine($"[FMDI] SKU={sku}, Model={model}, SN={sn1}");

                    // 解析 MAC 地址
                    ParseBarMacs(barMac, barMacs);

                    // 解析站别信息
                    ParseStations(bars, station, barStations);

                    // 解析 PPID 信息
                    bool isPpid = ParsePpids(bars, ppids, barPpids);

                    // 构建板卡信息
                    BuildBoardInfo(sn1, barMacs, barPpids, barStations, isPpid, 
                        boards, subBoards, stationList);

                    // 拼接板卡序列号（优化：使用 string.Join 避免尾随分号）
                    if (boards.Count > 0)
                    {
                        batcode_all = string.Join(";", boards);
                        if (subBoards.Count > 0)
                        {
                            batcode_all += ";" + string.Join(";", subBoards);
                        }
                        ackok = true;
                    }

                    // 拼接站别信息（优化：使用 string.Join 避免前导分号）
                    station_check = string.Join(";", stationList);

                    Console.WriteLine($"[FMDI] station_check={station_check}");
                    Console.WriteLine($"[FMDI] batcode_all={batcode_all}");

                    // 写入 ACK 文件
                    WriteAckFile(model, sku, station_check, boards, subBoards);
                }

                response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FMDI] 获取 BOM 信息失败：{ex.Message}");
                MessageBox.Show($"获取 BOM 信息失败：{ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return ackok;
        }

        /// <summary>
        /// 解析 MAC 地址信息
        /// </summary>
        private static void ParseBarMacs(string barMac, Dictionary<string, string> barMacs)
        {
            if (string.IsNullOrEmpty(barMac)) return;

            string[] barMaca = barMac.Split(';');
            for (int i = 0; i < barMaca.Length; i++)
            {
                if (barMaca[i].Length == 13)
                {
                    string macValue = (i < barMaca.Length - 1 && barMaca[i + 1].Length == 12) 
                        ? barMaca[i + 1] : "";
                    barMacs.Add(barMaca[i], macValue);
                }
            }
        }

        /// <summary>
        /// 解析站别信息
        /// </summary>
        private static void ParseStations(string bars, string station, 
            Dictionary<string, string> barStations)
        {
            if (string.IsNullOrEmpty(bars) || string.IsNullOrEmpty(station)) return;

            string[] bara = bars.Split(';');
            string[] stationa = station.Split(';');

            if (bara.Length != stationa.Length)
            {
                MessageBox.Show("FMDI 回传有误 station 与 SN 数量不同");
                return;
            }

            for (int i = 0; i < bara.Length; i++)
            {
                barStations.Add(bara[i], stationa[i]);
            }
        }

        /// <summary>
        /// 解析 PPID 信息
        /// </summary>
        private static bool ParsePpids(string bars, string ppids, 
            Dictionary<string, string> barPpids)
        {
            if (string.IsNullOrEmpty(bars) || string.IsNullOrEmpty(ppids)) return false;

            string[] bara = bars.Split(';');
            string[] ppida = ppids.Split(';');

            if (bara.Length != ppida.Length)
            {
                MessageBox.Show("FMDI 回传有误 PPID 与 SN 数量不同");
                return false;
            }

            for (int i = 0; i < bara.Length; i++)
            {
                barPpids.Add(bara[i], ppida[i]);
            }
            return true;
        }

        /// <summary>
        /// 构建板卡信息
        /// </summary>
        private static void BuildBoardInfo(string sn1, 
            Dictionary<string, string> barMacs,
            Dictionary<string, string> barPpids,
            Dictionary<string, string> barStations,
            bool isPpid,
            List<string> boards,
            List<string> subBoards,
            List<string> stationList)  // 优化：添加站别列表参数
        {
            foreach (var item in barMacs)
            {
                string boardInfo = isPpid 
                    ? $"{item.Key}_{barPpids[item.Key]}_{item.Value}"
                    : $"{item.Key}__{item.Value}";

                bool isMainBoard = IsMainBoard(sn1, item.Key);
                List<string> targetList = isMainBoard ? boards : subBoards;
                targetList.Add(boardInfo);

                // 优化：收集站别到 List
                if (barStations.ContainsKey(item.Key))
                {
                    stationList.Add(barStations[item.Key]);
                }
            }
        }

        /// <summary>
        /// 判断是否为主板
        /// </summary>
        private static bool IsMainBoard(string sn1, string key)
        {
            if (sn1 == key) return true;
            if (sn1.Length > 8 && key.Length > 8)
            {
                return sn1.Substring(1, 8) == key.Substring(1, 8);
            }
            return false;
        }

        /// <summary>
        /// 写入 ACK 文件
        /// </summary>
        private static void WriteAckFile(string model, string sku, string stationCheck, 
            List<string> boards, List<string> subBoards)
        {
            try
            {
                string path = "ACK.txt";
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"{model}_{sku}_{stationCheck}");
                
                foreach (string board in boards)
                {
                    sb.AppendLine(board);
                }
                foreach (string subBoard in subBoards)
                {
                    sb.AppendLine(subBoard);
                }
                sb.AppendLine();

                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ACK] 写入文件失败：{ex.Message}");
            }
        }
    }
}