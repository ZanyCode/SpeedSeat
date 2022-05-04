using System;
using F12020Telemetry;

public class F12020TelemetryAdaptor : IHostedService
{
    private readonly Speedseat seat;

    public F12020TelemetryAdaptor(Speedseat seat)
    {
        this.seat = seat;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var client = new F12020TelemetryClient(20777);
        client.OnMotionDataReceive += (data) =>  {
            var first = data.carMotionData.ElementAt(data.header.playerCarIndex);
            var longForce = (first.gForceLongitudinal >= 0? 1 : -1) * (Math.Abs(first.gForceLongitudinal) / 6) + 0.5;
            var latForce = (first.gForceLateral >= 0? 1 : -1) * (Math.Abs(first.gForceLateral) / 6) + 0.5;
            System.Console.WriteLine($"{latForce}, {longForce}, {first.gForceVertical}");
            seat.SetTilt(longForce, latForce);
        };
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
    }
}