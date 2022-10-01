using Microsoft.AspNetCore.SignalR;

public interface IFrontendLogger
{
    public void Log(string message);
}

public class FrontendLogger : IFrontendLogger
{
    private readonly IHubContext<InfoHub> hubContext;

    public FrontendLogger(IHubContext<InfoHub> hubContext)
    {
        this.hubContext = hubContext;
    }

    public void Log(string message)
    {
        this.hubContext.Clients.All.SendAsync("log", message);
    }
}