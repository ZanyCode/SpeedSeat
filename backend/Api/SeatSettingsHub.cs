using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

public class SeatSettingsHub : Hub
{ 
    private readonly ISpeedseatSettings settings;
    private readonly CommandService commandService;
    private readonly IOptionsMonitor<Config> options;
    private readonly IFrontendLogger logger;

    public SeatSettingsHub(ISpeedseatSettings settings, CommandService commandService, IOptionsMonitor<Config> options, IFrontendLogger logger)
    {
        this.settings = settings;
        this.commandService = commandService;
        this.options = options;
        this.logger = logger;
    }

    public IEnumerable<Command> GetCommands()
    {
        var commands = this.options.CurrentValue.Commands;
        foreach(var command in commands)
        {
            var (value1, value2, value3) = settings.GetConfigurableSettingsValues(command);
            command.Value1.Value = value1;
            command.Value2.Value = value2;
            command.Value3.Value = value3;
        }
        return commands;
    }

    public async Task<SerialWriteResult> UpdateSetting(Command command)
    {
        if(command.IsReadonly)
            throw new Exception($"Command with id 0x{Convert.ToHexString(new [] {command.Id})} can't be updated since it is readonly");

        settings.SaveConfigurableSetting(command);
        logger.Log($"Sending command to Microcontroller: {command.ToString()}, raw representatin: {Convert.ToHexString(command.ToByteArray())}");
        return await commandService.WriteCommand(command);
    }
}