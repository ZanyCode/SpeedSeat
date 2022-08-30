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

public class Command
{
    public byte Id { get; set; }

    public ushort Value1 { get; set; }

    public ushort Value2 { get; set; }

    public ushort Value3 { get; set; }

    public Command(byte id, ushort value1 = 0, ushort value2 = 0, ushort value3 = 0)
    {
        Id = id;
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
    }

    public byte[] ToByteArray()
    {
        var (val1Msb, val1Lsb) = UShortToBytes(Value1);
        var (val2Msb, val2Lsb) = UShortToBytes(Value2);
        var (val3Msb, val3Lsb) = UShortToBytes(Value3);

        var bytes = new byte[7];
        bytes[0] = 0;
        bytes[1] = val1Msb;
        bytes[2] = val1Lsb;
        bytes[3] = val2Msb;
        bytes[4] = val2Lsb;
        bytes[5] = val3Msb;
        bytes[6] = val3Lsb;

        return bytes;
    }

    private (byte msb, byte lsb) UShortToBytes(ushort value) {
        var bytes = BitConverter.GetBytes(value);
        return (bytes[1], bytes[0]);
    }
}

public class CommandService
{
    private int commandCount = 0;
    private DateTime latestPerformaceUpdate = DateTime.Now;

    private SerialPort? serialPort;

    private SemaphoreSlim writeDataSemaphore = new SemaphoreSlim(1);
    private readonly IHubContext<InfoHub> hubContext;

    public bool IsConnected => this.serialPort != null;

    public CommandService(IHubContext<InfoHub> hubContext)
    {
        this.hubContext = hubContext;
    }

    private void LogFrontend(string message)
    {
        this.hubContext.Clients.All.SendAsync("log", message);
    }

    public bool Connect(string port, int baudrate)
    {
        // Create a new SerialPort object with default settings.
        serialPort = new SerialPort(port);        
        serialPort.BaudRate = baudrate;        
        serialPort.ReadTimeout = 500;
        serialPort.WriteTimeout = 500;  
        serialPort.ErrorReceived += (sender, args) => {
            this.Disconnect();
        };
    
        serialPort.Open(); 
        LogFrontend($"Successfully connected to port {port} with baud rate {baudrate}");
        return true;
    }

    public void Disconnect()
    {        
        this.writeDataSemaphore.Release();
        this.serialPort?.Dispose();
        this.serialPort = null;
        LogFrontend("Connection to Serial Port closed");
    }

    public async Task<SerialWriteResult> WriteCommand(Command command)
    {
        try
        {
            if(serialPort == null)
                throw new Exception("Attempted to write data to a closed connection");

            commandCount++;
            await writeDataSemaphore.WaitAsync();
            var responseReceivedSemaphore = new SemaphoreSlim(0, 1);
            SerialWriteResult result = SerialWriteResult.InvalidHash;

            SerialDataReceivedEventHandler dataReceivedFunc = (sender, args) => {
                byte[] data = new byte[this.serialPort.BytesToRead];
                this.serialPort.Read(data, 0, data.Length);
                result = data.Any(x => x == 255) ? SerialWriteResult.Success : SerialWriteResult.InvalidHash;     
                responseReceivedSemaphore.Release();
            };   

            var data = command.ToByteArray();
            this.serialPort.DataReceived += dataReceivedFunc;     
            this.serialPort.Write(data, 0, data.Length);   
            bool receivedResponse = await responseReceivedSemaphore.WaitAsync(2000);

            if(!receivedResponse)
            {
                string error = "Value wasn't written since no response from controller was received. Connection closed, please reconnect manually.";
                System.Console.WriteLine(error);
                LogFrontend(error);
                throw new Exception(error);
            }

            this.serialPort.DataReceived -= dataReceivedFunc;

            writeDataSemaphore.Release();

            if((DateTime.Now - latestPerformaceUpdate).TotalMilliseconds > 1000)
            {
                double fps = commandCount / (DateTime.Now - latestPerformaceUpdate).TotalMilliseconds * 1000;
                commandCount = 0;
                latestPerformaceUpdate = DateTime.Now;
                System.Console.WriteLine($"Commands/Second: {fps}"); 
            }
            
            return receivedResponse? result : SerialWriteResult.Timeout;
        }
        catch
        {
            writeDataSemaphore.Release();
            this.Disconnect();
            return SerialWriteResult.WriteError;
        }
    }
}