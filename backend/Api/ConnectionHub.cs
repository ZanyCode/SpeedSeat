using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;
using System.IO.Ports;

public class ConnectionHub : Hub
{
    private readonly Speedseat seat;
    private readonly IHubContext<ConnectionHub> context;

    public ConnectionHub(Speedseat seat, IHubContext<ConnectionHub> context)
    {
        this.seat = seat;
        this.context = context;
    }

    public string[] GetPorts() 
    {
        return SerialPort.GetPortNames();
    }

    public bool GetIsConnected() {
        return seat.IsConnected;
    }

    public bool Connect(string port, int baudrate)
    {
        return seat.Connect(port, baudrate);       
    }

    public void Disconnect()
    {
        seat.Disconnect();       
    }
}
