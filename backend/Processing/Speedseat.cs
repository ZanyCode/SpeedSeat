using System;
using System.IO.Ports;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.SignalR;

public class Speedseat
{
    private double frontTilt;
    private double sideTilt;
    private double frontLeftMotorPosition;
    private double frontRightMotorPosition;
    private double backMotorPosition;

    private ISubject<Guid> publishPositionQueue = Subject.Synchronize(new Subject<Guid>());
    private ISubject<bool> canPublish = Subject.Synchronize(new Subject<bool>());
    private readonly SpeedseatSettings settings;
    private readonly ISerialConnection connection;

    public double FrontLeftMotorPosition { get => frontLeftMotorPosition; set {
        frontLeftMotorPosition = value;
        this.RequestPositionPublish();
    }}

    public double FrontRightMotorPosition { get => frontRightMotorPosition; set {
        frontRightMotorPosition = value;
        this.RequestPositionPublish();

    }}

    public double BackMotorPosition { get => backMotorPosition; set {
        backMotorPosition = value;
        this.RequestPositionPublish();

    }}

    public bool IsConnected { get => this.connection.IsConnected; }

    public Speedseat(SpeedseatSettings settings, ISerialConnection connection)
    {
        var publishIfPossible = publishPositionQueue                                   
                                    .DistinctUntilChanged();

        publishIfPossible.WithLatestFrom(
            settings.FrontLeftMotorIdxObs.CombineLatest(settings.FrontRightMotorIdxObs, settings.BackMotorIdxObs)
        ).Subscribe(x => {
            var (frontLeftIdx, frontRightIdx, backIdx) = x.Second;
            this.UpdatePosition(frontLeftIdx, frontRightIdx, backIdx);
        });        

        this.settings = settings;
        this.connection = connection;
    }

    // Tilt Range is 0 to 1
    public void SetTilt(double frontTilt, double sideTilt)
    {        
        frontLeftMotorPosition = sideTilt;
        frontRightMotorPosition = 1 - sideTilt;
        backMotorPosition = 1 - frontTilt;
        this.RequestPositionPublish();
    }

    public bool Connect(string port, int baudrate)
    {
        this.connection.Connect(port, baudrate);
        return true;      
    }

    public void Disconnect() {
        this.connection.Disconnect();        
    }

    private void UpdatePosition(int frontLeftIdx, int frontRightIdx, int backIdx) {
        var (frontLeftMsb, frontLeftLsb) = ScaleToUshortRange(frontLeftMotorPosition);
        var (frontRightMsb, frontRightLsb) = ScaleToUshortRange(frontRightMotorPosition);
        var (backMsb, backLsb) = ScaleToUshortRange(backMotorPosition);

        var bytes = new byte[7];
        bytes[0] = 0;
        bytes[frontLeftIdx * 2 + 1] = frontLeftMsb;
        bytes[frontLeftIdx * 2 + 2] = frontLeftLsb;
        bytes[frontRightIdx * 2 + 1] = frontRightMsb;
        bytes[frontRightIdx * 2 + 2] = frontRightLsb;
        bytes[backIdx * 2 + 1] = backMsb;
        bytes[backIdx * 2 + 2] = backLsb;
        System.Console.WriteLine($"FrontLeft(Idx{frontLeftIdx}): {frontLeftMotorPosition*100}%\n" +
                                    $"FrontRight(Idx{frontRightIdx}): {frontRightMotorPosition*100}%\n" + 
                                    $"Back(Idx{backIdx}): {backMotorPosition*100}%\n" +
                                    $"Binary Message: {Convert.ToHexString(bytes, 0, 7)}\n");
                                    
        this.connection.Write(bytes);
    }

    private void RequestPositionPublish() 
    {
        this.publishPositionQueue.OnNext(Guid.NewGuid());
    }

    private (byte msb, byte lsb) ScaleToUshortRange(double percentage) {
        ushort value = (ushort)Math.Clamp(percentage * ushort.MaxValue, 0, ushort.MaxValue);
        var bytes = BitConverter.GetBytes(value);
        return (bytes[1], bytes[0]);
    }
}

public interface ISerialConnection
{
    void Connect(string port, int baudrate);
    void Disconnect();

    bool Write(byte[] payload);

    bool IsConnected { get; }

}

public class SerialConnection : ISerialConnection
{
    private Dictionary<byte, string> messages = new Dictionary<byte, string> {
        {1, "X-Achse Min Position überschritten"},
        {2, "X-Achse Max Position überschritten"},
        {3, "X-Achse Endstop ausgelöst"},
        {4, "X-Achse Störung Treiber"},

        {11, "Y-Achse Min Position überschritten"},
        {12, "Y-Achse Max Position überschritten"},
        {13, "Y-Achse Endstop ausgelöst"},
        {14, "Y-Achse Störung Treiber"},

        {21, "Z-Achse Min Position überschritten"},
        {22, "Z-Achse Max Position überschritten"},
        {23, "Z-Achse Endstop ausgelöst"},
        {24, "Z-Achse Störung Treiber"},
    };

    private SerialPort? serialPort;

    private SemaphoreSlim semaphore = new SemaphoreSlim(1);
    private readonly InfoHub infoHub;
    private readonly IHubContext<InfoHub> hubContext;

    public bool IsConnected => this.serialPort != null;

    public SerialConnection(IHubContext<InfoHub> hubContext)
    {
        this.hubContext = hubContext;
    }

    public void Connect(string port, int baudrate)
    {
        // Create a new SerialPort object with default settings.
        serialPort = new SerialPort(port);

        // Allow the user to set the appropriate properties.
        serialPort.BaudRate = baudrate;
        // _serialPort.Parity = SetPortParity(_serialPort.Parity);
        // _serialPort.DataBits = SetPortDataBits(_serialPort.DataBits);
        // _serialPort.StopBits = SetPortStopBits(_serialPort.StopBits);
        // _serialPort.Handshake = SetPortHandshake(_serialPort.Handshake);

        // Set the read/write timeouts
        serialPort.ReadTimeout = 500;
        serialPort.WriteTimeout = 500;
        serialPort.DataReceived += (sender, args) => {
            byte[] data = new byte[serialPort.BytesToRead];
            serialPort.Read(data, 0, data.Length);
            foreach(var b in data) {
                if(b == 255) {
                    // this.hubContext.Clients.All.SendAsync("log", "jooooooooo");
                    this.semaphore.Release();
                }  
                else {
                    this.hubContext.Clients.All.SendAsync("log", GetMessageFromCode(b));
                    System.Console.WriteLine($"Serial received: {b}");
                }               
            }
              
        };

        serialPort.ErrorReceived += (sender, args) => {
            this.Disconnect();
        };

        serialPort.Open();        
    }

    public void Disconnect()
    {
        this.serialPort?.Dispose();
        this.serialPort = null;
    }

    public bool Write(byte[] payload)
    {
        if(serialPort == null)
            return false;

        try 
        {
            serialPort.Write(payload, 0, payload.Length); 
            if(!this.semaphore.Wait(2000))
            {
                throw new Exception("Did not receive appropriate confirmation byte from controller within 2 seconds.");
            }

            return true;
        }  
        catch
        {
            this.Disconnect();
            return false;  
        }
    }

    private string GetMessageFromCode(byte code)
    {
        if(!this.messages.ContainsKey(code))
        {
            return $"Unknown message with code '{code}'";
        }
        else 
            return $"{messages[code]} ({code})";
    }
}