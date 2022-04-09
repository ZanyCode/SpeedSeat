using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;

public class InfoHub : Hub 
{
    private readonly IHttpContextAccessor ctx;
    private readonly IWebHostEnvironment env;

    public InfoHub(IHttpContextAccessor ctx, IWebHostEnvironment env)
    {
        this.ctx = ctx;
        this.env = env;
    }

    public string GetOwnUrl() 
    {
        return GetLocalIPAddress(env.IsDevelopment());
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
}