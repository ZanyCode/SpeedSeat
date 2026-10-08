using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

public enum WriteResult
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

    private IDeviceConnection? connection;

    private SemaphoreSlim writeDataSemaphore = new SemaphoreSlim(1);

    private SemaphoreSlim responseReceivedSemaphore = new SemaphoreSlim(0, 1);

    private bool waitingForResponse = false;

    private WriteResult currentWriteResult = WriteResult.Success;
    private readonly IFrontendLogger frontendLogger;
    private readonly IDeviceConnectionFactory connectionFactory;
    private readonly IOptionsMonitor<Config> options;
    private readonly ISpeedseatSettings settings;
    private readonly IHubContext<SeatSettingsHub> settingsHubContext;

    public bool IsConnected => this.connection != null;

    // Raised whenever the connection is established (true) or dropped (false) so the
    // auto-connect loop and the frontend can react. The whole transport is WiFi/UDP now.
    public event Action<bool>? ConnectionStateChanged;

    // Firmware version reported by the MC in response to the FirmwareVersion read request,
    // null until (and unless) the MC answers — old firmwares don't know the command.
    public ushort? ReportedFirmwareVersion { get; private set; }

    private bool waitingForConnection = false;
    private SemaphoreSlim waitingForConnectionSemaphore = new SemaphoreSlim(0, 1);


    private int commandReadTimeoutMs = 500;
    Timer commandReadTimer;
    private bool isReadingCommand = false;

    public CommandService(IFrontendLogger frontendLogger, IDeviceConnectionFactory connectionFactory, IOptionsMonitor<Config> options, ISpeedseatSettings settings, IHubContext<SeatSettingsHub> settingsHubContext)
    {
        this.frontendLogger = frontendLogger;
        this.connectionFactory = connectionFactory;
        this.options = options;
        this.settings = settings;
        this.settingsHubContext = settingsHubContext;
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

    public async Task<bool> Connect(string address)
    {
        ReportedFirmwareVersion = null;
        connection = connectionFactory.Create(address);

        connection.DataReceived += async (sender, args) =>
        {
            try
            {
                var currentConnection = this.connection;
                if (currentConnection == null)
                    return;

                byte[] data = new byte[currentConnection.BytesToRead];
                currentConnection.Read(data, 0, data.Length);
                if (data.Length > 0)
                    positionsSinceLastReceive = 0;
                foreach (var dataByte in data)
                {
                    if (!isReadingCommand && (dataByte == 0xFF || dataByte == 0xFE))
                    {
                        ResetCommandBytes();
                        if (waitingForResponse)
                        {
                            currentWriteResult = dataByte == 0xFF ? WriteResult.Success : WriteResult.InvalidHash;
                            responseReceivedSemaphore.Release();
                            waitingForResponse = false;
                        }
                        else
                        {
                            // The ack of a motor position (those are never waited on), or the
                            // late ack of a command that already timed out.
                            ConnectionDiagnostics.Event("POSACK", $"{dataByte:X2}");
                        }
                    }
                    else
                    {
                        await ProcessCommandByte(dataByte);
                    }
                }
            }
            catch (Exception e)
            {
                frontendLogger.Log($"Error processing data from Microcontroller: {e.Message}");
            }
        };

        connection.Open();
        waitingForConnection = true;
        int timeoutMs = options.CurrentValue.ConnectionResponseTimeoutMs;
        frontendLogger.Log($"Connection Attempt: Sending Initiate-Connection command to Microcontroller and waiting for response. Specified Timeout from config.json: {timeoutMs}mS");
        await WriteCommand(new Command(Command.InitiateConnectionCommandId, null, null, null, false, false));
        bool connectionConfirmationReceived = true;
        if (waitingForConnection)
            connectionConfirmationReceived = await waitingForConnectionSemaphore.WaitAsync(timeoutMs);

        if (!connectionConfirmationReceived)
        {
            frontendLogger.Log($"Error Connecting: Did not receive connection confirmation within {timeoutMs}mS");
            this.Disconnect();
            return false;
        }

        ConnectionDiagnostics.SetSeatIp(address);
        ConnectionDiagnostics.Event("CONN", $"CONNECTED {address}");
        ConnectionStateChanged?.Invoke(true);
        return true;
    }

    public async Task FakeWriteRequest(Command command)
    {
        this.connection?.FakeWriteBytes(command.ToByteArray());
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

                if (id == Command.ConnectionInitiatedCommandId)
                {
                    if (waitingForConnection)
                    {
                        waitingForConnectionSemaphore.Release();
                        waitingForConnection = false;
                        frontendLogger.Log("Connection Success: Received Connection-Initiated confirmation command from Microcontroller.");
                    }
                    else
                    {
                        frontendLogger.Log("Warning: Received Connection-Initiated confirmation command, but there actually wasn't any unfinished connection attempt. Very weird.");
                    }

                    await SendAcknowledgeByteToMicrocontroller(Command.IsHashValid(commandData));
                    return;
                }

                if (id == Command.FirmwareVersionCommandId)
                {
                    ReportedFirmwareVersion = (ushort)((commandData[1] << 8) | commandData[2]);
                    frontendLogger.Log($"Microcontroller reported firmware version {ReportedFirmwareVersion}.");
                    await SendAcknowledgeByteToMicrocontroller(true);
                    return;
                }

                var command = options.CurrentValue.Commands.SingleOrDefault(x => x.Id == id);
                if (command == null)
                {
                    // A firmware from another release may report a setting this backend doesn't
                    // know (added later, or removed again). Accept and ignore it: answering 0xFE
                    // makes the microcontroller resend it forever, which blocks everything queued
                    // behind it — including its firmware version, without which no update starts.
                    frontendLogger.Log($"Warning: Received command with id {id}, which does not exist in config.json. Ignoring it.");
                    await SendAcknowledgeByteToMicrocontroller(true);
                }
                else
                {
                    try
                    {
                        var updatedCommand = command.CloneWithNewValuesFromByteArray(commandData.ToArray());
                        if (updatedCommand.IsReadRequest)
                        {
                            var (value1, value2, value3) = settings.GetConfigurableSettingsValues(updatedCommand);
                            updatedCommand = updatedCommand.CloneWithNewValues(value1, value2, value3, false);
                            frontendLogger.Log($"Successfully received read-request for command with id {updatedCommand.Id}, raw request: {Convert.ToHexString(commandData)}. Sending response with value {updatedCommand.ToString()}, raw response: {Convert.ToHexString(updatedCommand.ToByteArray())}");
                            var result = await WriteBytes(updatedCommand.ToByteArray());
                            if (result != WriteResult.Success)
                            {
                                var errorMsg = $"Successfully received valid read request from UC, but unable to write response: {result}";
                                frontendLogger.Log(errorMsg);
                                throw new Exception(errorMsg);
                            }
                        }
                        else
                        {
                            frontendLogger.Log($"Successfully received write-request for command with id {updatedCommand.Id}, raw request: {Convert.ToHexString(commandData)}, interpreted as Command: {updatedCommand.ToString()}. Writing values to Database.");
                            if (!command.Readonly)
                                this.settings.SaveConfigurableSetting(updatedCommand);

                            this.settings.NotifySettingChanged(updatedCommand);
                        }

                        await this.SendAcknowledgeByteToMicrocontroller(true);
                    }
                    catch (Exception e)
                    {
                        frontendLogger.Log($"Error Processing command with id {command.Id}: {e.Message}. Responding with 0xFE");
                        await this.SendAcknowledgeByteToMicrocontroller(false);
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
        // Note: writeDataSemaphore must NOT be released here. Every WriteBytes call releases
        // it itself (success and error paths), and an extra release would inflate the count
        // so two writers could interleave on the next connection.
        bool wasConnected = this.connection != null;
        this.connection?.Dispose();
        this.connection = null;
        this.ReportedFirmwareVersion = null;
        if (wasConnected)
        {
            ConnectionDiagnostics.Event("CONN", "DISCONNECTED");
            frontendLogger.Log("Connection to Microcontroller closed");
            ConnectionStateChanged?.Invoke(false);
        }
    }

    // Motor positions are streamed the way games stream state: each one is sent the moment it
    // exists and is never waited on or resent, because the next (newer) position replaces a
    // lost one anyway. Waiting for the ack of every position turned each lost WiFi packet into
    // a pause (1s with the generic retry interval, still 60ms+ with a short ack window), which
    // added up to visible hiccups whenever the link dropped several packets in a row.
    // The microcontroller still acks every position; those acks only prove the link is alive.

    // This many positions in a row without receiving anything from the microcontroller count
    // as a dead link (~1.5s at telemetry rate).
    private const int MaxPositionsWithoutReply = 60;
    private int positionsSinceLastReceive = 0;

    // Acks carry no ID, so the ack of a just-sent position could be mistaken for the ack of a
    // reliable command sent right after it. Reliable commands therefore wait until the acks of
    // the last positions have had this long to arrive (positions pause meanwhile).
    private const int PositionAckSettleMs = 80;
    private double lastPositionSentAtMs = double.MinValue;

    public async Task<WriteResult> WriteCommand(Command command)
    {
        var data = command.ToByteArray();
        bool isPositionStream = command.Id == Command.MotorPositionCommandId && !command.IsReadRequest;
        return await WriteBytes(data, isPositionStream);
    }

    private async Task<WriteResult> WriteBytes(byte[] data, bool isPositionStream = false)
    {
        bool acquiredSemaphore = false;
        try
        {
            if (connection == null)
                throw new Exception("Attempted to write data to a closed connection");

            commandCount++;
            bool receivedResponse = false;
            await writeDataSemaphore.WaitAsync();
            acquiredSemaphore = true;

            if (isPositionStream)
            {
                if (++positionsSinceLastReceive > MaxPositionsWithoutReply)
                {
                    positionsSinceLastReceive = 0;
                    throw new Exception($"No response from controller for {MaxPositionsWithoutReply} motor position commands in a row.");
                }

                ConnectionDiagnostics.Event("TX", Convert.ToHexString(data));
                this.connection.Write(data, 0, data.Length);
                lastPositionSentAtMs = ConnectionDiagnostics.NowMs;
                writeDataSemaphore.Release();
                return WriteResult.Success;
            }

            double settleMs = PositionAckSettleMs - (ConnectionDiagnostics.NowMs - lastPositionSentAtMs);
            if (settleMs > 0)
                await Task.Delay((int)settleMs + 1);

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                // Discard an ack that arrived after its command already timed out, so it
                // can't be mistaken for the ack of the datagram we're about to send.
                while (responseReceivedSemaphore.Wait(0)) { }
                waitingForResponse = true;
                double sentAtMs = ConnectionDiagnostics.NowMs;
                ConnectionDiagnostics.Event("TX", $"{Convert.ToHexString(data)} attempt={attempt}");
                this.connection.Write(data, 0, data.Length);
                receivedResponse = await responseReceivedSemaphore.WaitAsync(options.CurrentValue.CommandSendRetryIntervalMs);
                ConnectionDiagnostics.Event(receivedResponse ? "ACK" : "TIMEOUT", $"{Convert.ToHexString(data)} attempt={attempt} rttMs={ConnectionDiagnostics.NowMs - sentAtMs:F1} result={currentWriteResult}");

                if (!receivedResponse)
                {
                    string error = $"Error writing value: No response from controller received (Attempt {attempt}).";
                    frontendLogger.Log(error);
                }
                else if (currentWriteResult != WriteResult.Success)
                {
                    string error = $"Error writing value: Received non-success response from controller (Attempt {attempt}, response: {currentWriteResult}). Retrying in {options.CurrentValue.CommandSendRetryIntervalMs}mS (specified via config.json)";
                    frontendLogger.Log(error);
                    await Task.Delay(options.CurrentValue.CommandSendRetryIntervalMs);
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
            return receivedResponse ? currentWriteResult : WriteResult.Timeout;
        }
        catch (Exception e)
        {
            var message = $"Error writing value: {e.Message}";
            System.Console.WriteLine(message);
            frontendLogger.Log(message);
            if (acquiredSemaphore)
                writeDataSemaphore.Release();

            // A failed transmission means the link to the seat is unreliable. Drop the
            // connection so the auto-connect loop rediscovers and reconnects — no matter
            // what happens, the backend keeps trying to bind to the seat.
            this.Disconnect();
            return WriteResult.WriteError;
        }
    }

    private async Task SendAcknowledgeByteToMicrocontroller(bool isValid)
    {
        await writeDataSemaphore.WaitAsync();
        ConnectionDiagnostics.Event("ACKTX", isValid ? "FF" : "FE");
        this.connection?.Write(new[] { (byte)(isValid ? 0xFF : 0xFE) }, 0, 1);
        writeDataSemaphore.Release();
    }
}
