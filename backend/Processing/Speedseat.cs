using System;
using System.IO.Ports;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.SignalR;

public class Speedseat
{
    private double frontLeftMotorPosition;
    private double frontRightMotorPosition;
    private double backMotorPosition;

    private readonly SpeedseatSettings settings;
    private readonly ISerialConnection connection;
    private readonly IHubContext<InfoHub> hubContext;

    private (double frontTilt, double sideTilt) tilt;
    public (double frontTilt, double sideTilt) Tilt { get => tilt; set {
        tilt = value;
        this.UpdateTilt();
    }}

    public double FrontLeftMotorPosition { get => frontLeftMotorPosition; set {
        frontLeftMotorPosition = value;
        this.UpdatePosition();
    }}

    public double FrontRightMotorPosition { get => frontRightMotorPosition; set {
        frontRightMotorPosition = value;
        this.UpdatePosition();

    }}

    public double BackMotorPosition { get => backMotorPosition; set {
        backMotorPosition = value;
        this.UpdatePosition();
    }}

    public bool IsConnected { get => this.connection.IsConnected; }

    public Speedseat(SpeedseatSettings settings, ISerialConnection connection, IHubContext<InfoHub> hubContext)
    {
        this.settings = settings;       
        this.connection = connection;
        this.hubContext = hubContext;

        // Update whenever the one of the motor mapping changes
        settings.FrontLeftMotorIdxObs.CombineLatest(
            settings.FrontRightMotorIdxObs,
            settings.BackMotorIdxObs
            ).Subscribe(_ => UpdatePosition());      

        // Update whenever the tilt priority changes
        this.settings.FrontTiltPriorityObs.Subscribe(x => this.UpdateTilt());

        // Update whenever the back motor response curve changes
        this.settings.BackMotorResponseCurveObs.Subscribe(x => this.UpdatePosition());
    }

    // Tilt Range is -1  to 1
    public void SetTilt(double frontTilt, double sideTilt) 
    {
        this.Tilt = (frontTilt, sideTilt);
    }

    private void UpdateTilt()
    {        
        var availableAbsoluteSideTilt = 1 - Math.Abs(this.Tilt.frontTilt);
        var actualAbsoluteSideTilt = Math.Abs(this.Tilt.sideTilt);
        var necessarySideTiltReduction = Math.Clamp(actualAbsoluteSideTilt - availableAbsoluteSideTilt, 0, 1) * settings.FrontTiltPriority;
        var correctedSideTilt = this.Tilt.sideTilt - necessarySideTiltReduction * (this.Tilt.sideTilt < 0? -1 : 1);

        backMotorPosition = 1 - (this.Tilt.frontTilt + 1) / 2.0;        
        frontLeftMotorPosition = (correctedSideTilt + 1) / 2.0;
        frontRightMotorPosition = 1 - (correctedSideTilt + 1) / 2.0;
        
        var desiredAdditionalBackMotorTiltAbs = Math.Abs(this.Tilt.frontTilt) * 0.5;
        var maxAdditionalBackMotorTiltAbs = (1 - Math.Abs(correctedSideTilt)) * 0.5; 
        var additionalBackMotorTiltAbs = Math.Min(desiredAdditionalBackMotorTiltAbs, maxAdditionalBackMotorTiltAbs);
        var additionalBackMotorTilt = additionalBackMotorTiltAbs * (this.Tilt.frontTilt < 0? -1 : 1);
        frontLeftMotorPosition += additionalBackMotorTilt;
        frontRightMotorPosition += additionalBackMotorTilt;

        this.UpdatePosition();
    }

    public bool Connect(string port, int baudrate)
    {
        this.connection.Connect(port, baudrate);
        return true;      
    }

    public void Disconnect() {
        this.connection.Disconnect();        
    }

    private void UpdatePosition()
    {
        if(this.connection.IsConnected)
         {
            var transformedBackMotorPosition = this.ApplyCurveToMotorPosition(backMotorPosition, settings.BackMotorResponseCurve);
            var transformedFrontLeftMotorPosition = this.ApplyCurveToMotorPosition(frontLeftMotorPosition, settings.SideMotorResponseCurve);
            var transformedFrontRightMotorPosition = this.ApplyCurveToMotorPosition(frontRightMotorPosition, settings.SideMotorResponseCurve);
            var bytes = GetMotorPositionsAsByteArray(transformedFrontLeftMotorPosition, transformedFrontRightMotorPosition, transformedBackMotorPosition);    
                                       
            if(this.connection.Write(bytes))
            {
                System.Console.WriteLine($"FrontLeft(Idx{settings.FrontLeftMotorIdx}): {frontLeftMotorPosition*100}%\n" +
                    $"FrontRight(Idx{settings.FrontRightMotorIdx}): {frontRightMotorPosition*100}%\n" + 
                    $"Back(Idx{settings.BackMotorIdx}): {backMotorPosition*100}%\n" +
                    $"Binary Message: {Convert.ToHexString(bytes, 0, 7)}\n");
            }
            else
            {
                string error = "Value wasn't written since no response from controller was received. Connection closed, please reconnect manually.";
                System.Console.WriteLine(error);
                this.hubContext.Clients.All.SendAsync("log", error);
            }      
        }      
    }

    private byte[] GetMotorPositionsAsByteArray(double frontLeftMotorPosition, double frontRightMotorPosition, double backMotorPosition) {
        var (frontLeftMsb, frontLeftLsb) = ScaleToUshortRange(frontLeftMotorPosition);
        var (frontRightMsb, frontRightLsb) = ScaleToUshortRange(frontRightMotorPosition);
        var (backMsb, backLsb) = ScaleToUshortRange(backMotorPosition);

        var bytes = new byte[7];
        bytes[0] = 0;
        bytes[settings.FrontLeftMotorIdx * 2 + 1] = frontLeftMsb;
        bytes[settings.FrontLeftMotorIdx * 2 + 2] = frontLeftLsb;
        bytes[settings.FrontRightMotorIdx * 2 + 1] = frontRightMsb;
        bytes[settings.FrontRightMotorIdx * 2 + 2] = frontRightLsb;
        bytes[settings.BackMotorIdx * 2 + 1] = backMsb;
        bytes[settings.BackMotorIdx * 2 + 2] = backLsb;

        return bytes;
    }

    private double ApplyCurveToMotorPosition(double motorPosition, IEnumerable<ResponseCurvePoint> curve) {
        var (a, b) = curve.Zip(curve.Skip(1), (a, b) => (a, b)).FirstOrDefault(x => x.Item2.Input >= motorPosition);
        var range = b.Input - a.Input;
        var position = motorPosition - a.Input;       
        var factor = position / range;
        var output = a.Output + (b.Output - a.Output) * factor;
        return output;
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