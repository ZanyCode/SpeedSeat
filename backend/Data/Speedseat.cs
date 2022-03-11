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

    private bool canSend = true;

    private ISubject<bool> publishPositionQueue = Subject.Synchronize(new Subject<bool>());
    private readonly SpeedseatSettings settings;

    public double FrontLeftMotorPosition { get => frontLeftMotorPosition; set {
        frontLeftMotorPosition = value;
        this.publishPositionQueue.OnNext(true);
    }}

    public double FrontRightMotorPosition { get => frontRightMotorPosition; set {
        frontRightMotorPosition = value;
        this.publishPositionQueue.OnNext(true);    
    }}

    public double BackMotorPosition { get => backMotorPosition; set {
        backMotorPosition = value;
        this.publishPositionQueue.OnNext(true);    
    }}

    public bool IsConnected { get; set; }

    public Speedseat(SpeedseatSettings settings)
    {
        this.publishPositionQueue.WithLatestFrom(
            settings.FrontLeftMotorIdxObs.CombineLatest(settings.FrontRightMotorIdxObs, settings.BackMotorIdxObs)
        ).Subscribe(x => {
            var (frontLeftIdx, frontRightIdx, backIdx) = x.Second;
            this.UpdatePosition(frontLeftIdx, frontRightIdx, backIdx);
        });
        this.settings = settings;
    }

    public void SetTilt(double frontTilt, double sideTilt)
    {        
        FrontLeftMotorPosition = FrontRightMotorPosition = frontTilt;
        BackMotorPosition = 1 - frontTilt;
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
                    canSend = true;
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
        if(this.IsConnected && this.canSend) {
            this.canSend = false;
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
                serialPort.Write(bytes, 0, 7);           
                System.Console.WriteLine($"FrontLeft(Idx{frontLeftIdx}): {frontLeftMotorPosition}%\n" +
                                         $"FrontRight(Idx{frontRightIdx}): {frontRightMotorPosition}%\n" + 
                                         $"Back(Idx{backIdx}): {backMotorPosition}%\n" +
                                         $"Binary Message: {Convert.ToHexString(bytes, 0, 7)}\n");
            }
            catch {
                this.Disconnect();
            }
         
        }
    }

    private (byte msb, byte lsb) ScaleToUshortRange(double percentage) {
        ushort value = (ushort)Math.Clamp(percentage * ushort.MaxValue, 0, ushort.MaxValue);
        var bytes = BitConverter.GetBytes(value);
        return (bytes[1], bytes[0]);
    }
}