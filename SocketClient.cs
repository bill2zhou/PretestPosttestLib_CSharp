using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/// <summary>
/// 兼容C# 7.3的线程安全Socket客户端类（包含字符串发送功能）
/// </summary>
public class SocketClient : IDisposable
{
    // 私有字段存储连接信息和Socket实例
    private readonly string _ipAddress;
    private readonly int _port;
    private Socket _clientSocket;
    private int _isDisposed = 0; // 0=未释放，1=已释放（用int适配Interlocked）
    private readonly Encoding _encoding = Encoding.UTF8;

    // 线程安全锁对象（私有静态确保唯一性）
    private readonly object _lockObj = new object();
    // 连接状态：0=未连接，1=已连接（用int适配Interlocked）
    private int _isConnected = 0;

    /// <summary>
    /// 构造函数，初始化IP和端口
    /// </summary>
    /// <param name="ipAddress">服务器IP地址（如 "127.0.0.1"）</param>
    /// <param name="port">服务器端口号（如 8888）</param>
    /// <exception cref="ArgumentNullException">IP地址为空时抛出</exception>
    /// <exception cref="ArgumentOutOfRangeException">端口号不合法时抛出</exception>
    public SocketClient(string ipAddress, int port)
    {
        if (string.IsNullOrEmpty(ipAddress))
            throw new ArgumentNullException(nameof(ipAddress), "IP地址不能为空");

        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "端口号必须在1-65535之间");

        _ipAddress = ipAddress;
        _port = port;
        _clientSocket = null;
    }

    /// <summary>
    /// 线程安全的连接方法（防止多线程重复连接）
    /// </summary>
    /// <returns>连接成功返回true，失败返回false</returns>
    public bool Connect()
    {
        // 双重检查锁：外层无锁快速判断
        if (IsConnected)
            return true;

        lock (_lockObj)
        {
            // 加锁后再次检查，避免多线程竞争
            if (IsConnected || _isDisposed == 1)
                return IsConnected;

            try
            {
                // 断开已有无效连接
                if (_clientSocket != null)
                {
                    if (_clientSocket.Connected)
                    {
                        try { _clientSocket.Shutdown(SocketShutdown.Both); }
                        catch { }
                        _clientSocket.Close();
                    }
                    _clientSocket.Dispose();
                }

                // 创建新Socket实例
                _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _clientSocket.SendTimeout = 5000;
                _clientSocket.ReceiveTimeout = 5000;

                // 连接服务器
                IPAddress ip = IPAddress.Parse(_ipAddress);
                IPEndPoint endPoint = new IPEndPoint(ip, _port);
                _clientSocket.Connect(endPoint);

                // 原子更新连接状态（1=已连接）
                if (_clientSocket.Connected)
                    Interlocked.Exchange(ref _isConnected, 1);

                return _clientSocket.Connected;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket连接失败：{ex.Message} (错误码: {ex.SocketErrorCode})");
                Interlocked.Exchange(ref _isConnected, 0);
                return false;
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"IP地址格式错误：{ex.Message}");
                Interlocked.Exchange(ref _isConnected, 0);
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"连接发生未知错误：{ex.Message}");
                Interlocked.Exchange(ref _isConnected, 0);
                return false;
            }
        }
    }

    /// <summary>
    /// 线程安全的字符串发送方法
    /// </summary>
    /// <param name="data">要发送的字符串</param>
    /// <returns>发送成功返回true，失败返回false</returns>
    public bool SendString(string data)
    {
        // 前置快速校验（无锁）
        if (string.IsNullOrEmpty(data))
        {
            Console.WriteLine("发送失败：待发送的字符串不能为空");
            return false;
        }

        if (IsConnected == false || _isDisposed == 1)
        {
            Console.WriteLine("发送失败：未连接到服务器或实例已释放，请先调用Connect()方法");
            return false;
        }

        lock (_lockObj)
        {
            // 加锁后再次校验（防止校验后状态变化）
            if (IsConnected == false || _isDisposed == 1 || _clientSocket == null)
                return false;

            try
            {
                byte[] sendBytes = _encoding.GetBytes(data);
                int sentBytes = _clientSocket.Send(sendBytes);

                if (sentBytes == sendBytes.Length)
                {
                    Console.WriteLine($"发送成功，共发送 {sentBytes} 字节数据：{data}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"发送失败：仅发送了 {sentBytes} 字节，总需发送 {sendBytes.Length} 字节");
                    return false;
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"发送数据时发生Socket异常：{ex.Message} (错误码: {ex.SocketErrorCode})");
                // 发送异常时标记为未连接
                Interlocked.Exchange(ref _isConnected, 0);
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送数据时发生未知错误：{ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 线程安全的连接状态检查
    /// </summary>
    public bool IsConnected
    {
        get
        {
            // 原子读取连接状态（避免线程间可见性问题）
            if (Interlocked.CompareExchange(ref _isConnected, 0, 0) == 0)
                return false;

            // 双重验证：原子标记 + Socket实际连接状态
            lock (_lockObj)
            {
                if (_clientSocket == null || _isDisposed == 1)
                    return false;

                try
                {
                    // 额外检查Socket是否真的连接（处理异常断开场景）
                    return !(_clientSocket.Poll(1, SelectMode.SelectRead) && _clientSocket.Available == 0)
                           && _clientSocket.Connected;
                }
                catch
                {
                    Interlocked.Exchange(ref _isConnected, 0);
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// 线程安全的断开连接方法
    /// </summary>
    public void Disconnect()
    {
        lock (_lockObj)
        {
            if (_clientSocket == null || _isDisposed == 1)
                return;

            try
            {
                if (_clientSocket.Connected)
                {
                    _clientSocket.Shutdown(SocketShutdown.Both);
                }
            }
            catch { }
            finally
            {
                _clientSocket.Close();
                _clientSocket.Dispose();
                _clientSocket = null;
                // 原子更新连接状态
                Interlocked.Exchange(ref _isConnected, 0);
            }
        }
    }

    /// <summary>
    /// 线程安全的资源释放（实现IDisposable接口）
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // 原子检查是否已释放，避免重复释放（用int的CompareExchange替代bool）
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 1)
            return;

        if (disposing)
        {
            lock (_lockObj)
            {
                // 释放托管资源
                Disconnect();
            }
        }
    }

    ~SocketClient()
    {
        Dispose(false);
    }
}