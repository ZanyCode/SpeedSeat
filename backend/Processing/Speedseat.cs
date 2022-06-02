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
    private readonly IHubContext<InfoHub> hubContext;

    private readonly ISubject<(double frontTilt, double sideTilt)> tiltSubject = new Subject<(double frontTilt, double sideTilt)>();

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

    public Speedseat(SpeedseatSettings settings, ISerialConnection connection, IHubContext<InfoHub> hubContext)
    {
        this.settings = settings;       
        this.connection = connection;
        this.hubContext = hubContext;

        var publishIfPossible = publishPositionQueue                                   
                                    .DistinctUntilChanged();

        publishIfPossible.WithLatestFrom(
            settings.FrontLeftMotorIdxObs.CombineLatest(settings.FrontRightMotorIdxObs, settings.BackMotorIdxObs)
        ).Subscribe(x => {
            var (frontLeftIdx, frontRightIdx, backIdx) = x.Second;
            this.UpdatePosition(frontLeftIdx, frontRightIdx, backIdx);
        });      

        this.settings.FrontTiltPriorityObs.CombineLatest(this.tiltSubject).Subscribe(x => {
            var (priority, (frontTilt, sideTilt)) = x;
            this.UpdateTilt(priority, frontTilt, sideTilt);
        });          
    }

    // Tilt Range is -1  to 1
    public void SetTilt(double frontTilt, double sideTilt) 
    {
        this.tiltSubject.OnNext((frontTilt, sideTilt));
    }

    private void UpdateTilt(double frontTiltPriority, double frontTilt, double sideTilt)
    {        
        var availableAbsoluteSideTilt = 1 - Math.Abs(frontTilt);
        var actualAbsoluteSideTilt = Math.Abs(sideTilt);
        var necessarySideTiltReduction = Math.Clamp(actualAbsoluteSideTilt - availableAbsoluteSideTilt, 0, 1) * frontTiltPriority;
        var correctedSideTilt = sideTilt - necessarySideTiltReduction * (sideTilt < 0? -1 : 1);

        backMotorPosition = 1 - (frontTilt + 1) / 2.0;        
        frontLeftMotorPosition = (correctedSideTilt + 1) / 2.0;
        frontRightMotorPosition = 1 - (correctedSideTilt + 1) / 2.0;
        
        var desiredAdditionalBackMotorTiltAbs = Math.Abs(frontTilt) * 0.5;
        var maxAdditionalBackMotorTiltAbs = (1 - Math.Abs(correctedSideTilt)) * 0.5; 
        var additionalBackMotorTiltAbs = Math.Min(desiredAdditionalBackMotorTiltAbs, maxAdditionalBackMotorTiltAbs);
        var additionalBackMotorTilt = additionalBackMotorTiltAbs * (frontTilt < 0? -1 : 1);
        frontLeftMotorPosition += additionalBackMotorTilt;
        frontRightMotorPosition += additionalBackMotorTilt;

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
                                    
        if(this.connection.Write(bytes))
        {
            System.Console.WriteLine($"FrontLeft(Idx{frontLeftIdx}): {frontLeftMotorPosition*100}%\n" +
                $"FrontRight(Idx{frontRightIdx}): {frontRightMotorPosition*100}%\n" + 
                $"Back(Idx{backIdx}): {backMotorPosition*100}%\n" +
                $"Binary Message: {Convert.ToHexString(bytes, 0, 7)}\n");
        }
        else
        {
            string error = "Value wasn't written since no response from controller was received. Connection closed, please reconnect manually.";
            System.Console.WriteLine(error);
            this.hubContext.Clients.All.SendAsync("log", error);
        }
      
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
        this.hubContext.Clients.All.SendAsync("log", $"Successfully connected to port {port} with baud rate {baudrate}");    
    }

    public void Disconnect()
    {        
        this.semaphore.Release();
        this.serialPort?.Dispose();
        this.serialPort = null;
        this.hubContext.Clients.All.SendAsync("log", $"Connection to Serial Port closed");    
    }

    public bool Write(byte[] payload)
    {
        if(serialPort == null)
            return false;

        try 
        {
            System.Console.WriteLine(this.semaphore.CurrentCount);
            if(!this.semaphore.Wait(2000))
            {
                throw new Exception("Did not receive appropriate confirmation byte from controller within 2 seconds.");
            }
            serialPort.Write(payload, 0, payload.Length);            
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