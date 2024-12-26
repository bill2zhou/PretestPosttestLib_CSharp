using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Xml;
using System.Windows.Forms;

namespace PretestPosttestLib_CSharp
{
    public class Refun
    {
        public static string getbomstr(string patten, string matstr)
        {
            string output = "";
            Regex re = new Regex(patten, RegexOptions.IgnoreCase);
            MatchCollection matches = re.Matches(matstr);
            foreach (Match match in matches)
            {
                GroupCollection groups = match.Groups;
                output = groups[1].Value;
               if (groups[2].Value.Length>0)
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

            int i;
            string all_sn = "";
            bool isppid = false;
            batcode_all = "";
            station_check = "";

            List<string> boards = new List<string>();
            List<string> sub_boards = new List<string>();

            string configpath=PretestPosttest.inipath;
            Iniconfig iniconfig = new Iniconfig();
            iniconfig.INIFile(configpath);
            Dictionary<string, string> bar_macs = new Dictionary<string, string>();
            Dictionary<string, string> bar_ppids = new Dictionary<string, string>();
            Dictionary<string, string> bar_stations = new Dictionary<string, string>();

            string text = iniconfig.IniReadValue("FMDI", "URL") + "?sn=" + sn +"&customer="+ iniconfig.IniReadValue("FMDI", "CUSTOMER") + "&group="+ iniconfig.IniReadValue("FMDI", "GROUP") + "&line="+ iniconfig.IniReadValue("FMDI", "LINE") + "&site="+ iniconfig.IniReadValue("FMDI", "SITE") + "&intl=0.0.0.11-StandardClientImpl.dll-7.0.0.0";
            Console.WriteLine(text);
          
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
                string path1 = "BOM.txt";
                StreamWriter streamWriter1 = new StreamWriter(path1);
                streamWriter1.Write(output);
                streamWriter1.Close();

               string skuPatten = @".*SET SKU=(\w+).*";
               string modPatten = @".*SET MODELNAME=\s*(\w+).*";
               string sn1Patten = @".*SET SN=(\d+).*";
               string barsPatten = @".*SET SNMULTI=(.*);";
               string mac1Patten = @".*SET MAC_LAN_1=(\w+).*";
               string macsPatten = @".*SET MACMULTI_LAN_1=(.*);";
               string bar_macPatten = @".*SET SNMACMULTI=(.*);";
               string ppid1Patten = @".*SET SSN=(\w+).*";
               string ppidsPatten = @".*SET SSNMULTI=(.*);";
               string stationPatten = @".*SET GROUPMULTI=(.*);";

                 sku = Refun.getbomstr(skuPatten, output);
                 model = Refun.getbomstr(modPatten, output);
                string sn1 = Refun.getbomstr(sn1Patten, output);                           
                string bars = Refun.getbomstr(barsPatten, output);
                string mac1 = Refun.getbomstr(mac1Patten, output);
                string macs = Refun.getbomstr(macsPatten, output);
                string bar_mac= Refun.getbomstr(bar_macPatten, output);
                string ppid1 = Refun.getbomstr(ppid1Patten, output);
                string ppids = Refun.getbomstr(ppidsPatten, output);
                string station = Refun.getbomstr(stationPatten, output);


                
                if (bar_mac.Length>0)
                {
                   
                    string[] bar_maca = bar_mac.Split(';');
                    for (i = 0; i < bar_maca.Length; i++)
                    {
                        if (i < bar_maca.Length - 1)
                        {
                            if (bar_maca[i].Length == 13)
                            {
                                if (bar_maca[i + 1].Length == 12)
                                {
                                    bar_macs.Add(bar_maca[i], bar_maca[i + 1]);
                                }
                                else {
                                    bar_macs.Add(bar_maca[i], "");

                                }
                                                  
                            }
                        
                        }
               
                        if (i== bar_maca.Length - 1)
                        {
                            if (bar_maca[i].Length == 13)
                            {
                                bar_macs.Add(bar_maca[i], "");
                            }
                        }
                    }
                }

                if ((bars.Length > 0) && (station.Length > 0))
                {
                    string[] bara = bars.Split(';');
                    string[] stationa = station.Split(';');
                    int board_num = bara.Length;
                    int sta_num = stationa.Length;
            //        Console.WriteLine(board_num);
           //         Console.WriteLine(sta_num);
                    if (board_num != sta_num)
                    {
                        MessageBox.Show("FMDI回传有误station与SN数量不同");
                    }
                    else
                    {
                        for (i = 0; i < board_num; i++)
                        {
                            bar_stations.Add(bara[i], stationa[i]);

                        }
                    }

                }



                if ((bars.Length > 0) && (ppids.Length > 0))
                {
                    string[] bara = bars.Split(';');
                    string[] ppida = ppids.Split(';');
                    int board_num = bara.Length;
                    int ppid_num = ppida.Length;
                    if (board_num != ppid_num)
                    {
                        MessageBox.Show("FMDI回传有误PPID与SN数量不同");
                    }
                    else
                    {
                        for (i = 0; i < board_num; i++)
                        {
                            bar_ppids.Add(bara[i], ppida[i]);

                        }
                        isppid = true;
                    }

                }



                foreach (var item in bar_macs)
                {
                    if (isppid)
                    {
                        if (sn1 == item.Key)
                        {
                            boards.Add(item.Key + "_" + bar_ppids[item.Key] + "_" + item.Value);
                            station_check = station_check + ";" + bar_stations[item.Key];
                        }
                        else
                        {
                            if (sn1.Substring(1, 8) == item.Key.Substring(1, 8))
                            {
                                boards.Add(item.Key + "_" + bar_ppids[item.Key] + "_" + item.Value);
                                station_check = station_check + ";" + bar_stations[item.Key];
                            }
                            else
                            {
                                sub_boards.Add(item.Key + "_" + bar_ppids[item.Key] + "_" + item.Value);
                                station_check = station_check + ";" + bar_stations[item.Key];

                            }
                        }
                    }
                    else
                    {
                        if (sn1 == item.Key)
                        {
                            boards.Add(item.Key + "__" + item.Value);
                            station_check = station_check + ";" + bar_stations[item.Key];
                        }
                        else
                        {
                            if (sn1.Substring(1, 8) == item.Key.Substring(1, 8))
                            {
                                boards.Add(item.Key + "__" + item.Value);
                                station_check = station_check + ";" + bar_stations[item.Key];
                            }
                            else
                            {
                                sub_boards.Add(item.Key + "__" + item.Value);
                                station_check = station_check + ";" + bar_stations[item.Key];

                            }
                        }

                    }

                }


                for (i = 0; i < boards.Count; i++)
                {

                    all_sn = all_sn + boards[i] + "\r\n";
                    batcode_all = batcode_all + boards[i] + ";";

                }

                for (i = 0; i < sub_boards.Count; i++)
                {

                    all_sn = all_sn + sub_boards[i] + "\r\n";
                    batcode_all = batcode_all + sub_boards[i] + ";";
                }


                string path = "ACK.txt";
                StreamWriter streamWriter = new StreamWriter(path);
                streamWriter.Write(model+"_");
                streamWriter.Write(sku + "_");
                streamWriter.Write(station_check + "\r\n");
                streamWriter.Write(all_sn + "\r\n");
                streamWriter.Write("\n");
                streamWriter.Close();
                      
            }
         
            return true;
        }
  
    
    }
}
