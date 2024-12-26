using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Windows.Forms;
using Teradyne.Systems.TAP.Component;
using Teradyne.Systems.TAP.Common;
using System.Runtime.ConstrainedExecution;
using PretestPosttestLib_CSharp.Createddb;

namespace PretestPosttestLib_CSharp
{
    /// <summary>
    /// This class is a template that implements the required methods for an
    /// object to act as a Pretest and Posttest component in TAP.
    /// Search for the string "TODO" to see those places where you may need
    /// to write some code.
    /// </summary>
    public class PretestPosttest : ITAPComponent, IPretestProcessor, IPosttestProcessor
    {
        public string board1sn = "";
        public string board2sn = "";


        public static string inipath = "C:\\PretestPostTestLib\\PretestPostTestLib.ini";

        #region ITAPComponent members 

        /// <summary>
        /// This method will be called by TAP to initialize the object. It is expected
        /// that initialization is performed asynchronously, and the Initialized
        /// event be raised when initialization is complete. The implementation of
        /// the Initialize method shown here uses the threadpool to asynchronously
        /// execute a different method, __InternalInitialize(). Please put all your
        /// initialization code in the method __InternalInitialize().
        /// </summary>
        public virtual void Initialize()
        {
            // DO NOT CHANGE THIS. Please put your initialization
            // code in the indicated place in the method __InternalInitialize().
            ThreadPool.QueueUserWorkItem(__InternalInitialize);     
        }

        private void __InternalInitialize(object not_used)
        {
            //TODO: Put any required initialization code here.
            //TODO: When initialization is complete, set the 
            //TODO: successful and description variables as appropriate.
            bool successful;
            Iniconfig iniconfig = new Iniconfig();
            iniconfig.INIFile(inipath);
            if (!File.Exists(inipath))
            {
                //iniconfig.INIFile(inipath);
                iniconfig.IniWriteValue("CONFIG", "FAIL_COUNT", "0");
                iniconfig.IniWriteValue("CONFIG", "FAIL_ALERT_NB", "3");

                iniconfig.IniWriteValue("CONFIG", "NG_SATION", "LOADER,SMTS,SMTC,SPIS,SPIC,AOIA,CPUSN");
                iniconfig.IniWriteValue("CONFIG", "PROCESS_NEED_TEST", "Panic");

                iniconfig.IniWriteValue("CONFIG", "BARCODE", "PPID");

                iniconfig.IniWriteValue("CONFIG", "PREDDB", "ON");
                iniconfig.IniWriteValue("CONFIG", "DDBPATH", "C:\\Teradyne\\PretestPosttestLib\\Compal\\DDB");
                iniconfig.IniWriteValue("CONFIG", "DDBADDRES", "0xFE0");
                iniconfig.IniWriteValue("CONFIG", "DDBCHECKSUM", "0");

                iniconfig.IniWriteValue("FMDI", "URL", "http://192.188.0.13:9092/FMDI/SMT/FTSTDCHECK.ashx");
                iniconfig.IniWriteValue("FMDI", "CUSTOMER", "SMT");
                iniconfig.IniWriteValue("FMDI", "GROUP", "ATE");
                iniconfig.IniWriteValue("FMDI", "SITE", "CD10");
                iniconfig.IniWriteValue("FMDI", "LINE", "CD2SMTA");

                iniconfig.IniWriteValue("SKU", "451AQL31L01", "LAL241P,100,2,0");

            }

            if (File.Exists(inipath))
            {

                string[] skus = iniconfig.IniReadKeys("SKU");
                foreach (string s in skus)
                {
                    if (!String.IsNullOrEmpty(s))
                    {
                        int i = 0;
                        foreach (string k in skus)
                        {
                            if (!String.IsNullOrEmpty(k))
                            {
                                if (k == s)
                                {
                                    i++;
                                    if (i > 1)
                                    {
                                        Console.WriteLine(k);
                                        iniconfig.IniDeletekey("SKU", k);
                                    }
                                }
                            }
                        }

                    }

                }

                successful = true;
            }
            else
            {
                successful = false;
            }




            string description = "Initialization successful.";

            // This block of code raises the Initialized event. You
            // should not need to modify this block of code.
            if (Initialized != null)
            {
                InitializedEventArgs args = new InitializedEventArgs(successful, description);
                Delegate[] delegates = Initialized.GetInvocationList();
                foreach (EventHandler<InitializedEventArgs> eh in delegates)
                {
                    try { eh(this, args); }
                    catch { }
                }
            }
        }

        /// <summary>
        /// This method will be called to shut down the object. It is expected
        /// that this process be performed asynchronously, and the ShutdownComplete
        /// event be raised when the process is complete. The implementation of
        /// the Shutdown method shown here uses the threadpool to asynchronously
        /// execute a different method, __InternalShutdown(). Please put all your
        /// shutdown code in the method __InternalShutdown().
        /// </summary>
        public virtual void Shutdown()
        {
            // DO NOT CHANGE THIS. Please put your shutdown
            // code in the indicated place in the method __InternalShutdown().
            ThreadPool.QueueUserWorkItem(__InternalShutdown);     
        }

        private void __InternalShutdown(object not_used)
        {
            //TODO: Put any required shutdown code here.
            //TODO: When shutdown is complete, set the 
            //TODO: successful and description flags as appropriate.
            bool successful = true;
            string description = "Shutdown successful.";

            // This block of code raises the ShutdownComplete event. You
            // should not need to modify this block of code.
            if (ShutdownComplete != null)
            {
                ShutdownEventArgs args = new ShutdownEventArgs(successful, description);
                Delegate[] delegates = ShutdownComplete.GetInvocationList();
                foreach (EventHandler<ShutdownEventArgs> eh in delegates)
                {
                    try { eh(this, args); }
                    catch { }
                }
            }
        }

        /// <summary>
        /// An event raised once the object has initialized itself.
        /// Please do not change this definition.
        /// </summary>
        public virtual event EventHandler<InitializedEventArgs> Initialized;

        /// <summary>
        /// An event raised once the object has shut down.
        /// Please do not change this definition.
        /// </summary>
        public virtual event EventHandler<ShutdownEventArgs> ShutdownComplete;

        /// <summary>
        /// Returns an object that contains basic information about the component.
        /// This should not return null.
        /// </summary>
        public virtual ComponentInfo Info
        {
            get
            {
                //TODO: Change the Name and Description variables, if you like. 
                //TODO: These may be visible on the TAP main user interface, and 
                //TODO: in log files.
                string Name = "Compal(CD) TSHL52 TAP PretestPosttest_CSharp.dll";
                string Description = "Custom Pretest/Posttest";

                //TODO: This block of code reads the version of the DLL. You
                //TODO: can change the version by editing the file AssemblyInfo.cs
                //TODO: and changing the value assigned to the AssemblyFileVersion
                //TODO: attribute. (Which might look like: [assembly: AssemblyFileVersion("1.0.1.0")])
                Assembly assembly = Assembly.GetExecutingAssembly();
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(assembly.Location);
                string version = info.FileVersion;

                return new ComponentInfo(Name, Description, version, DateTime.Now);
            }
        }

        /// <summary>
        /// Returns the object that the component uses for logging. This may be  null.
        /// </summary>
        public virtual DebugLogEx Logger
        {
            //TODO: Don't change this unless you really feel the need to
            //TODO: Use the DebugLogEx-style logging. If you do however,
            //TODO: you are on your own...
            get { return null; }
        }

        #endregion
        #region IPretestProcessing Members

        /// <summary>
        /// Determines whether a board is to be tested.
        /// </summary>
        /// <param name="fixtureID">The fixture ID of the fixture currently
        /// mounted in the handler.</param>
        /// <param name="serialNumbers">The serial numbers that have been read
        /// for the board waiting to be tested.</param>
        /// <returns>A value that indicates whether the board should be tested.</returns>
        /// <remarks>
        /// The Prejudgement enumeration contains the following members:
        /// Prejudgement.DoNotTest_Pass: Bring the board into the machine, but do not
        ///     test it...just send it through. SMEMA signals will be configured to 
        ///     indicate that the test failed.
        /// Prejudgement.DoNotTest_Fail: Bring the board into the machine, but do not
        ///     test it...just send it through. SMEMA signals will be configured to 
        ///     indicate that the test failed.
        /// Prejudgement.Test: Bring the board into the machine and test it.
        /// Prejudgement.Panic: Do not bring the board into the machine. The assembly
        ///     line will stop until the operator manually removes the board from the
        ///     conveyor. Use this option if, for example, the board is not the correct
        ///     type for the fixture, and you really need to get the operator's attention
        ///     that there may be a serious problem.
        /// </remarks>
        public virtual Prejudgement DoPretestJudgement(int fixtureID,
            params string[] serialNumbers)
        {
            //TODO: Decide whether or not the incoming board should be tested,
            //TODO: and return the appropriate value.
            Prejudgement pre_jug = new Prejudgement();
            int iniid;
            int inibdnumber;
            string ver;
            string model;
            string sku = "";
            string[] boards = { "" };
            string[] board1s = { "" };
            string[] board2s = { "" };
            string[] stations = { "" };
            string ddbbase_path = "";
            string ddb_path = "";
            bool ddbset=false;
            string ddbaddres = "";
            string ddbchecksum = "";

            Iniconfig iniconfig = new Iniconfig();
            iniconfig.INIFile(inipath);
            string fail_alert = iniconfig.IniReadValue("CONFIG", "FAIL_ALERT_NB");
            string countf = iniconfig.IniReadValue("CONFIG", "FAIL_COUNT");
            string sn_set = iniconfig.IniReadValue("CONFIG", "BARCODE");

            if (Convert.ToUInt16(countf) >= Convert.ToUInt16(fail_alert))
            {
                Thread thread1 = new Thread(Message3fail);
                thread1.Start();
                pre_jug = Prejudgement.Panic;
            }
            else
            {
                Console.WriteLine(serialNumbers[0]);
                bool ack = Getbom.getbom(serialNumbers[0]);
                if (ack)
                {
                    string setpath = Path.GetDirectoryName(inipath);
                    string cuttpath = AppDomain.CurrentDomain.BaseDirectory.ToString();
                    if (File.Exists(setpath + "\\ACK.txt"))
                    {
                        File.Delete(setpath + "\\ACK.txt");
                    }
                    File.Move(cuttpath + "\\ACK.txt", setpath + "\\ACK.txt");
                    sku = Getbom.sku;;
                    stations = Getbom.station_check.Split(';');
                    ver = Getbom.sku.Substring(8, 3);
                    model = Getbom.model;
                    boards = Getbom.batcode_all.Split(';');
                    board1s = boards[0].Split('_');
                    board2s = boards[1].Split('_');


                    string rec = iniconfig.IniReadValue("SKU", sku);
                    //ini里状态是否存在
                    if (String.IsNullOrEmpty(rec))
                    {
                        Thread thread2 = new Thread(Messageskufail);
                        thread2.Start(sku);
                        pre_jug = Prejudgement.Panic;
                    }
                    else
                    {
                        bool skuseter = false;
                        string[] skuset = rec.Split(',');
                        if ((skuset.Length!=4)&& (skuset.Length != 7))
                        {
                            skuseter=true;
                        }
                        else
                        {
                            foreach (string s in skuset)
                            {
                                if (String.IsNullOrEmpty(s))
                                {
                                    skuseter = true;
                                }
                            }
                        }
                        //ini状态设置是否正确
                        if (skuseter)
                        {
                            Thread thread5 = new Thread(Messageskufail2);
                            thread5.Start(sku);
                            pre_jug = Prejudgement.Panic;
                        }
                        else
                        {
                            iniid = Convert.ToInt32(skuset[1]);
                            if (iniid!= fixtureID)
                            {
                                Thread thread9 = new Thread(Messageidfail);
                                thread9.Start(iniid);
                                pre_jug = Prejudgement.Panic;
                            }
                            else
                            {

                                inibdnumber = Convert.ToInt32(skuset[2]);
                                string preddb = skuset[3];
                                if (preddb == "1")
                                {
                                    ddbbase_path = skuset[6];
                                    ddb_path = iniconfig.IniReadValue("CONFIG", "DDBPATH");
                                    ddbaddres = skuset[4];
                                    ddbchecksum = skuset[5];

                                    if (!File.Exists(ddbbase_path))
                                    {
                                        Thread thread6 = new Thread(Messageddbfail);
                                        thread6.Start(ddbbase_path);
                                        pre_jug = Prejudgement.Panic;
                                    }
                                    else
                                    {
                                        if (!Directory.Exists(ddb_path))
                                        {
                                            Thread thread7 = new Thread(Messageddbfail);
                                            thread7.Start(ddb_path);
                                            pre_jug = Prejudgement.Panic;
                                        }
                                        else
                                        {
                                            ddbset = true;
                                        }

                                    }

                                }
                                if ((preddb != "1") || (ddbset))
                                {

                                    board1sn = model + "_" + ver;
                                    board2sn = model + "_" + ver;
                                    switch (sn_set)
                                    {
                                        case
                                            "PPID":
                                            board1sn = board1sn + "_" + board1s[1];
                                            board2sn = board2sn + "_" + board2s[1];
                                            break;
                                        case
                                            "PPID_SN":
                                            board1sn = board1sn + "_" + board1s[1] + "_" + board1s[0];
                                            board2sn = board2sn + "_" + board2s[1] + "_" + board2s[0];
                                            if (ddbset)
                                            {
                                                string ddb1 = ddb_path + "\\" + board1s[0] + ".ddb";
                                                string ddb2 = ddb_path + "\\" + board2s[0] + ".ddb";
                                                creatddb crt1 = new creatddb(ddb1, ddbbase_path, board1s[0], ddbaddres, ddbchecksum);
                                                Thread t1 = new Thread(new ThreadStart(crt1.CreateTestDDB));
                                                t1.Start();
                                                creatddb crt2 = new creatddb(ddb2, ddbbase_path, board2s[0], ddbaddres, ddbchecksum);
                                                Thread t2 = new Thread(new ThreadStart(crt2.CreateTestDDB));
                                                t2.Start();
                                            }
                                            break;
                                        case
                                            "PPID_MAC":
                                            if (board1s.Length < 3)
                                            {
                                                Thread thread4 = new Thread(Messagemacfail);
                                                thread4.Start();
                                                board1sn = board1sn + "_" + board1s[1];
                                                board2sn = board2sn + "_" + board2s[1];
                                            }
                                            else
                                            {
                                                board1sn = board1sn + "_" + board1s[1] + "_" + board1s[2];
                                                board2sn = board2sn + "_" + board2s[1] + "_" + board2s[2];
                                                if (ddbset)
                                                {
                                                    string ddb1 = ddb_path + "\\" + board1s[2] + ".ddb";
                                                    string ddb2 = ddb_path + "\\" + board2s[2] + ".ddb";

                                                    creatddb crt1 = new creatddb(ddb1, ddbbase_path, board1s[2], ddbaddres, ddbchecksum);
                                                    creatddb crt2 = new creatddb(ddb2, ddbbase_path, board2s[2], ddbaddres, ddbchecksum);
                                                    Thread t1 = new Thread(new ThreadStart(crt1.CreateTestDDB));
                                                    Thread t2 = new Thread(new ThreadStart(crt2.CreateTestDDB));
                                                    t1.Start();
                                                    t2.Start();
                                                }
                                            }
                                            break;
                                        case
                                        "SN_MAC":
                                            if (board1s.Length < 3)
                                            {
                                                Thread thread4 = new Thread(Messagemacfail);
                                                thread4.Start();
                                                board1sn = board1sn + "_" + board1s[0];
                                                board2sn = board2sn + "_" + board2s[0];
                                            }
                                            else
                                            {
                                                board1sn = board1sn + "_" + board1s[0] + "_" + board1s[2];
                                                board2sn = board2sn + "_" + board2s[0] + "_" + board2s[2];
                                                if (ddbset)
                                                {
                                                    string ddb1 = ddb_path + "\\" + board1s[2] + ".ddb";
                                                    string ddb2 = ddb_path + "\\" + board2s[2] + ".ddb";

                                                    creatddb crt1 = new creatddb(ddb1, ddbbase_path, board1s[2], ddbaddres, ddbchecksum);
                                                    creatddb crt2 = new creatddb(ddb2, ddbbase_path, board2s[2], ddbaddres, ddbchecksum);
                                                    Thread t1 = new Thread(new ThreadStart(crt1.CreateTestDDB));
                                                    Thread t2 = new Thread(new ThreadStart(crt2.CreateTestDDB));
                                                    t1.Start();
                                                    t2.Start();
                                                }
                                            }
                                            break;

                                        default:
                                            board1sn = board1sn + "_" + board1s[1];
                                            board2sn = board2sn + "_" + board2s[1];
                                            break;
                                    }






                                    string stat = iniconfig.IniReadValue("CONFIG", "NG_SATION");
                                    string proc = iniconfig.IniReadValue("CONFIG", "PROCESS_NEED_TEST");
                                    string[] stats = stat.Split(',');
                                    bool ng_station = false;
                                    foreach (string s in stats)
                                    {    int i ;
                                        for (i=0; i <= inibdnumber; i++)
                                        {
                                            if (s == stations[i])
                                            {
                                                ng_station = true;
                                            }
                                        }
                                    }
                                    if (ng_station)
                                    {

                                        Thread thread3 = new Thread(Messagestationfail);
                                        thread3.Start();
                                        pre_jug = Prejudgement.Panic;

                                        switch (proc)
                                        {
                                            case "DoNotTest_Pass": pre_jug = Prejudgement.DoNotTest_Pass; break;
                                            case "DoNotTest_Fail": pre_jug = Prejudgement.DoNotTest_Fail; break;
                                            case "Panic": pre_jug = Prejudgement.Panic; break;
                                            case "Test": pre_jug = Prejudgement.Test; break;
                                            default:
                                                pre_jug = Prejudgement.Test; break;
                                        }

                                    }
                                    else
                                    {
                                        pre_jug = Prejudgement.Test;
                                    }
                                }

                            } 
                        }
                     }
                }             
            }

            return pre_jug; 
        }

        /// <summary>
        /// Returns the serial numbers of a multiboard panel, given the serial number
        /// of the panel itself.
        /// </summary>
        /// <param name="serialNumber">The serial number of the panel.</param>
        /// <returns>A collection of barodes, one for each board in a multiboard
        /// panel. If this is not a multiboard panel, then this array should contain
        /// only the value serialNumber.</returns>
        public virtual string[] GetBoardSerialNumbers(string serialNumber)
        {
            //TODO: If this is a multiboard panel, and if you want to pass all
            //TODO: of the serial numbers to TestStation, create an array of strings
            //TODO: containing the serial numbers and return it to the caller.
            //TODO: If you do not need to do this, the following line of code will
            //TODO: suffice.


            return new string[] { board1sn, board2sn };


        }



        public virtual void Message3fail()
        {

            Iniconfig iniconfig = new Iniconfig();
            iniconfig.INIFile(inipath);
            MessageBox.Show("连续3片不良");
            iniconfig.IniWriteValue("CONFIG", "FAIL_COUNT", "0");
        }


        public virtual void Messageskufail(object sku)
        {
            string ver= sku.ToString();
            MessageBox.Show(ver + ":PretestPostTestLib.ini里没有这个状态");
        }

        public virtual void Messageidfail(object id)
        {
            string ver = id.ToString();
            MessageBox.Show(ver + ":PretestPostTestLib.ini里fixID设置不匹配");
        }


        public virtual void Messageddbfail(object sku)
        {
            string ver = sku.ToString();
            MessageBox.Show(ver + ":不存在");
        }
        public virtual void Messageskufail2(object sku)
        {
            string ver = sku.ToString();
            MessageBox.Show(ver + ":PretestPostTestLib.ini里设置有误");
        }
        public virtual void Messagestationfail()
        {
            string station = Getbom.station_check;
            MessageBox.Show(station + ":站别错误");
        }



        public virtual void Messagemacfail()
        {
            
            MessageBox.Show("没有MAC,更改PretestPostTestLib.ini里BARCODE设置");
        }

        #endregion
        #region IPosttestProcessing Members

        /// <summary>
        /// Provides a way for a board's detailed test results to be
        /// handled. This can mean anything from throwing them away, to
        /// uploading them to a customer's database. This method is expected
        /// to return a value that indicates whether TestStation's overall
        /// pass/fail result is to be accepted, or overridden.
        /// </summary>
        /// <param name="serialNumbers">The serial number(s) for the board
        /// that was tested.</param>
        /// <param name="pfResults">A collection of values indicating whether
        /// each board's test passed, failed ot was aborted. There should be
        /// exactly one element in this array for each element of the 
        /// serialNumbers array.</param>
        /// <param name="results">The detailed test results, as reported
        /// by TestStation.</param>
        /// <returns>A value that indicates whether TestStation's pass/fail
        /// determination should be accepted. The return value must come from the
        /// FinalJudgement enumeration, which has the following members:
        /// FinalJudgement.ForceFail: Override TestStation's pass/fail determination...the board FAILED.
        /// FinalJudgement.ForcePass: Override TestStation's pass/fail determination...the board PASSED.
        /// FinalJudgement.NoJudgement: Accept TestStation's pass/fail determination.
        /// </returns>
        public FinalJudgement ProcessResults(string[] serialNumbers, TestResult[] pfResults, byte[] results)
        {
            //TODO: Determine if the results indicate that TestStation's
            //TODO: pass/fail determination should be accepted, or overridden.
            //TODO: Return the appropriate value to the caller.
            string setpath = Path.GetDirectoryName(inipath);
            File.Delete(setpath+"\\ACK.txt");
            Iniconfig iniconfig = new Iniconfig();
            iniconfig.INIFile(inipath);
            string countf;
            bool Repass = true;
            FinalJudgement finalJudgement = new FinalJudgement();
            finalJudgement = FinalJudgement.NoJudgement;
            foreach (TestResult pfResult in pfResults) 
            {
                if (pfResult == TestResult.Fail)
                {
                    Repass=false;
                }
                if ((pfResult == TestResult.Untested)|| (pfResult == TestResult.Abort))
                {
                    finalJudgement = FinalJudgement.ForceFail;
                }
            }
            if (Repass)
            {
                iniconfig.IniWriteValue("CONFIG", "FAIL_COUNT", "0");
            }
            else
            {
                countf = iniconfig.IniReadValue("CONFIG", "FAIL_COUNT");
                countf = (Convert.ToInt32(countf) + 1).ToString();
                iniconfig.IniWriteValue("CONFIG", "FAIL_COUNT", countf);
            }
            return finalJudgement;
        }

        #endregion
    }
}
