using Microsoft.AspNetCore.SignalR;

public class ManualControlHub : Hub
{
    public async Task UpdateMotorValue(int motorPosition, double value)
    {
        System.Console.WriteLine(motorPosition);
        System.Console.WriteLine(value);
    }
}
