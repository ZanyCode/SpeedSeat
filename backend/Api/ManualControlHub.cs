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
        System.Console.WriteLine($"Updating FrontLeftMotor to position {position}");
        seat.FrontLeftMotorPosition = position;    
    }

    public void SetFrontRightMotorPosition(double position)
    {
        System.Console.WriteLine($"Updating FrontRightMotor to position {position}");
        seat.FrontRightMotorPosition = position;    
    }

    public void SetBackMotorPosition(double position)
    {
        System.Console.WriteLine($"Updating BackMotor to position {position}");
        seat.BackMotorPosition = position;    
    }

    public void SetTilt(double frontTilt, double sideTilt) {
        System.Console.WriteLine($"Updating tilt to front: {frontTilt}, side: {sideTilt}");
        seat.SetTilt(frontTilt, sideTilt);
    }

    public Speedseat GetCurrentState() {
        return seat;
    }
}