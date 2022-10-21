using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;

public class ProgramSettingsHub : Hub
{
    private readonly ISpeedseatSettings settings;
    private readonly Speedseat seat;

    public ProgramSettingsHub(ISpeedseatSettings settings, Speedseat seat)
    {
        this.settings = settings;
        this.seat = seat;
    }

    public void SetFrontLeftMotorIdx(int idx)
    {
        settings.FrontLeftMotorIdx = idx;
    }

    public void SetFrontRightMotorIdx(int idx)
    {
        settings.FrontRightMotorIdx = idx;
    }

    public void SetBackMotorIdx(int idx)
    {
        settings.BackMotorIdx = idx;
    }

    public void SetFrontTiltPriority(double priority)
    {
        settings.FrontTiltPriority = priority;
    }

    public void SetBackMotorResponseCurve(IEnumerable<ResponseCurvePoint> curve)
    {
        settings.BackMotorResponseCurve = curve;
    }

    public void SetSideMotorResponseCurve(IEnumerable<ResponseCurvePoint> curve)
    {
        settings.SideMotorResponseCurve = curve;
    }

    public ISpeedseatSettings GetSettings()
    {
        return settings;
    }
}