using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;

public class SeatSettingsHub : Hub
{
    private readonly IHubContext<SeatSettingsHub> context;

    public SeatSettingsHub(Speedseat seat, IHubContext<SeatSettingsHub> context)
    {
        this.context = context;
    }   

    public void SendCommandToSeat(Command command)
    {    
    }
}