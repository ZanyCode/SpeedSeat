using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;

public class ManualControlHub : Hub
{
    private readonly SpeedseatSettings settings;
    private readonly IHubContext<ManualControlHub> context;

    public ManualControlHub(SpeedseatSettings settings, IHubContext<ManualControlHub> context)
    {
        this.settings = settings;
        this.context = context;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        var id = this.Context.ConnectionId;
        settings.Motor0Position.Subscribe(x => {
            context.Clients.Client(id).SendAsync("motor0Position", x);
        });

        settings.Motor1Position.Subscribe(x => {
            context.Clients.Client(id).SendAsync("motor1Position", x);
        });

        settings.Motor2Position.Subscribe(x => {
            context.Clients.Client(id).SendAsync("motor2Position", x);
        });

        settings.FrontTilt.Subscribe(x => {
            context.Clients.Client(id).SendAsync("frontTilt", x);
        });

        settings.SideTilt.Subscribe(x => {
            context.Clients.Client(id).SendAsync("sideTilt", x);
        });

        settings.FrontTilt.CombineLatest(settings.SideTilt).Subscribe(x => {
            var (frontTilt, sideTilt) = x;
            var frontMotorPositions = frontTilt;
            var backMotorPosition = 1 - frontTilt;
            settings.SetMotor0Position(frontMotorPositions);
            settings.SetMotor1Position(frontMotorPositions);
            settings.SetMotor2Position(backMotorPosition);
        });
    }

    public async Task UpdateMotorValue(int motorIdx, double position)
    {
        System.Console.WriteLine($"Updating motor with idx {motorIdx} to position {position}");
        var setPosition = new [] { settings.SetMotor0Position, settings.SetMotor1Position, settings.SetMotor2Position};
        setPosition[motorIdx](position);        
    }

    public async Task UpdateFrontTilt(double position) {
        System.Console.WriteLine($"Updating front tilt to {position}");
        var frontMotorPositions = position;
        var backMotorPosition = 1 - position;
        settings.SetFrontTilt(position);     
    }

     public async Task UpdateSideTilt(double position) {
        System.Console.WriteLine($"Updating side tilt to {position}");     
        settings.SetSideTilt(position);
    }
}
