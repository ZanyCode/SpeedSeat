using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;
using System.IO.Ports;

public class ConnectionHub : Hub
{
    private readonly Speedseat seat;
    private readonly ISpeedseatSettings settings;
    private readonly CommandService commandService;
    private readonly IHubContext<ConnectionHub> context;

    public ConnectionHub(Speedseat seat, ISpeedseatSettings settings, CommandService commandService, IHubContext<ConnectionHub> context)
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

    public bool GetIsConnected()
    {
        return commandService.IsConnected;
    }

    public async Task<bool> Connect(string port, int baudrate)
    {
        settings.BaudRate = baudrate;
        return await commandService.Connect(port, baudrate);
    }

    public async Task DeleteEEPROM(string port, int baudrate)
    {
        this.commandService.DeleteEEPROM(port, baudrate);
    }

    public int GetBaudRate()
    {
        return settings.BaudRate;
    }

    public void CancelConnectionProcess()
    {
        commandService.CancelConnectionProcess();
    }

    public void Disconnect()
    {
        commandService.Disconnect();
    }

    public void FakeConnectionConfirmation()
    {
        this.commandService.FakeWriteRequest(new Command(Command.ConnectionInitiatedCommandId, null, null, null, false, false));
    }
}
