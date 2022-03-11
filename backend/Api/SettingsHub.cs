using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;

public class SettingsHub : Hub
{
    private readonly SpeedseatSettings settings;
    private readonly Speedseat seat;

    public SettingsHub(SpeedseatSettings settings, Speedseat seat)
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

    public SpeedseatSettings GetSettings()
    {
        return settings;
    }
}