using System.Reactive.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

public class SeatSettingsHub : Hub
{
    private readonly ISpeedseatSettings settings;
    private readonly CommandService commandService;
    private readonly IOptionsMonitor<Config> options;
    private readonly IFrontendLogger logger;
    private readonly Speedseat seat;
    private IDictionary<byte, IAsyncEnumerable<Command>> commandEnumerables = new Dictionary<byte, IAsyncEnumerable<Command>>();

    public SeatSettingsHub(ISpeedseatSettings settings, CommandService commandService, IOptionsMonitor<Config> options, IFrontendLogger logger, Speedseat seat)
    {
        this.settings = settings;
        this.commandService = commandService;
        this.options = options;
        this.logger = logger;
        this.seat = seat;
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
        if (!commandEnumerables.ContainsKey(command.Id))
        {
            var enumerable = this.settings.SubscribeToConfigurableSetting(command).ToAsyncEnumerable();
            commandEnumerables.Add(command.Id, enumerable);
        }

        return commandEnumerables[command.Id];
    }

    public async Task UpdateSetting(Command command)
    {
        if (command.IsReadonly)
            throw new Exception($"Command with id 0x{Convert.ToHexString(new[] { command.Id })} can't be updated since it is readonly");

        if (command.Id == Command.MotorPositionCommandId)
        {
            await seat.SetMotorPositionsFromCommand(command);
            logger.Log($"Success sending Motor Positions to Microcontroller: {command.ToString()}, raw representation: {Convert.ToHexString(command.ToByteArray())}");
        }
        else
        {
            var result = await commandService.WriteCommand(command);
            if (result == SerialWriteResult.Success)
                logger.Log($"Success sending command to Microcontroller: {command.ToString()}, raw representation: {Convert.ToHexString(command.ToByteArray())}");
            else
                logger.Log($"Error sending command to Microcontroller: {command.ToString()}, raw representation: {Convert.ToHexString(command.ToByteArray())}, Result Code: {result}");
        }
        settings.SaveConfigurableSetting(command, false);
    }

    public async Task FakeWriteRequest(Command command)
    {
        if (command.IsReadonly)
            throw new Exception($"Command with id 0x{Convert.ToHexString(new[] { command.Id })} can't be updated since it is readonly");

        await commandService.FakeWriteRequest(command);
    }

    public override Task OnConnectedAsync()
    {
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var item in commandEnumerables.Values)
            await item.GetAsyncEnumerator().DisposeAsync();

        await base.OnDisconnectedAsync(exception);
    }
}