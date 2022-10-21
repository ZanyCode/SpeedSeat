using System;
using System.IO.Ports;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

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
    private readonly IOptionsMonitor<Config> options;
    private readonly ISpeedseatSettings settings;

    public bool IsConnected => this.serialPort != null;


    private int commandReadTimeoutMs = 500;
    Timer commandReadTimer;
    private bool isReadingCommand = false;

    public CommandService(IFrontendLogger frontendLogger, ISerialPortConnectionFactory serialPortConnectionFactory, IOptionsMonitor<Config> options, ISpeedseatSettings settings)
    {
        this.frontendLogger = frontendLogger;
        this.serialPortConnectionFactory = serialPortConnectionFactory;
        this.options = options;
        this.settings = settings;
        commandReadTimer = new Timer(x =>
        {
            this.isReadingCommand = false;
            if (currentCommandBytes.Count > 0)
            {
                frontendLogger.Log($"Error: Cancelling incomplete command read process due to timeout. Currently {currentCommandBytes.Count} read. Buffer content before delete: {Convert.ToHexString(currentCommandBytes.ToArray())}");
            }
            this.currentCommandBytes.Clear();
        }, null, Timeout.Infinite, Timeout.Infinite);
    }

    public bool Connect(string port, int baudrate)
    {
        // Create a new SerialPort object with default settings.
        serialPort = serialPortConnectionFactory.Create(port, baudrate);
        serialPort.ErrorReceived += (sender, args) =>
        {
            this.Disconnect();
            frontendLogger.Log($"Serial port experienced unexpected error, disconnecting ({args.EventType})");
        };

        serialPort.DataReceived += (sender, args) =>
        {
            byte[] data = new byte[this.serialPort.BytesToRead];
            this.serialPort.Read(data, 0, data.Length);
            foreach (var dataByte in data)
            {
                if (!isReadingCommand && (dataByte == 0xFF || dataByte == 0xFE))
                {
                    ResetCommandBytes();
                    currentSerialWriteResult = dataByte == 0xFF ? SerialWriteResult.Success : SerialWriteResult.InvalidHash;
                    responseReceivedSemaphore.Release();
                    waitingForResponse = false;
                }
                else
                    ProcessCommandByte(dataByte);
            }

            // frontendLogger.Log($"Got message {Convert.ToHexString(data)}");
        };

        serialPort.Open();
        return true;
    }

    private List<byte> currentCommandBytes = new List<byte>();
    private async Task ProcessCommandByte(byte dataByte)
    {
        isReadingCommand = true;
        commandReadTimer.Change(commandReadTimeoutMs, Timeout.Infinite);

        currentCommandBytes.Add(dataByte);
        if (currentCommandBytes.Count >= 8)
        {
            var commandData = currentCommandBytes.ToArray();
            currentCommandBytes.Clear();
            isReadingCommand = false;

            if (Command.IsHashValid(commandData))
            {
                var id = Command.ExtractIdFromByteArray(commandData.ToArray());
                var command = options.CurrentValue.Commands.SingleOrDefault(x => x.Id == id);
                if (command == null)
                {
                    frontendLogger.Log($"Error: Received command with id {id}, but this command does not exist in config.json. Responding with 0xFE.");
                    await SendAcknowledgeByteToMicrocontroller(false);
                }
                else
                {
                    var updatedCommand = command.CloneWithNewValuesFromByteArray(commandData.ToArray());
                    await SendAcknowledgeByteToMicrocontroller(true);

                    if (updatedCommand.IsReadRequest)
                    {
                        var (value1, value2, value3) = settings.GetConfigurableSettingsValues(updatedCommand);
                        updatedCommand = updatedCommand.CloneWithNewValues(value1, value2, value3, false);
                        frontendLogger.Log($"Successfully received read-request for command with id {updatedCommand.Id}, raw request: {Convert.ToHexString(commandData)}. Sending response with value {updatedCommand.ToString()}, raw response: {Convert.ToHexString(updatedCommand.ToByteArray())}");
                        var result = await WriteBytesToSerialPort(updatedCommand.ToByteArray());
                        if (result != SerialWriteResult.Success)
                        {
                            var errorMsg = $"Successfully received valid read request from UC, but unable to write response: {result}";
                            frontendLogger.Log(errorMsg);
                            throw new Exception(errorMsg);
                        }
                    }
                    else
                    {
                        frontendLogger.Log($"Successfully received write-request for command with id {updatedCommand.Id}, raw request: {Convert.ToHexString(commandData)}, interpreted as Command: {updatedCommand.ToString()}). Writing values to Database.");
                        this.settings.SaveConfigurableSetting(updatedCommand);
                    }
                }
            }
            else
            {
                frontendLogger.Log($"Error: Received command with invalid hash: {Convert.ToHexString(commandData)}");
                await SendAcknowledgeByteToMicrocontroller(false);
            }
        }
    }

    private void ResetCommandBytes()
    {
        currentCommandBytes.Clear();
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
        var data = command.ToByteArray();
        return await WriteBytesToSerialPort(data);
    }

    private async Task<SerialWriteResult> WriteBytesToSerialPort(byte[] data)
    {
        try
        {
            if (serialPort == null)
                throw new Exception("Attempted to write data to a closed connection");

            commandCount++;
            bool receivedResponse = false;
            await writeDataSemaphore.WaitAsync();

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                waitingForResponse = true;
                // frontendLogger.Log($"Sending message {Convert.ToHexString(data)}");
                this.serialPort.Write(data, 0, data.Length);
                receivedResponse = await responseReceivedSemaphore.WaitAsync(500);

                if (!receivedResponse)
                {
                    string error = $"Error writing value: No response from controller received (Attempt {attempt}).";
                    System.Console.WriteLine(error);
                    frontendLogger.Log(error);
                }
                else
                {
                    break;
                }
            }

            if (!receivedResponse)
            {
                throw new Exception($"Value wasn't written since no response from controller was received after 3 attempts.");
            }

            if ((DateTime.Now - latestPerformaceUpdate).TotalMilliseconds > 1000)
            {
                double fps = commandCount / (DateTime.Now - latestPerformaceUpdate).TotalMilliseconds * 1000;
                commandCount = 0;
                latestPerformaceUpdate = DateTime.Now;
                System.Console.WriteLine($"Commands/Second: {fps}");
            }

            writeDataSemaphore.Release();
            return receivedResponse ? currentSerialWriteResult : SerialWriteResult.Timeout;
        }
        catch (Exception e)
        {
            var message = $"Error writing value: {e.Message}";
            System.Console.WriteLine(message);
            frontendLogger.Log(message);
            writeDataSemaphore.Release();
            return SerialWriteResult.WriteError;
        }
    }

    private async Task SendAcknowledgeByteToMicrocontroller(bool isValid)
    {
        await writeDataSemaphore.WaitAsync();
        this.serialPort?.Write(new[] { (byte)(isValid ? 0xFF : 0xFE) }, 0, 1);
        writeDataSemaphore.Release();
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