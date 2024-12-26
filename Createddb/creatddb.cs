using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PretestPosttestLib_CSharp.Createddb
{
    internal class creatddb
    {
        private string ddbfile;
        private string baseDDB;
        private string barcode;
        private string address;
        private string sumcehck;


        public creatddb(string p1,string p2,string p3,string p4,string p5)
        {
            ddbfile = p1;
            baseDDB=p2;
            barcode=p3;
            address=p4;
            sumcehck=p5;
        }

        public void CreateTestDDB()
        {
            byte[] buffer = new byte[5120];
            int i = 0;
            int barcode_size = 0;
            int ad = 1024 + Convert.ToInt32(address, 16);
            int sum = 0;
            int check_sum = 0;


            if (barcode.Length == 13)
            {
                barcode = barcode + "F";
          //      Console.WriteLine("SN:" + barcode);
                barcode_size = 7;
            }

            if (barcode.Length == 12)
            {
         //       Console.WriteLine("MacSN:" + barcode);
                barcode_size = 6;
            }

            using (FileStream streamrd = new FileStream(baseDDB,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                using (var reader = new BinaryReader(streamrd))
                {
                    for (i = 0; i < ad; i++)
                    {
                        buffer[i] = reader.ReadByte();
                    }


                    for (i = ad; i < (ad + barcode_size); i++)
                    {
                        reader.ReadByte();
                        string bb = barcode.Substring((i - ad) * 2, 2);
                        buffer[i] = ReverseBitFromStr(bb);
                    }
                    for (i = (ad + barcode_size); i < 5120; i++)
                    {
                        buffer[i] = reader.ReadByte();
                    }
                }
                try
                {
                    using (var stream = File.Open(ddbfile, FileMode.Create))
                    {

                        using (var writer = new BinaryWriter(stream, Encoding.ASCII))
                        {
                            if (sumcehck == "1")
                            {
                                for (i = 1024; i < 1150;)
                                {
                                    string hexrec = ReverseBit(buffer[i + 1]).ToString("X2") + ReverseBit(buffer[i]).ToString("X2");
                                    //      Console.WriteLine(hexrec);
                                    sum = sum + Convert.ToInt32(hexrec, 16);
                                    //    Console.WriteLine(sum);
                                    i = i + 2;
                                }
                                //       Console.WriteLine(sum);
                                sum = sum & Convert.ToInt32("0xFFFF", 16);
                                if (sum > Convert.ToInt32("0xBABA", 16))
                                {
                                    check_sum = Convert.ToInt32("0x1BABA", 16) - sum;
                                }
                                if (sum < Convert.ToInt32("0xBABA", 16))
                                {
                                    check_sum = Convert.ToInt32("0xBABA", 16) - sum;
                                }

                                string check_hex = check_sum.ToString("X4");
                                check_hex = check_hex.Substring(2, 2) + check_hex.Substring(0, 2);
                                Console.WriteLine("CheckSum:" + check_hex);
                                buffer[1150] = ReverseBitFromStr(check_hex.Substring(0, 2));
                                buffer[1151] = ReverseBitFromStr(check_hex.Substring(2, 2));

                                for (i = 0; i < 5120; i++)
                                {
                                    writer.Write(buffer[i]);
                                }
                            }
                            else
                            {
                                for (i = 0; i < 5120; i++)
                                {
                                    writer.Write(buffer[i]);
                                }
                            }
                        }
                    }
                }
                catch (IOException)
                {
                    MessageBox.Show(ddbfile+":文件写入失败");
                }
            
            
            }
        }

        static byte ReverseBitFromStr(string old)
        {
            byte b = Convert.ToByte(old, 16);
            b = (byte)((byte)((byte)(b & 0x55) << 1) | (byte)((byte)(b & 0xAA) >> 1));
            b = (byte)((byte)((byte)(b & 0x33) << 2) | (byte)((byte)(b & 0xCC) >> 2));
            b = (byte)((byte)((byte)(b & 0x0F) << 4) | (byte)((byte)(b & 0xF0) >> 4));
            return b;
        }


        static byte ReverseBit(byte b)
        {
            b = (byte)((byte)((byte)(b & 0x55) << 1) | (byte)((byte)(b & 0xAA) >> 1));
            b = (byte)((byte)((byte)(b & 0x33) << 2) | (byte)((byte)(b & 0xCC) >> 2));
            b = (byte)((byte)((byte)(b & 0x0F) << 4) | (byte)((byte)(b & 0xF0) >> 4));
            return b;
        }

    }
}
