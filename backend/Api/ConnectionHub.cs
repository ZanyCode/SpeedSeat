using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;
using System.IO.Ports;

public class ConnectionHub : Hub
{
    private readonly SpeedseatSettings settings;
    private readonly IHubContext<ConnectionHub> context;

    public ConnectionHub(SpeedseatSettings settings, IHubContext<ConnectionHub> context)
    {
        this.settings = settings;
        this.context = context;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        var id = this.Context.ConnectionId;

        Task.Factory.StartNew(async x => {
            string[] ports = new string[] {};
            context.Clients.Client(id).SendAsync("serialPorts", ports);

            while(true) {
                var currentPorts = SerialPort.GetPortNames();
                if(ports.Length != currentPorts.Length || currentPorts.Any(p => !ports.Contains(p))) {
                    context.Clients.Client(id).SendAsync("serialPorts", currentPorts);
                    ports = currentPorts;
                }
                await Task.Delay(3000);
            }
        }, TaskCreationOptions.LongRunning);       
    }

    public void Connect(string port) {
         // Create a new SerialPort object with default settings.
        var _serialPort = new SerialPort("COM3");

        // Allow the user to set the appropriate properties.
        _serialPort.BaudRate = 9600;
        // _serialPort.Parity = SetPortParity(_serialPort.Parity);
        // _serialPort.DataBits = SetPortDataBits(_serialPort.DataBits);
        // _serialPort.StopBits = SetPortStopBits(_serialPort.StopBits);
        // _serialPort.Handshake = SetPortHandshake(_serialPort.Handshake);

        // Set the read/write timeouts
        _serialPort.ReadTimeout = 10;
        _serialPort.WriteTimeout = 10;

        _serialPort.Open();

        settings.Motor0Position.Subscribe(pos => {            
            string msg = $"0 {(int)(pos * 180)}";
             _serialPort.WriteLine(msg);
        });   

        settings.Motor1Position.Subscribe(pos => {            
            string msg = $"1 {(int)(pos * 180)}";
             _serialPort.WriteLine(msg);
        });   

        settings.Motor2Position.Subscribe(pos => {            
            string msg = $"2 {(int)(pos * 180)}";
             _serialPort.WriteLine(msg);
        });     
    }
}
