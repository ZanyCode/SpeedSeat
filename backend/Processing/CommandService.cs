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

    private SerialPort? serialPort;

    private SemaphoreSlim writeDataSemaphore = new SemaphoreSlim(1);

    private SemaphoreSlim responseReceivedSemaphore = new SemaphoreSlim(0, 1);

    private bool waitingForResponse = false;

    private SerialWriteResult currentSerialWriteResult = SerialWriteResult.Success;

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
    
        serialPort.DataReceived += (sender, args) => {
            byte[] data = new byte[this.serialPort.BytesToRead];
            this.serialPort.Read(data, 0, data.Length);
            currentSerialWriteResult = data.Any(x => x == 255) ? SerialWriteResult.Success : SerialWriteResult.InvalidHash;
            if(waitingForResponse)
            {
                waitingForResponse = false;
                responseReceivedSemaphore.Release();
            }     
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

            var data = command.ToByteArray();
            waitingForResponse = true;
            this.serialPort.Write(data, 0, data.Length);   
            bool receivedResponse = await responseReceivedSemaphore.WaitAsync(2000);

            if(!receivedResponse)
            {
                string error = "Value wasn't written since no response from controller was received. Connection closed, please reconnect manually.";
                System.Console.WriteLine(error);
                LogFrontend(error);
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
            this.Disconnect();
            return SerialWriteResult.WriteError;
        }
    }
}