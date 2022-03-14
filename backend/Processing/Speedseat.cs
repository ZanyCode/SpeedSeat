using System;
using System.IO.Ports;
using System.Reactive.Linq;
using System.Reactive.Subjects;

public class Speedseat
{
    private double frontTilt;
    private double sideTilt;
    private double frontLeftMotorPosition;
    private double frontRightMotorPosition;
    private double backMotorPosition;
    private SerialPort serialPort;

    private ISubject<Guid> publishPositionQueue = Subject.Synchronize(new Subject<Guid>());
    private ISubject<bool> canPublish = Subject.Synchronize(new Subject<bool>());
    private readonly SpeedseatSettings settings;

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

    public bool IsConnected { get; set; }

    public Speedseat(SpeedseatSettings settings)
    {
        var publishIfPossible = publishPositionQueue
                                    .CombineLatest(canPublish.StartWith(true))
                                    .Where(x => x.Second)
                                    .Select(x => x.First)
                                    .DistinctUntilChanged();

        publishIfPossible.WithLatestFrom(
            settings.FrontLeftMotorIdxObs.CombineLatest(settings.FrontRightMotorIdxObs, settings.BackMotorIdxObs)
        ).Subscribe(x => {
            var (frontLeftIdx, frontRightIdx, backIdx) = x.Second;
            this.UpdatePosition(frontLeftIdx, frontRightIdx, backIdx);
        });
        
        this.settings = settings;
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
        if(IsConnected)
            return true;

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
                    this.EnablePublishing();
                }
                else {
                    System.Console.WriteLine($"Serial received: {b}");
                }
            }
              
        };
        serialPort.Open();        
        IsConnected = true;   
        return true;   
    }

    public void Disconnect() {
        if(!this.IsConnected)
            return;
        
        this.serialPort.Close();
        this.IsConnected = false;
    }

    private void UpdatePosition(int frontLeftIdx, int frontRightIdx, int backIdx) {
        if(this.IsConnected) {
            this.DisablePublishing();
            try {
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
                serialPort.Write(bytes, 0, 7);           
            }
            catch {
                this.Disconnect();
            }
         
        }
    }

    private void RequestPositionPublish() 
    {
        this.publishPositionQueue.OnNext(Guid.NewGuid());
    }

    private void DisablePublishing()
    {
        this.canPublish.OnNext(false);
    }

    private void EnablePublishing() 
    {
        this.canPublish.OnNext(true);
    }

    private (byte msb, byte lsb) ScaleToUshortRange(double percentage) {
        ushort value = (ushort)Math.Clamp(percentage * ushort.MaxValue, 0, ushort.MaxValue);
        var bytes = BitConverter.GetBytes(value);
        return (bytes[1], bytes[0]);
    }
}