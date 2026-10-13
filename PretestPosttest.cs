using PretestPosttestLib_CSharp.Createddb;
using PretestPosttestLib_CSharp.message;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Teradyne.Systems.TAP.Common;
using Teradyne.Systems.TAP.Component;

namespace PretestPosttestLib_CSharp
{
    /// <summary>
    /// TAP Pretest 和 Posttest 组件实现类
    /// 负责电路板测试前的预判断和测试后的结果处理
    /// </summary>
    public class PretestPosttest : ITAPComponent, IPretestProcessor, IPosttestProcessor
    {
        #region 常量定义
 
        private const string DEFAULT_INI_PATH = @"C:\PretestPostTestLib\PretestPostTestLib.ini";
        private const int DEFAULT_FAIL_ALERT_COUNT = 3;
        private const string DEFAULT_NG_STATION = "LOADER,SMTS,SMTC,SPIS,SPIC,AOIA,CPUSN";
        private const string DEFAULT_PROCESS_NEED_TEST = "Panic";
        private const string DEFAULT_BARCODE_TYPE = "PPID";
        
        // [新增] Socket Server 默认端口，不再从 Config 读取
        private const int DEFAULT_SERVER_PORT = 8888;

        #endregion

        #region 字段

        public string[] BoardSerialNumbers;
        public static string IniPath = DEFAULT_INI_PATH;
        private Form _msgWindow = null;
        private Form _msgWindow2 = null;
        private static readonly object _lockObj = new object();

        // Socket 服务器实例，用于广播消息给监控端
        private SocketServer _socketServer = null;

        #endregion

        #region ITAPComponent 成员

        /// <summary>
        /// TAP 组件初始化方法（异步执行）
        /// </summary>
        public virtual void Initialize()
        {
            ThreadPool.QueueUserWorkItem(InternalInitialize);
        }

        /// <summary>
        /// 内部初始化逻辑
        /// </summary>
        private void InternalInitialize(object state)
        {
            bool successful = false;
            string description = string.Empty;

            try
            {
                var iniConfig = new Iniconfig();
                iniConfig.INIFile(IniPath);

                // 如果 INI 文件不存在，创建默认配置
                if (!File.Exists(IniPath))
                {
                    CreateDefaultIniConfig(iniConfig);
                }

                // 清除重复的 SKU 配置
                if (File.Exists(IniPath))
                {
                    RemoveDuplicateSkus(iniConfig);
                    
                    // [修改] 启动 Socket 服务器，不再检查 Config 开关，直接启动
                    StartSocketServer();
                    
                    successful = true;
                    description = "初始化成功";
                }
                else
                {
                    successful = false;
                    description = "INI 文件创建失败";
                }
            }
            catch (Exception ex)
            {
                successful = false;
                description = $"初始化失败：{ex.Message}";
            }

            // 触发初始化完成事件
            RaiseInitializedEvent(successful, description);
        }

        /// <summary>
        /// [修改] 启动 Socket 服务器
        /// 不再从 Config 读取 IP/Port/Switch，直接使用常量端口
        /// </summary>
        private void StartSocketServer()
        {
            // 防止重复启动
            if (_socketServer != null)
            {
                return;
            }

            try
            {
                Console.WriteLine($"[SocketServer] 正在启动监听端口: {DEFAULT_SERVER_PORT}...");
                
                // 创建并启动服务器
                _socketServer = new SocketServer(DEFAULT_SERVER_PORT);
                _socketServer.Start();
                
                Console.WriteLine($"[SocketServer] 已在端口 {DEFAULT_SERVER_PORT} 启动监听，等待监控端连接...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SocketServer] 启动失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建默认 INI 配置文件
        /// </summary>
        private void CreateDefaultIniConfig(Iniconfig iniConfig)
        {
            // CONFIG 节
            iniConfig.IniWriteValue("CONFIG", "FAIL_COUNT", "0");
            iniConfig.IniWriteValue("CONFIG", "FAIL_ALERT_NB", DEFAULT_FAIL_ALERT_COUNT.ToString());
            iniConfig.IniWriteValue("CONFIG", "NG_SATION", DEFAULT_NG_STATION);
            iniConfig.IniWriteValue("CONFIG", "PROCESS_NEED_TEST", DEFAULT_PROCESS_NEED_TEST);
            iniConfig.IniWriteValue("CONFIG", "BARCODE", DEFAULT_BARCODE_TYPE);
            iniConfig.IniWriteValue("CONFIG", "PREDDB", "ON");
            iniConfig.IniWriteValue("CONFIG", "DDBPATH", "C:\\Teradyne\\PretestPosttestLib\\Compal\\DDB");
            iniConfig.IniWriteValue("CONFIG", "DDBADDRES", "0xFE0");
            iniConfig.IniWriteValue("CONFIG", "DDBCHECKSUM", "0");

            // FMDI 节
            iniConfig.IniWriteValue("FMDI", "URL", "http://192.188.0.13:9092/FMDI/SMT/FTSTDCHECK.ashx");
            iniConfig.IniWriteValue("FMDI", "CUSTOMER", "SMT");
            iniConfig.IniWriteValue("FMDI", "GROUP", "ATE");
            iniConfig.IniWriteValue("FMDI", "SITE", "CD10");
            iniConfig.IniWriteValue("FMDI", "LINE", "CD2SMTA");



            // SKU 节（示例）
            iniConfig.IniWriteValue("SKU", "451AQL31L01", "LAL241P,100,2,0");
        }

        /// <summary>
        /// 清除 INI 文件中重复的 SKU 配置
        /// </summary>
        private void RemoveDuplicateSkus(Iniconfig iniConfig)
        {
            string[] skus = iniConfig.IniReadKeys("SKU");
            var processedSkus = new System.Collections.Generic.HashSet<string>();

            foreach (string sku in skus)
            {
                if (!string.IsNullOrEmpty(sku))
                {
                    if (processedSkus.Contains(sku))
                    {
                        Console.WriteLine($"删除重复 SKU: {sku}");
                        iniConfig.IniDeletekey("SKU", sku);
                    }
                    else
                    {
                        processedSkus.Add(sku);
                    }
                }
            }
        }

        /// <summary>
        /// 触发初始化完成事件
        /// </summary>
        private void RaiseInitializedEvent(bool successful, string description)
        {
            if (Initialized != null)
            {
                var args = new InitializedEventArgs(successful, description);
                foreach (EventHandler<InitializedEventArgs> handler in Initialized.GetInvocationList())
                {
                    try
                    {
                        handler(this, args);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"初始化事件处理异常：{ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// TAP 组件关闭方法（异步执行）
        /// </summary>
        public virtual void Shutdown()
        {
            ThreadPool.QueueUserWorkItem(InternalShutdown);
        }

        /// <summary>
        /// 内部关闭逻辑
        /// </summary>
        private void InternalShutdown(object state)
        {
            bool successful = true;
            string description = "关闭成功";

            try
            {
                // 停止 Socket 服务器
                if (_socketServer != null)
                {
                    _socketServer.Stop();
                    _socketServer.Dispose();
                    _socketServer = null;
                    Console.WriteLine("[SocketServer] 服务器已停止。");
                }

                // 清理资源
                _msgWindow?.Dispose();
                _msgWindow2?.Dispose();
            }
            catch (Exception ex)
            {
                successful = false;
                description = $"关闭失败：{ex.Message}";
            }

            RaiseShutdownCompleteEvent(successful, description);
        }

        /// <summary>
        /// 触发关闭完成事件
        /// </summary>
        private void RaiseShutdownCompleteEvent(bool successful, string description)
        {
            if (ShutdownComplete != null)
            {
                var args = new ShutdownEventArgs(successful, description);
                foreach (EventHandler<ShutdownEventArgs> handler in ShutdownComplete.GetInvocationList())
                {
                    try
                    {
                        handler(this, args);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"关闭事件处理异常：{ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 初始化完成事件
        /// </summary>
        public virtual event EventHandler<InitializedEventArgs> Initialized;

        /// <summary>
        /// 关闭完成事件
        /// </summary>
        public virtual event EventHandler<ShutdownEventArgs> ShutdownComplete;

        /// <summary>
        /// 组件基本信息
        /// </summary>
        public virtual ComponentInfo Info
        {
            get
            {
                string name = "Compal(CD) TSHL52 TAP PretestPosttest_CSharp.dll";
                string description = "Custom Pretest/Posttest 2024";

                Assembly assembly = Assembly.GetExecutingAssembly();
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
                string version = versionInfo.FileVersion;

                return new ComponentInfo(name, description, version, DateTime.Now);
            }
        }

        /// <summary>
        /// 日志记录器（当前未启用）
        /// </summary>
        public virtual DebugLogEx Logger
        {
            get { return null; }
        }

        #endregion

        #region IPretestProcessor 成员

        /// <summary>
        /// 预测试判断：决定是否对电路板进行测试
        /// </summary>
        /// <param name="fixtureID">当前安装的治具 ID</param>
        /// <param name="serialNumbers">已读取的电路板序列号</param>
        /// <returns>测试判断结果</returns>
        public virtual Prejudgement DoPretestJudgement(int fixtureID, params string[] serialNumbers)
        {
            try
            {
                // 隐藏之前的消息窗口
                _msgWindow?.Hide();

                var iniConfig = new Iniconfig();
                iniConfig.INIFile(IniPath);

                // 检查连续失败计数
                string failAlert = iniConfig.IniReadValue("CONFIG", "FAIL_ALERT_NB");
                string failCount = iniConfig.IniReadValue("CONFIG", "FAIL_COUNT");

                if (Convert.ToUInt16(failCount) >= Convert.ToUInt16(failAlert))
                {
                    StartThreadSafe(Message3fail);
                    return Prejudgement.Panic;
                }

                // 获取 BOM 信息
                if (!Getbom.getbom(serialNumbers[0]))
                {
                    StartThreadSafe(Messageackfail);
                    CleanupAckFile();
                    return Prejudgement.Panic;
                }

                // 移动 ACK 文件
                MoveAckFile();

                // 解析 BOM 信息
                string sku = Getbom.sku;
                string[] stations = Getbom.station_check.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                string ver = Getbom.sku.Length >= 11 ? Getbom.sku.Substring(8, 3) : string.Empty;
                string model = Getbom.model;
                string[] boards = Getbom.batcode_all.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                // 验证 SKU 配置
                Prejudgement? skuCheckResult = ValidateSkuConfig(iniConfig, sku, fixtureID, stations, boards, ver, model);
                if (skuCheckResult.HasValue)
                {
                    return skuCheckResult.Value;
                }

                return Prejudgement.Test;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"预测试判断异常：{ex.Message}");
                return Prejudgement.Panic;
            }
        }

        /// <summary>
        /// 验证 SKU 配置
        /// </summary>
        private Prejudgement? ValidateSkuConfig(Iniconfig iniConfig, string sku, int fixtureID, 
            string[] stations, string[] boards, string ver, string model)
        {
            string rec = iniConfig.IniReadValue("SKU", sku);

            // 检查 SKU 是否存在
            if (string.IsNullOrEmpty(rec))
            {
                StartThreadSafe(() => Messageskufail(sku));
                return Prejudgement.Panic;
            }

            string[] skuSet = rec.Split(',');

            // 验证 SKU 配置格式
            if (!IsValidSkuFormat(skuSet))
            {
                StartThreadSafe(() => Messageskufail2(sku));
                return Prejudgement.Panic;
            }

            // 验证治具 ID
            int iniId = Convert.ToInt32(skuSet[1]);
            if (iniId != fixtureID)
            {
                StartThreadSafe(() => Messageidfail(iniId));
                return Prejudgement.Panic;
            }

            // 验证板卡数量
            int boardNumber = Convert.ToInt32(skuSet[2]);
            if (boards.Length < boardNumber)
            {
                StartThreadSafe(Messagesnfail);
                return Prejudgement.Panic;
            }

            // 初始化序列号数组
            BoardSerialNumbers = new string[boardNumber];

            // 处理 DDB 配置
            string predDdb = skuSet[3];
            bool ddbSet = false;
            string ddbBasePath = string.Empty;
            string ddbPath = iniConfig.IniReadValue("CONFIG", "DDBPATH");
            string ddbAddress = skuSet.Length > 4 ? skuSet[4] : string.Empty;
            string ddbChecksum = skuSet.Length > 5 ? skuSet[5] : string.Empty;

            if (predDdb == "1" || predDdb == "A")
            {
                ddbBasePath = skuSet.Length > 6 ? skuSet[6] : string.Empty;

                if (!File.Exists(ddbBasePath) || !Directory.Exists(ddbPath))
                {
                    StartThreadSafe(() => Messageddbfail(string.IsNullOrEmpty(ddbBasePath) ? ddbPath : ddbBasePath));
                    return Prejudgement.Panic;
                }
                ddbSet = true;
            }

            // 生成板卡序列号
            if (predDdb == "0" || ddbSet)
            {
                string barcodeType = iniConfig.IniReadValue("CONFIG", "BARCODE");
                GenerateBoardSerialNumbers(boardNumber, model, ver, boards, barcodeType, 
                    ddbSet, predDdb, ddbPath, ddbBasePath, ddbAddress, ddbChecksum);
            }

            // 检查站别配置
            return CheckStationConfig(iniConfig, stations, boardNumber);
        }

        /// <summary>
        /// 验证 SKU 配置格式
        /// </summary>
        private bool IsValidSkuFormat(string[] skuSet)
        {
            if (skuSet.Length != 4 && skuSet.Length != 7)
            {
                return false;
            }

            return !skuSet.Any(string.IsNullOrEmpty);
        }

        /// <summary>
        /// 生成板卡序列号
        /// </summary>
        private void GenerateBoardSerialNumbers(int boardNumber, string model, string ver, 
            string[] boards, string barcodeType, bool ddbSet, string predDdb,
            string ddbPath, string ddbBasePath, string ddbAddress, string ddbChecksum)
        {
            for (int i = 0; i < boardNumber; i++)
            {
                string[] boardParts = boards[i].Split('_');
                BoardSerialNumbers[i] = $"{model}_{ver}";

                switch (barcodeType)
                {
                    case "PPID":
                        BoardSerialNumbers[i] += boardParts.Length > 1 ? $"_{boardParts[1]}" : string.Empty;
                        break;

                    case "PPID_SN":
                        BoardSerialNumbers[i] += boardParts.Length > 1 ? $"_{boardParts[1]}" : string.Empty;
                        if (boardParts.Length > 0)
                        {
                            BoardSerialNumbers[i] += $"_{boardParts[0]}";
                            CreateDdbIfNeeded(ddbSet, predDdb, ddbPath, ddbBasePath, 
                                boardParts[0], ddbAddress, ddbChecksum, false);
                        }
                        break;

                    case "PPID_MAC":
                        if (boardParts.Length < 3)
                        {
                            StartThreadSafe(Messagemacfail);
                            BoardSerialNumbers[i] += boardParts.Length > 1 ? $"_{boardParts[1]}" : string.Empty;
                        }
                        else
                        {
                            BoardSerialNumbers[i] += $"_{boardParts[1]}_{boardParts[2]}";
                            CreateDdbIfNeeded(ddbSet, predDdb, ddbPath, ddbBasePath, 
                                boardParts[2], ddbAddress, ddbChecksum, true);
                        }
                        break;

                    case "SN_MAC":
                        if (boardParts.Length < 3)
                        {
                            StartThreadSafe(Messagemacfail);
                            BoardSerialNumbers[i] += boardParts.Length > 0 ? $"_{boardParts[0]}" : string.Empty;
                        }
                        else
                        {
                            BoardSerialNumbers[i] += $"_{boardParts[0]}_{boardParts[2]}";
                            CreateDdbIfNeeded(ddbSet, predDdb, ddbPath, ddbBasePath, 
                                boardParts[2], ddbAddress, ddbChecksum, true);
                        }
                        break;

                    default:
                        BoardSerialNumbers[i] += boardParts.Length > 1 ? $"_{boardParts[1]}" : string.Empty;
                        break;
                }
            }
        }

        /// <summary>
        /// 根据需要创建 DDB 文件
        /// </summary>
        private void CreateDdbIfNeeded(bool ddbSet, string predDdb, string ddbPath, 
            string ddbBasePath, string identifier, string ddbAddress, string ddbChecksum, bool isMac)
        {
            if (!ddbSet) return;

            string ddbFile = $"{ddbPath}\\{identifier}.ddb";
            string content = isMac ? ConvertToAsciiHex(identifier) : identifier;

            var createDdb = new creatddb(ddbFile, ddbBasePath, content, ddbAddress, ddbChecksum);
            StartThreadSafe(createDdb.CreateTestDDB);
        }

        /// <summary>
        /// 将字符串转换为 ASCII 十六进制格式
        /// </summary>
        private string ConvertToAsciiHex(string input)
        {
            return string.Concat(input.Select(c => ((int)c).ToString("X2")));
        }

        /// <summary>
        /// 检查站别配置
        /// </summary>
        private Prejudgement CheckStationConfig(Iniconfig iniConfig, string[] stations, int boardNumber)
        {
            try
            {
                string ngStationConfig = iniConfig.IniReadValue("CONFIG", "NG_SATION"); 
                string processNeedTest = iniConfig.IniReadValue("CONFIG", "PROCESS_NEED_TEST");
                string[] ngStations = ngStationConfig.Split(',').Select(s => s.Trim().ToUpper()).ToArray();
                
                // 修复站别匹配逻辑 - 检查所有站别
                bool isNgStation = stations.Take(boardNumber)
                    .Select(s => s.Trim().ToUpper())
                    .Any(s => ngStations.Contains(s));

                if (!isNgStation)
                {
                    return Prejudgement.Test;
                }

                switch (processNeedTest)
                {
                    case "DoNotTest_Pass":
                        return Prejudgement.DoNotTest_Pass;
                    case "DoNotTest_Fail":
                        return Prejudgement.DoNotTest_Fail;
                    case "Test":
                        return Prejudgement.Test;
                    case "Panic":
                        StartThreadSafe(Messagestationfail);
                        return Prejudgement.Panic;
                    default:
                        StartThreadSafe(Messagestationfail);
                        return Prejudgement.Panic;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StationCheck] 异常：{ex.Message}");
                return Prejudgement.Panic; 
            }
        }

        /// <summary>
        /// 安全地启动线程
        /// </summary>
        private void StartThreadSafe(ThreadStart action)
        {
            try
            {
                var thread = new Thread(action);
                thread.IsBackground = true;
                thread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"线程启动失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 安全地启动带参数的线程
        /// </summary>
        private void StartThreadSafe(ParameterizedThreadStart action, object parameter)
        {
            try
            {
                var thread = new Thread(action);
                thread.IsBackground = true;
                thread.Start(parameter);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"线程启动失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 清理 ACK 文件
        /// </summary>
        private void CleanupAckFile()
        {
            try
            {
                string cuttPath = AppDomain.CurrentDomain.BaseDirectory;
                string ackFile = Path.Combine(cuttPath, "ACK.txt");
                if (File.Exists(ackFile))
                {
                    File.Delete(ackFile);
                }
            }
            catch { }
        }

        /// <summary>
        /// 移动 ACK 文件
        /// </summary>
        private void MoveAckFile()
        {
            try
            {
                string setPath = Path.GetDirectoryName(IniPath);
                string cuttPath = AppDomain.CurrentDomain.BaseDirectory;
                string sourceFile = Path.Combine(cuttPath, "ACK.txt");
                string destFile = Path.Combine(setPath, "ACK.txt");

                if (File.Exists(destFile))
                {
                    File.Delete(destFile);
                }

                if (File.Exists(sourceFile))
                {
                    File.Move(sourceFile, destFile);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"移动 ACK 文件失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 获取板卡序列号（多板面板支持）
        /// </summary>
        public virtual string[] GetBoardSerialNumbers(string serialNumber)
        {
            return BoardSerialNumbers ?? new[] { serialNumber };
        }

        #endregion

        #region TAP 主窗口唤醒

        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        /// <summary>
        /// 弹出提示窗口前调用：若 TAP 主窗口处于最小化状态，先还原并置前，
        /// 以保证提示窗口能显示在最顶层。
        /// </summary>
        private void RestoreTapMainWindow()
        {
            try
            {
                IntPtr hWnd = GetTapMainWindowHandle();
                if (hWnd == IntPtr.Zero)
                {
                    return;
                }

                if (IsIconic(hWnd))
                {
                    ShowWindow(hWnd, SW_RESTORE);
                }

                SetForegroundWindow(hWnd);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RestoreTapMainWindow] 还原 TAP 主窗口失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 获取 TAP 主窗口句柄：优先使用进程主窗口，取不到时枚举当前进程的顶层可见窗口
        /// </summary>
        private IntPtr GetTapMainWindowHandle()
        {
            IntPtr hWnd = Process.GetCurrentProcess().MainWindowHandle;
            if (hWnd != IntPtr.Zero)
            {
                return hWnd;
            }

            IntPtr result = IntPtr.Zero;
            uint currentPid = (uint)Process.GetCurrentProcess().Id;

            EnumWindows((hWndEnum, lParam) =>
            {
                uint pid;
                GetWindowThreadProcessId(hWndEnum, out pid);

                if (pid != currentPid)
                {
                    return true;
                }
                if (!IsWindowVisible(hWndEnum))
                {
                    return true;
                }
                if (GetWindowTextLength(hWndEnum) == 0)
                {
                    return true;
                }

                result = hWndEnum;
                return false;
            }, IntPtr.Zero);

            return result;
        }

        #endregion

        #region 消息显示方法

        /// <summary>
        /// 通过 Socket Server 广播消息
        /// </summary>
        private void BroadcastMessage(string msg)
        {
            if (_socketServer == null)
            {
                Console.WriteLine("[Broadcast] 服务器未初始化，无法发送消息。");
                return;
            }

            if (string.IsNullOrEmpty(msg))
                return;

            try
            {
                int count = _socketServer.BroadcastString(msg);
                Console.WriteLine($"[Broadcast] 消息已广播: {msg} (接收客户端数: {count})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Broadcast] 广播失败: {ex.Message}");
            }
        }

        public virtual void Message3fail()
        {
            try
            {
                // 先确保 TAP 主窗口已还原，避免弹窗被最小化的主窗口遮住
                RestoreTapMainWindow();

                // [修改] 直接广播，不再读取 SERVER/SWITCH
                BroadcastMessage("Continuetestfailed");

                var iniConfig = new Iniconfig();
                iniConfig.INIFile(IniPath);

                if (_msgWindow2 != null && !_msgWindow2.IsDisposed)
                {
                    _msgWindow2.Activate();
                    _msgWindow2.BringToFront();
                    _msgWindow2.ShowDialog();
                    iniConfig.IniWriteValue("CONFIG", "FAIL_COUNT", "0");
                    return;
                }

                _msgWindow2 = new Form2("连续三片不良！");
                _msgWindow2.Text = "Continue test failed";
                _msgWindow2.StartPosition = FormStartPosition.CenterScreen;
                _msgWindow2.ShowDialog();

                iniConfig.IniWriteValue("CONFIG", "FAIL_COUNT", "0");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"消息显示失败：{ex.Message}");
            }
        }

        public virtual void Messageackfail()
        {
            MessageBox.Show("ACK.txt 获取失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public virtual void Messagesnfail()
        {
            MessageBox.Show("INI 里面板子数量不对", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public virtual void Messageskufail(object sku)
        {
            MessageBox.Show($"{sku}: PretestPostTestLib.ini 里没有这个状态", "错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public virtual void Messageidfail(object id)
        {
            MessageBox.Show($"{id}: PretestPostTestLib.ini 里 fixID 设置不匹配", "错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public virtual void Messageddbfail(object path)
        {
            MessageBox.Show($"{path}: 不存在", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public virtual void Messageskufail2(object sku)
        {
            MessageBox.Show($"{sku}: PretestPostTestLib.ini 里设置有误", "错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public virtual void Messagestationfail()
        {
            var iniConfig = new Iniconfig();
            iniConfig.INIFile(IniPath);
            try
            {
                // 先确保 TAP 主窗口已还原，避免弹窗被最小化的主窗口遮住
                RestoreTapMainWindow();

                string station = Getbom.station_check;
                string sn = Getbom.batcode_all;
                string[] sns = sn.Split(';');
                string message = string.Join("\n", sns.Where(s => !string.IsNullOrEmpty(s)));
                message += $"\n{station}: 站别错误";
                
                // [修改] 直接广播，不再读取 SERVER/SWITCH
                BroadcastMessage("StationError");

                if (_msgWindow != null && !_msgWindow.IsDisposed)
                {
                    _msgWindow.Activate();
                    _msgWindow.BringToFront();
                    _msgWindow.ShowDialog();
                    return;
                }

                _msgWindow = new Form1(message);
                _msgWindow.Text = "StationError";
                _msgWindow.StartPosition = FormStartPosition.CenterScreen;
                _msgWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"站别错误消息显示失败：{ex.Message}");
            }
        }

        public virtual void Messagemacfail()
        {
            MessageBox.Show("没有 MAC，请更改 PretestPostTestLib.ini 里 BARCODE 设置", "错误", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        #endregion

        #region IPosttestProcessor 成员

        /// <summary>
        /// 处理测试结果
        /// </summary>
        /// <param name="serialNumbers">测试板卡的序列号</param>
        /// <param name="pfResults">各板卡的测试结果</param>
        /// <param name="results">详细测试数据</param>
        /// <returns>最终判定结果</returns>
        public FinalJudgement ProcessResults(string[] serialNumbers, TestResult[] pfResults, byte[] results)
        {
            try
            {
                var iniConfig = new Iniconfig();
                iniConfig.INIFile(IniPath);

                bool allPass = true;
                FinalJudgement finalJudgement = FinalJudgement.NoJudgement;

                foreach (TestResult pfResult in pfResults)
                {
                    if (pfResult == TestResult.Fail)
                    {
                        allPass = false;
                    }
                    if (pfResult == TestResult.Untested || pfResult == TestResult.Abort)
                    {
                        finalJudgement = FinalJudgement.ForceFail;
                    }
                }

                // 更新失败计数
                if (allPass)
                {
                    iniConfig.IniWriteValue("CONFIG", "FAIL_COUNT", "0");
                }
                else
                {
                    string currentCount = iniConfig.IniReadValue("CONFIG", "FAIL_COUNT");
                    int newCount = Convert.ToInt32(currentCount) + 1;
                    iniConfig.IniWriteValue("CONFIG", "FAIL_COUNT", newCount.ToString());
                }

                return finalJudgement;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"结果处理异常：{ex.Message}");
                return FinalJudgement.NoJudgement;
            }
        }

        #endregion
    }
}