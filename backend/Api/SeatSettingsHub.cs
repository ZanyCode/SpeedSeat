using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

public class SeatSettingsHub : Hub
{ 
    private readonly SpeedseatSettings settings;
    private readonly CommandService commandService;
    private readonly IOptionsMonitor<Config> options;

    public SeatSettingsHub(SpeedseatSettings settings, CommandService commandService, IOptionsMonitor<Config> options)
    {
        this.settings = settings;
        this.commandService = commandService;
        this.options = options;
    }

    public IEnumerable<Command> GetCommands()
    {
        return this.options.CurrentValue.Commands;
    }

    public async Task<SerialWriteResult> UpdateSetting(Command command)
    {
        if(command.IsReadonly)
            throw new Exception($"Command with id 0x{Convert.ToHexString(new [] {command.WriteId})} can't be updated since it is readonly");

        return await commandService.WriteCommand(command);
    }
}