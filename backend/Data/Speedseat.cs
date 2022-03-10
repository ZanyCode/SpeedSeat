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

    public Speedseat()
    {
        this.publishPositionQueue.Sample(TimeSpan.FromMilliseconds(1)).Subscribe(x => this.UpdatePosition());
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
                    System.Console.WriteLine($"{b}");
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

    private void UpdatePosition() {
        if(this.IsConnected && this.canSend) {
            this.canSend = false;
            try {
                var bytes = new byte[] {0, 0, 0, 0, 0, 0, 0};
                serialPort.Write(bytes, 0, 7);
                // var response = serialPort.ReadExisting();
                // System.Console.WriteLine(response);
                // string msg = $"0 {(int)(this.FrontLeftMotorPosition * 180)}";
                // serialPort.WriteLine(msg);
                // msg = $"1 {(int)(this.FrontRightMotorPosition * 180)}";
                // serialPort.WriteLine(msg);
                // msg = $"2 {(int)(this.BackMotorPosition * 180)}";
                // serialPort.WriteLine(msg);                
            }
            catch {
                this.Disconnect();
            }
         
        }
    }
}