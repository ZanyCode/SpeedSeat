using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;
using System.IO.Ports;

public class ConnectionHub : Hub
{
    private readonly Speedseat seat;
    private readonly SpeedseatSettings settings;
    private readonly CommandService commandService;
    private readonly IHubContext<ConnectionHub> context;

    public ConnectionHub(Speedseat seat, SpeedseatSettings settings, CommandService commandService, IHubContext<ConnectionHub> context)
    {
        this.seat = seat;
        this.settings = settings;
        this.commandService = commandService;
        this.context = context;
    }

    public string[] GetPorts() 
    {
        return SerialPort.GetPortNames();
    }

    public bool GetIsConnected() {
        return commandService.IsConnected;
    }

    public bool Connect(string port, int baudrate)
    {
        settings.BaudRate = baudrate;
        return commandService.Connect(port, baudrate);       
    }

    public int GetBaudRate()
    {
        return settings.BaudRate;
    }

    public void Disconnect()
    {
        commandService.Disconnect();       
    }
}
