using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;

public class TelemetryHub : Hub
{
    private readonly Speedseat seat;
    private readonly IHubContext<ManualControlHub> context;
    private readonly SpeedseatSettings settings;
    private readonly F12020TelemetryAdaptor telemetryAdaptor;

    public TelemetryHub(SpeedseatSettings settings, F12020TelemetryAdaptor telemetryAdaptor)
    {     
        this.settings = settings;
        this.telemetryAdaptor = telemetryAdaptor;
    }

    public void SetFrontTiltGForceMultiplier(double value)
    {
        this.settings.FrontTiltGforceMultiplier = value;
    }

    public void SetFrontTiltOutputCap(double value)
    {
        this.settings.FrontTiltOutputCap = value;
    }

    public void SetFrontTiltSmoothing(double value)
    {
        this.settings.FrontTiltSmoothing = value;
    }

    public void SetSideTiltGForceMultiplier(double value)
    {
        this.settings.SideTiltGforceMultiplier = value;
    }

    public void SetSideTiltOutputCap(double value)
    {
        this.settings.SideTiltOutputCap = value;
    }

    public void SetSideTiltSmoothing(double value)
    {
        this.settings.SideTiltSmoothing = value;
    }

    public void SetFrontTiltReverse(bool reverse) 
    {
        this.settings.FrontTiltReverse = reverse;
    }

    public void SetSideTiltReverse(bool reverse) 
    {
        this.settings.SideTiltReverse = reverse;
    }

    public void StartStreaming() 
    {
        this.telemetryAdaptor.StartStreaming();
    }

    public void StopStreaming()
    {
        this.telemetryAdaptor.StopStreaming();
    }

    public SpeedseatSettings GetCurrentState() {
        return settings;
    }

    public bool GetIsStreaming() {
        return telemetryAdaptor.IsStreaming;
    }
}