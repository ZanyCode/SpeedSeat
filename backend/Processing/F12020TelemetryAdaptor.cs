using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using F12020Telemetry;
using Microsoft.AspNetCore.SignalR;

public class F12020TelemetryAdaptor
{
    private readonly Speedseat seat;
    private readonly ISpeedseatSettings settings;
    private readonly IHubContext<TelemetryHub> telemetryHubContext;
    private bool isStreaming = false;

    public bool IsStreaming {get => isStreaming;}

    private F12020TelemetryClient client;

    private ISubject<PacketMotionData> updateRequestSubject = new Subject<PacketMotionData>();

    private DateTime lastFrontendTelemetryUpdate = DateTime.Now;
    private DateTime lastConsoleTelemetryUpdate = DateTime.Now;

    private List<Datapoint> frontTiltTelemetry = new List<Datapoint>();
    private List<Datapoint> sideTiltTelemetry = new List<Datapoint>();

    public F12020TelemetryAdaptor(Speedseat seat, ISpeedseatSettings settings, IHubContext<TelemetryHub> telemetryHubContext)
    {
        this.seat = seat;
        this.settings = settings;
        this.telemetryHubContext = telemetryHubContext;
    }

    public async void StartStreaming()
    {
        this.isStreaming = true;

        if(client != null)
            return;
        
        updateRequestSubject
            .CombineLatest(
                settings.FrontTiltGforceMultiplierObs, 
                settings.FrontTiltOutputCapObs,
                settings.FrontTiltReverseObs,
                settings.SideTiltGforceMultiplierObs,
                settings.SideTiltOutputCapObs,
                settings.SideTiltReverseObs)
            .Subscribe(x => UpdateSeatPosition(x.First, x.Second, x.Third, x.Fourth, x.Fifth, x.Sixth, x.Seventh));

        client = new F12020TelemetryClient(20777);
        client.OnMotionDataReceive += (data) => {
            if(this.isStreaming)
                updateRequestSubject.OnNext(data);
        };
    }

    private void UpdateSeatPosition(
        PacketMotionData motion, 
        double frontTiltGForceMultiplier,
        double frontTiltOutputCap,
        bool frontTiltReverse,
        double sideTiltGForceMultiplier,
        double sideTiltOutputCap,
        bool sideTiltReverse)
    {
        var first = motion.carMotionData.ElementAt(motion.header.playerCarIndex);      
        var frontTilt = Math.Clamp(first.gForceLongitudinal * frontTiltGForceMultiplier, -frontTiltOutputCap, frontTiltOutputCap) * (frontTiltReverse? -1 : 1);
        var sideTilt = Math.Clamp(first.gForceLateral * sideTiltGForceMultiplier, -sideTiltOutputCap, sideTiltOutputCap) * (sideTiltReverse? -1 : 1);
        seat.SetTilt(frontTilt, sideTilt);
        SendTelemetryToFrontend(frontTilt, sideTilt);
        PrintTelemetryToConsole(frontTilt, sideTilt);
    }

    private void SendTelemetryToFrontend(double frontTilt, double sideTilt)
    {
        this.frontTiltTelemetry.Add(new Datapoint {X = DateTime.Now, Y = frontTilt});
        this.sideTiltTelemetry.Add(new Datapoint {X = DateTime.Now, Y = sideTilt});
        if((DateTime.Now - lastFrontendTelemetryUpdate).TotalMilliseconds > 250)
        {
            this.telemetryHubContext.Clients.All.SendAsync("updateTelemetry", frontTiltTelemetry.ToArray(), sideTiltTelemetry.ToArray());
            this.frontTiltTelemetry.Clear();
            this.sideTiltTelemetry.Clear();
            this.lastFrontendTelemetryUpdate = DateTime.Now;
        }
    }

    private void PrintTelemetryToConsole(double frontTilt, double sideTilt)
    {
        if ((DateTime.Now - lastConsoleTelemetryUpdate).TotalMilliseconds > 250)
        {
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            Console.WriteLine($"[{DateTime.Now}] Received telemetry update. FrontTilt: {frontTilt}, SideTilt: {sideTilt}");
            this.lastConsoleTelemetryUpdate = DateTime.Now;
        }
    }

    public void StopStreaming()
    {
        this.isStreaming = false;
    }
}

class Datapoint
{
    public DateTime X { get; set; }
    public double Y { get; set; }
}