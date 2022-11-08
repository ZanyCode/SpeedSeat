using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

public interface IFrontendLogger
{
    public void Log(string message);

    IObservable<string> Messages { get; }
}

public class FrontendLogger : IFrontendLogger
{
    private readonly IHubContext<InfoHub> hubContext;

    public IObservable<string> Messages { get; private set; }

    private Subject<string> messagesSubject = new Subject<string>();

    public FrontendLogger(IHubContext<InfoHub> hubContext)
    {
        this.hubContext = hubContext;
        this.Messages = this.messagesSubject;
   
    }

    public void Log(string message)
    {
        this.messagesSubject.OnNext(message);
    }
}