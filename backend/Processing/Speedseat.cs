using System;
using System.IO.Ports;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

public class Speedseat
{
    private double frontLeftMotorPosition;
    private double frontRightMotorPosition;
    private double backMotorPosition;

    private readonly ISpeedseatSettings settings;
    private readonly CommandService commandService;
    private readonly OutdatedDataDiscardQueue<Command> actionQueue;
    private readonly IHubContext<InfoHub> hubContext;
    private readonly IOptionsMonitor<Config> options;

    public bool IsConnected => this.commandService.IsConnected;

    private (double frontTilt, double sideTilt) tilt;
    public (double frontTilt, double sideTilt) Tilt { get => tilt; set {
        tilt = value;
        this.UpdateTilt();
    }}

    public double FrontLeftMotorPosition { get => frontLeftMotorPosition; set {
        frontLeftMotorPosition = value;
        this.UpdatePosition();
    }}

    public double FrontRightMotorPosition { get => frontRightMotorPosition; set {
        frontRightMotorPosition = value;
        this.UpdatePosition();

    }}

    public double BackMotorPosition { get => backMotorPosition; set {
        backMotorPosition = value;
        this.UpdatePosition();
    }}

    public Speedseat(ISpeedseatSettings settings, CommandService commandService, OutdatedDataDiscardQueue<Command> actionQueue, IHubContext<InfoHub> hubContext)
    {
        this.settings = settings;
        this.commandService = commandService;
        this.actionQueue = actionQueue;
        this.hubContext = hubContext;
        this.options = options;

        // Update whenever the one of the motor mapping changes
        settings.FrontLeftMotorIdxObs.CombineLatest(
            settings.FrontRightMotorIdxObs,
            settings.BackMotorIdxObs
            ).Subscribe(_ => UpdatePosition());      

        // Update whenever the tilt priority changes
        this.settings.FrontTiltPriorityObs.Subscribe(x => this.UpdateTilt());

        // Update whenever the back motor response curve changes
        this.settings.BackMotorResponseCurveObs.Subscribe(x => this.UpdatePosition());
    }

    // Tilt Range is -1  to 1
    public void SetTilt(double frontTilt, double sideTilt) 
    {
        this.Tilt = (frontTilt, sideTilt);
    }

    private async Task UpdateTilt()
    {        
        var availableAbsoluteSideTilt = 1 - Math.Abs(this.Tilt.frontTilt);
        var actualAbsoluteSideTilt = Math.Abs(this.Tilt.sideTilt);
        var necessarySideTiltReduction = Math.Clamp(actualAbsoluteSideTilt - availableAbsoluteSideTilt, 0, 1) * settings.FrontTiltPriority;
        var correctedSideTilt = this.Tilt.sideTilt - necessarySideTiltReduction * (this.Tilt.sideTilt < 0? -1 : 1);

        backMotorPosition = 1 - (this.Tilt.frontTilt + 1) / 2.0;        
        frontLeftMotorPosition = (correctedSideTilt + 1) / 2.0;
        frontRightMotorPosition = 1 - (correctedSideTilt + 1) / 2.0;
        
        var desiredAdditionalBackMotorTiltAbs = Math.Abs(this.Tilt.frontTilt) * 0.5;
        var maxAdditionalBackMotorTiltAbs = (1 - Math.Abs(correctedSideTilt)) * 0.5; 
        var additionalBackMotorTiltAbs = Math.Min(desiredAdditionalBackMotorTiltAbs, maxAdditionalBackMotorTiltAbs);
        var additionalBackMotorTilt = additionalBackMotorTiltAbs * (this.Tilt.frontTilt < 0? -1 : 1);
        frontLeftMotorPosition += additionalBackMotorTilt;
        frontRightMotorPosition += additionalBackMotorTilt;

        await this.UpdatePosition();
    }

    private async Task UpdatePosition()
    {
        if(this.commandService.IsConnected)
        {
            var transformedBackMotorPosition = this.ApplyCurveToMotorPosition(backMotorPosition, settings.BackMotorResponseCurve);
            var transformedFrontLeftMotorPosition = this.ApplyCurveToMotorPosition(frontLeftMotorPosition, settings.SideMotorResponseCurve);
            var transformedFrontRightMotorPosition = this.ApplyCurveToMotorPosition(frontRightMotorPosition, settings.SideMotorResponseCurve);
            
            var positions = new CommandValue[3];
            positions[settings.FrontLeftMotorIdx] = new CommandValue(ValueType.Numeric, transformedFrontLeftMotorPosition);
            positions[settings.FrontRightMotorIdx] = new CommandValue(ValueType.Numeric, transformedFrontRightMotorPosition);
            positions[settings.BackMotorIdx] = new CommandValue(ValueType.Numeric, transformedBackMotorPosition);
            var command = new Command(0, positions[0], positions[1], positions[2], false, false);

            // We actually don't want to await this call here, at this point we just want to "fire and forget"
            Task.Factory.StartNew(() => this.actionQueue.QueueDatapoint(command, x => this.commandService.WriteCommand(x)));  
        }
    }

    private double ApplyCurveToMotorPosition(double motorPosition, IEnumerable<ResponseCurvePoint> curve) {
        var (a, b) = curve.Zip(curve.Skip(1), (a, b) => (a, b)).FirstOrDefault(x => x.Item2.Input >= motorPosition);
        var range = b.Input - a.Input;
        var position = motorPosition - a.Input;       
        var factor = position / range;
        var output = a.Output + (b.Output - a.Output) * factor;
        return output;
    }
}