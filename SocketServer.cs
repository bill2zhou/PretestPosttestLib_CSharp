using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/// <summary>
/// 兼容C# 7.3的线程安全Socket服务器类（支持多客户端广播）
/// </summary>
public class SocketServer : IDisposable
{
    // 服务器监听Socket
    private Socket _serverSocket;
    private readonly int _port;
    private readonly Encoding _encoding = Encoding.UTF8;
    
    // 存储已连接的客户端 Socket (Key: 远程EndPoint字符串, Value: Socket实例)
    // 使用 ConcurrentDictionary 保证线程安全的增删改查
    private readonly ConcurrentDictionary<string, Socket> _clients = new ConcurrentDictionary<string, Socket>();
    
    // 线程安全锁对象，用于保护非原子操作或复杂逻辑
    private readonly object _lockObj = new object();
    
    // 状态标记
    private int _isDisposed = 0;
    private int _isListening = 0; // 0=未监听, 1=监听中

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="port">监听端口</param>
    public SocketServer(int port)
    {
        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "端口号必须在1-65535之间");
        
        _port = port;
    }

    /// <summary>
    /// 启动服务器并开始监听
    /// </summary>
    public void Start()
    {
        if (Interlocked.CompareExchange(ref _isListening, 1, 0) == 1)
        {
            Console.WriteLine("服务器已经在运行中。");
            return;
        }

        try
        {
            // 创建监听Socket
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            // 允许端口复用
            _serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            
            // 绑定IP和端口
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, _port);
            _serverSocket.Bind(endPoint);
            
            // 开始监听，最大队列长度100
            _serverSocket.Listen(100);
            
            Console.WriteLine($"服务器启动成功，正在监听端口: {_port}");

            // 启动异步接受客户端连接的线程
            Thread acceptThread = new Thread(AcceptClientsLoop);
            acceptThread.IsBackground = true;
            acceptThread.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"服务器启动失败: {ex.Message}");
            Interlocked.Exchange(ref _isListening, 0);
            throw;
        }
    }

    /// <summary>
    /// 接受客户端连接的循环线程
    /// </summary>
    private void AcceptClientsLoop()
    {
        while (_isListening == 1 && _isDisposed == 0)
        {
            try
            {
                // 阻塞等待新连接
                Socket clientSocket = _serverSocket.Accept();
                
                // 获取客户端标识
                string clientKey = clientSocket.RemoteEndPoint.ToString();
                
                // 添加到客户端列表
                if (_clients.TryAdd(clientKey, clientSocket))
                {
                    Console.WriteLine($"客户端已连接: {clientKey}，当前在线人数: {_clients.Count}");
                    
                    // 为每个客户端启动独立的接收线程
                    Thread receiveThread = new Thread(() => ReceiveDataLoop(clientSocket, clientKey));
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }
                else
                {
                    // 如果已存在（极少情况），关闭新连接
                    clientSocket.Close();
                }
            }
            catch (ObjectDisposedException)
            {
                // 服务器关闭时抛出，正常退出循环
                break;
            }
            catch (Exception ex)
            {
                if (_isListening == 1 && _isDisposed == 0)
                {
                    Console.WriteLine($"接受连接时发生错误: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 单个客户端的数据接收循环
    /// </summary>
    private void ReceiveDataLoop(Socket clientSocket, string clientKey)
    {
        byte[] buffer = new byte[4096];
        
        try
        {
            while (_isListening == 1 && _isDisposed == 0 && clientSocket.Connected)
            {
                // 接收数据
                int bytesRead = clientSocket.Receive(buffer);
                
                if (bytesRead <= 0)
                {
                    // 客户端断开连接
                    break;
                }

                string message = _encoding.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"收到来自 [{clientKey}] 的消息: {message}");
                
                // 这里可以添加消息处理逻辑，例如解析命令等
                // HandleMessage(clientKey, message);
            }
        }
        catch (SocketException ex)
        {
            // 连接异常断开
            Console.WriteLine($"客户端 [{clientKey}] 连接异常: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理客户端 [{clientKey}] 数据时出错: {ex.Message}");
        }
        finally
        {
            // 清理断开的客户端
            RemoveClient(clientKey, clientSocket);
        }
    }

    /// <summary>
    /// 移除客户端并释放资源
    /// </summary>
    private void RemoveClient(string clientKey, Socket socket)
    {
        // 从字典中移除
        if (_clients.TryRemove(clientKey, out Socket removedSocket))
        {
            try
            {
                if (removedSocket.Connected)
                {
                    removedSocket.Shutdown(SocketShutdown.Both);
                }
            }
            catch { }
            finally
            {
                removedSocket.Close();
                removedSocket.Dispose();
            }
            Console.WriteLine($"客户端已断开: {clientKey}，当前在线人数: {_clients.Count}");
        }
    }

    /// <summary>
    /// 向所有已连接的客户端广播字符串消息 (已优化：添加长度前缀防止粘包)
    /// </summary>
    /// <param name="message">要广播的消息</param>
    /// <returns>成功发送到的客户端数量</returns>
    public int BroadcastString(string message)
    {
        if (string.IsNullOrEmpty(message))
            return 0;

        if (_isListening == 0 || _isDisposed == 1)
        {
            Console.WriteLine("广播失败：服务器未启动或已关闭");
            return 0;
        }

        // 1. 编码消息
        byte[] data = _encoding.GetBytes(message);

        // 2. 构建长度前缀 (4字节 Int32, Little-Endian)
        // BitConverter.GetBytes 默认根据系统架构，Windows .NET 通常为 Little-Endian
        byte[] lengthPrefix = BitConverter.GetBytes(data.Length);

        int successCount = 0;

        foreach (var kvp in _clients)
        {
            string clientKey = kvp.Key;
            Socket clientSocket = kvp.Value;

            try
            {
                if (clientSocket != null && clientSocket.Connected)
                {
                    // 3. 先发送长度头 (4字节)
                    clientSocket.Send(lengthPrefix);

                    // 4. 再发送数据体
                    clientSocket.Send(data);

                    successCount++;
                }
                else
                {
                    RemoveClient(clientKey, clientSocket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"向客户端 [{clientKey}] 广播失败: {ex.Message}");
                RemoveClient(clientKey, clientSocket);
            }
        }

        // Console.WriteLine($"广播完成，成功发送至 {successCount} 个客户端");
        return successCount;
    }
    /// <summary>
    /// 停止服务器
    /// </summary>
    public void Stop()
    {
        if (Interlocked.CompareExchange(ref _isListening, 0, 1) == 0)
        {
            Console.WriteLine("服务器未在运行。");
            return;
        }

        Console.WriteLine("正在停止服务器...");

        // 关闭所有客户端连接
        foreach (var kvp in _clients)
        {
            RemoveClient(kvp.Key, kvp.Value);
        }
        _clients.Clear();

        // 关闭监听Socket
        lock (_lockObj)
        {
            if (_serverSocket != null)
            {
                try
                {
                    if (_serverSocket.Connected)
                        _serverSocket.Shutdown(SocketShutdown.Both);
                }
                catch { }
                finally
                {
                    _serverSocket.Close();
                    _serverSocket.Dispose();
                    _serverSocket = null;
                }
            }
        }
        
        Console.WriteLine("服务器已停止。");
    }

    /// <summary>
    /// 获取当前在线客户端数量
    /// </summary>
    public int ConnectedClientCount
    {
        get { return _clients.Count; }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 1)
            return;

        Stop();

        if (disposing)
        {
            // 释放其他托管资源
        }
    }

    ~SocketServer()
    {
        Dispose(false);
    }
}