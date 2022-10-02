using System;
using System.IO.Ports;
using Microsoft.AspNetCore.SignalR;

public enum SerialWriteResult
{
    Success,
    InvalidHash,
    Timeout,
    WriteError    
}

public class CommandService
{
    private int commandCount = 0;
    private DateTime latestPerformaceUpdate = DateTime.Now;

    private ISerialPortConnection? serialPort;

    private SemaphoreSlim writeDataSemaphore = new SemaphoreSlim(1);

    private SemaphoreSlim responseReceivedSemaphore = new SemaphoreSlim(0, 1);

    private bool waitingForResponse = false;

    private SerialWriteResult currentSerialWriteResult = SerialWriteResult.Success;
    private readonly IFrontendLogger frontendLogger;
    private readonly ISerialPortConnectionFactory serialPortConnectionFactory;

    public bool IsConnected => this.serialPort != null;

    public CommandService(IFrontendLogger frontendLogger, ISerialPortConnectionFactory serialPortConnectionFactory)
    {
        this.frontendLogger = frontendLogger;
        this.serialPortConnectionFactory = serialPortConnectionFactory;
    }

    public bool Connect(string port, int baudrate)
    {
        // Create a new SerialPort object with default settings.
        serialPort = serialPortConnectionFactory.Create(port, baudrate);
        serialPort.ErrorReceived += (sender, args) => {
            this.Disconnect();
            frontendLogger.Log($"Serial port experienced unexpected error, disconnecting ({args.EventType})");
        };
    
        serialPort.DataReceived += (sender, args) => {
            byte[] data = new byte[this.serialPort.BytesToRead];
            this.serialPort.Read(data, 0, data.Length);
            currentSerialWriteResult = data.Any(x => x == 255) ? SerialWriteResult.Success : SerialWriteResult.InvalidHash;
            if(waitingForResponse)
            {
                waitingForResponse = false;
                responseReceivedSemaphore.Release();
            }     
            frontendLogger.Log($"Got message {Convert.ToHexString(data)}");
        };

        serialPort.Open(); 
        frontendLogger.Log($"Successfully connected to port {port} with baud rate {baudrate}");
        return true;
    }

    public void Disconnect()
    {        
        this.writeDataSemaphore.Release();
        this.serialPort?.Dispose();
        this.serialPort = null;
        frontendLogger.Log("Connection to Serial Port closed");
    }

    public async Task<SerialWriteResult> WriteCommand(Command command)
    {
        try
        {
            if(serialPort == null)
                throw new Exception("Attempted to write data to a closed connection");

            commandCount++;
            await writeDataSemaphore.WaitAsync();  

            var data = command.ToByteArray(write: true);
            waitingForResponse = true;
            frontendLogger.Log($"Sending message {Convert.ToHexString(data)}");
            this.serialPort.Write(data, 0, data.Length);   
            bool receivedResponse = await responseReceivedSemaphore.WaitAsync(2000);

            if(!receivedResponse)
            {
                string error = "Value wasn't written since no response from controller was received.";
                System.Console.WriteLine(error);
                frontendLogger.Log(error);
                throw new Exception(error);
            }

            writeDataSemaphore.Release();

            if((DateTime.Now - latestPerformaceUpdate).TotalMilliseconds > 1000)
            {
                double fps = commandCount / (DateTime.Now - latestPerformaceUpdate).TotalMilliseconds * 1000;
                commandCount = 0;
                latestPerformaceUpdate = DateTime.Now;
                System.Console.WriteLine($"Commands/Second: {fps}"); 
            }
            
            return receivedResponse? currentSerialWriteResult : SerialWriteResult.Timeout;
        }
        catch(Exception e)
        {
            writeDataSemaphore.Release();
            System.Console.WriteLine($"Error writing value: {e.Message}");
            return SerialWriteResult.WriteError;
        }
    }
}

// Thin wrapper around SerialPort to facilitate testing of command service
public interface ISerialPortConnection : IDisposable
{
    public event SerialErrorReceivedEventHandler ErrorReceived;

    public event SerialDataReceivedEventHandler DataReceived;

    public void Open();

    public void Write(byte[] buffer, int offset, int count);

    public int Read(byte[] data, int offset, int count);

    public int BytesToRead { get; }
}

public class SerialPortConnection : ISerialPortConnection
{
    private SerialPort serialPort;
    private readonly string port;
    private readonly int baudrate;
    private readonly IFrontendLogger frontendLogger;

    public int BytesToRead => serialPort.BytesToRead;

    public event SerialErrorReceivedEventHandler ErrorReceived 
    {
        add
        {
            serialPort.ErrorReceived -= value;
            serialPort.ErrorReceived += value;
        }
        remove
        {
            serialPort.ErrorReceived -= value;
        }
    }

    public event SerialDataReceivedEventHandler DataReceived 
    {
        add
        {
            serialPort.DataReceived -= value;
            serialPort.DataReceived += value;
        }
        remove
        {
            serialPort.DataReceived -= value;
        }
    }
    
    public SerialPortConnection(string port, int baudrate, IFrontendLogger frontendLogger)
    {
        // Create a new SerialPort object with default settings.
        serialPort = new SerialPort(port);        
        serialPort.BaudRate = baudrate;        
        serialPort.ReadTimeout = 500;
        serialPort.WriteTimeout = 500;
        this.port = port;
        this.baudrate = baudrate;
        this.frontendLogger = frontendLogger;
    }

    public void Dispose()
    {
        serialPort.Dispose();
    }

    public void Open()
    {
        serialPort.Open();
        frontendLogger.Log($"Successfully connected to port {port} with baud rate {baudrate}");
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        serialPort.Write(buffer, offset, count);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        return serialPort.Read(buffer, offset, count);
    }
}

public interface ISerialPortConnectionFactory
{
    public ISerialPortConnection Create(string port, int baudrate);    
}

public class SerialPortConnectionFactory : ISerialPortConnectionFactory
{
    private readonly IFrontendLogger frontendLogger;

    public SerialPortConnectionFactory(IFrontendLogger frontendLogger)
    {
        this.frontendLogger = frontendLogger;
    }

    public ISerialPortConnection Create(string port, int baudrate)
    {
        return new SerialPortConnection(port, baudrate, frontendLogger);
    }
}

