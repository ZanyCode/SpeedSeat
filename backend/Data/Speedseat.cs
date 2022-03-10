using System;
using System.IO.Ports;

public class Speedseat
{
    private double frontTilt;
    private double sideTilt;
    private double frontLeftMotorPosition;
    private double frontRightMotorPosition;
    private double backMotorPosition;
    private SerialPort serialPort;

    public double FrontLeftMotorPosition { get => frontLeftMotorPosition; set {
        frontLeftMotorPosition = value;
        if(IsConnected) {
            string msg = $"0 {(int)(value * 180)}";
            serialPort.WriteLine(msg);
        }      
    }}

    public double FrontRightMotorPosition { get => frontRightMotorPosition; set {
        frontRightMotorPosition = value;
        if(IsConnected) {
            string msg = $"1 {(int)(value * 180)}";
            serialPort.WriteLine(msg);
        }      
    }}

    public double BackMotorPosition { get => backMotorPosition; set {
        backMotorPosition = value;
        if(IsConnected) {
            string msg = $"2 {(int)(value * 180)}";
            serialPort.WriteLine(msg);
        }      
    }}

    public bool IsConnected { get; set; }

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
}