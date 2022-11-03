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
        foreach (var command in commands)
        {
            var (value1, value2, value3) = settings.GetConfigurableSettingsValues(command);
            if (command.Value1 != null)
                command.Value1.Value = value1;
            if (command.Value2 != null)
                command.Value2.Value = value2;
            if (command.Value3 != null)
                command.Value3.Value = value3;
        }
        return commands;
    }

    public IAsyncEnumerable<Command> SubscribeToConfigurableSetting(Command command)
    {
        return this.settings.SubscribeToConfigurableSetting(command).ToAsyncEnumerable();
    }

    public async Task<SerialWriteResult> UpdateSetting(Command command)
    {
        if (command.IsReadonly)
            throw new Exception($"Command with id 0x{Convert.ToHexString(new[] { command.Id })} can't be updated since it is readonly");

        settings.SaveConfigurableSetting(command);
        var result = await commandService.WriteCommand(command);
        if (result == SerialWriteResult.Success)
            logger.Log($"Success sending command to Microcontroller: {command.ToString()}, raw representation: {Convert.ToHexString(command.ToByteArray())}");
        else
            logger.Log($"Error sending command to Microcontroller: {command.ToString()}, raw representation: {Convert.ToHexString(command.ToByteArray())}, Result Code: {result}");
        return result;
    }

    public async Task FakeWriteRequest(Command command)
    {
        if (command.IsReadonly)
            throw new Exception($"Command with id 0x{Convert.ToHexString(new[] { command.Id })} can't be updated since it is readonly");

        await commandService.FakeWriteRequest(command);
    }
}