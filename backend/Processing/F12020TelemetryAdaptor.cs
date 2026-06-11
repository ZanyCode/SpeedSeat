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

    private F12020TelemetryClient client;

    private ISubject<PacketMotionData> updateRequestSubject = new Subject<PacketMotionData>();

    private DateTime lastFrontendTelemetryUpdate = DateTime.Now;
    private DateTime lastConsoleTelemetryUpdate = DateTime.Now;

    private List<Datapoint> frontTiltTelemetry = new List<Datapoint>();
    private List<Datapoint> sideTiltTelemetry = new List<Datapoint>();

    // Game year from the packetFormat of the incoming telemetry, null while no game is sending.
    public int? DetectedGame => client?.DetectedPacketFormat;

    public F12020TelemetryAdaptor(Speedseat seat, ISpeedseatSettings settings, IHubContext<TelemetryHub> telemetryHubContext)
    {
        this.seat = seat;
        this.settings = settings;
        this.telemetryHubContext = telemetryHubContext;
    }

    // Called once at application startup. Telemetry is processed for the whole lifetime
    // of the backend, so there is no streaming state to manage from the UI.
    public void Start()
    {
        if(started)
            return;
        started = true;

        updateRequestSubject
            .CombineLatest(
                settings.FrontTiltGforceMultiplierObs,
                settings.FrontTiltOutputCapObs,
                settings.FrontTiltReverseObs,
                settings.SideTiltGforceMultiplierObs,
                settings.SideTiltOutputCapObs,
                settings.SideTiltReverseObs)
            .Subscribe(x => UpdateSeatPosition(x.First, x.Second, x.Third, x.Fourth, x.Fifth, x.Sixth, x.Seventh));

        _ = CreateClientWithRetry();
    }

    private bool started = false;

    // Binding UDP port 20777 fails while another process (a second backend instance,
    // a debugging tool) still holds it — keep retrying instead of silently giving up.
    private async Task CreateClientWithRetry()
    {
        while (client == null)
        {
            try
            {
                var newClient = new F12020TelemetryClient(20777);
                newClient.OnDetectedPacketFormatChanged += format =>
                    this.telemetryHubContext.Clients.All.SendAsync("gameDetected", (int?)format);
                newClient.OnMotionDataReceive += (data) => updateRequestSubject.OnNext(data);
                client = newClient;
                Console.WriteLine("Listening for F1 telemetry on UDP port 20777.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not open telemetry port 20777 ({e.Message}), retrying in 5 seconds.");
                await Task.Delay(5000);
            }
        }
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
        // Never let an exception escape: this runs inside an Rx Subscribe callback, and a
        // single throw would permanently kill the subscription (telemetry stops forever).
        try
        {
            var first = motion.carMotionData.ElementAt(motion.header.playerCarIndex);
            var frontTilt = Math.Clamp(first.gForceLongitudinal * frontTiltGForceMultiplier, -frontTiltOutputCap, frontTiltOutputCap) * (frontTiltReverse? -1 : 1);
            var sideTilt = Math.Clamp(first.gForceLateral * sideTiltGForceMultiplier, -sideTiltOutputCap, sideTiltOutputCap) * (sideTiltReverse? -1 : 1);
            seat.SetTilt(frontTilt, sideTilt);
            SendTelemetryToFrontend(frontTilt, sideTilt);
            PrintTelemetryToConsole(frontTilt, sideTilt);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error processing telemetry update: {e.Message}");
        }
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
            // SetCursorPosition throws when the cursor is at the top or output is redirected.
            try
            {
                if (!Console.IsOutputRedirected && Console.CursorTop > 0)
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
            }
            catch { }
            Console.WriteLine($"[{DateTime.Now}] Received telemetry update. FrontTilt: {frontTilt}, SideTilt: {sideTilt}");
            this.lastConsoleTelemetryUpdate = DateTime.Now;
        }
    }

}

class Datapoint
{
    public DateTime X { get; set; }
    public double Y { get; set; }
}