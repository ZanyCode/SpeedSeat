using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;

public class ManualControlHub : Hub
{
    private readonly Speedseat seat;
    private readonly IHubContext<ManualControlHub> context;

    public ManualControlHub(Speedseat seat, IHubContext<ManualControlHub> context)
    {
        this.seat = seat;
        this.context = context;
    }

    public void SetFrontLeftMotorPosition(double position)
    {
        seat.FrontLeftMotorPosition = position;    
    }

    public void SetFrontRightMotorPosition(double position)
    {
        seat.FrontRightMotorPosition = position;    
    }

    public void SetBackMotorPosition(double position)
    {
        seat.BackMotorPosition = position;    
    }

    public void SetTilt(double frontTilt, double sideTilt) {
        seat.SetTilt(frontTilt, sideTilt);
    }

    public Speedseat GetCurrentState() {
        return seat;
    }
}