using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

public class InfoHub : Hub
{
    private readonly IHttpContextAccessor ctx;
    private readonly IWebHostEnvironment env;
    private readonly IFrontendLogger logger;
    private readonly IOptionsMonitor<Config> options;
    private IAsyncEnumerable<string> logMessages;

    public InfoHub(IHttpContextAccessor ctx, IWebHostEnvironment env, IFrontendLogger logger, IOptionsMonitor<Config> options)
    {
        this.ctx = ctx;
        this.env = env;
        this.logger = logger;
        this.options = options;
    }

    public string GetOwnUrl()
    {
        return GetLocalIPAddress(env.IsDevelopment());
    }

    public IAsyncEnumerable<string> LogMessages()
    {
        if (logMessages == null)
        {
            var logMessagesLists = logger.Messages.Scan(new List<string>(), (acc, val) => {
                acc.Add(val);
                return acc;
            });

            var timer = Observable.Timer(TimeSpan.Zero, TimeSpan.FromMilliseconds(options.CurrentValue.UiUpdateIntervalMs));
            var throttledMessages = timer.WithLatestFrom(logMessagesLists).Select(x => {                
                var (_, messages) = x;
                if(messages.Count <= 0)
                    return null;
                else if(messages.Count > 20)
                {
                    var count = messages.Count;
                    messages.Clear();
                    messages.Add($"Warning: clearing {count} unsent logs from server side buffer to prevent overflow. Maybe slow down a little bit with all that spam!");
                }
                var message = messages.ElementAt(0);
                messages.RemoveAt(0);
                return message;                
            }).Where(x => x != null);
          
            logMessages = throttledMessages.ToAsyncEnumerable();
        }

        return logMessages;
    }

    public static string GetLocalIPAddress(bool isDevelopment)
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().EndsWith(".1"))
            {
                var ipstr = ip.ToString();
                return $"http://{ipstr}:{(isDevelopment ? 4200 : 5000)}";
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        this.logMessages?.GetAsyncEnumerator().DisposeAsync();
        this.logMessages = null;
        return base.OnDisconnectedAsync(exception);
    }
}