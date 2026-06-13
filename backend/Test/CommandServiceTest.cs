using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
public class CommandServiceTest
{
    CommandService sut;
    Mock<IDeviceConnection> portConnectionMock;
    Mock<ISpeedseatSettings> speedseatSettingsMock;
    Mock<IOptionsMonitor<Config>> configOptionsMock;

    [TestInitialize]
    public void Init()
    {
        var connectionFactoryMock = new Mock<IDeviceConnectionFactory>();
        portConnectionMock = new Mock<IDeviceConnection>();
        connectionFactoryMock.Setup(x => x.Create(It.IsAny<string>())).Returns(portConnectionMock.Object);
        speedseatSettingsMock = new Mock<ISpeedseatSettings>();
        configOptionsMock = new Mock<IOptionsMonitor<Config>>();
        configOptionsMock.SetupGet(x => x.CurrentValue).Returns(new Config { ConnectionResponseTimeoutMs = 2000 });

        var loggerMock = new Mock<IFrontendLogger>();
        loggerMock.Setup(x => x.Log(It.IsAny<string>())).Callback<string>(m => Console.WriteLine("[LOG] " + m));
        sut = new CommandService(loggerMock.Object, connectionFactoryMock.Object, configOptionsMock.Object, speedseatSettingsMock.Object, new Mock<IHubContext<SeatSettingsHub>>().Object);
    }

    [TestMethod]
    public async Task ConnectWithSendingResponseShouldSucceed()
    {
        // Arrange
        SetupPortResponses(new Dictionary<byte, Func<byte[], byte[]>>
        {
            [Command.InitiateConnectionCommandId] = x => new Command(Command.ConnectionInitiatedCommandId, null, null, null, false, false).ToByteArray()
        }, millisecondDelayBeforeResponse: 100);

        // Act
        bool result = await sut.Connect("");

        // Assert        
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task ConnectWithoutSendingResponseShouldReturnFalse()
    {
        // Act
        bool result = await sut.Connect("");

        // Assert
        portConnectionMock.Verify(x => x.Write(It.Is<byte[]>(data => Command.ExtractIdFromByteArray(data) == Command.InitiateConnectionCommandId), It.IsAny<int>(), It.IsAny<int>()));
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task CommandServiceShouldSuccessfullySendCommandIfResponseReceived()
    {
        // Arrange
        SetupPortResponses(new Dictionary<byte, Func<byte[], byte[]>>
        {
            [Command.InitiateConnectionCommandId] = x => new Command(Command.ConnectionInitiatedCommandId, null, null, null, false, false).ToByteArray()
        }, millisecondDelayBeforeResponse: 0);


        // Act
        await sut.Connect("");
        var result = await sut.WriteCommand(new Command(0, value1: null, value2: null, value3: null, false, false, "Fetter Command"));

        // Assert
        portConnectionMock.Verify(x => x.Write(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(3)); // Initiate-Connection command, ack byte for the Connection-Initiated response, and the command that was sent
        Assert.AreEqual(WriteResult.Success, result);
    }

    [TestMethod]
    public async Task CommandServiceShouldRetrySendingCommandThreeTimes()
    {
        // Arrange
        SetupPortResponses(new Dictionary<byte, Func<byte[], byte[]>>
        {
            [Command.InitiateConnectionCommandId] = x => new Command(Command.ConnectionInitiatedCommandId, null, null, null, false, false).ToByteArray()
        });

        await sut.Connect("");
        SetupPortResponses(alwaysSendAckByte: false);

        // Act
        await sut.WriteCommand(new Command(0, value1: null, value2: null, value3: null, false, false, "Fetter Command"));

        // Assert
        // 2 writes from Connect (Initiate-Connection command + ack byte) and 3 unanswered send attempts
        portConnectionMock.Verify(x => x.Write(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(5));
    }

    [TestMethod]
    public async Task CommandServiceShouldCorrectlyWriteCommand()
    {
        // Arrange 
        SetupPortResponses(new Dictionary<byte, Func<byte[], byte[]>>
        {
            [Command.InitiateConnectionCommandId] = x => new Command(Command.ConnectionInitiatedCommandId, null, null, null, false, false).ToByteArray()
        });
        await sut.Connect("");
        await Task.Delay(50); // let the background ack write from Connect finish before re-mocking the port

        // Ids 4/5 — must not collide with the reserved ids (0x00-0x02, 0x40-0x42),
        // those are intercepted by CommandService before the config lookup.
        var testConfigCommands = new[] {
            new Command(4,
                new CommandValue(ValueType.Numeric, 0, "V1", true, 0, 1),
                new CommandValue(ValueType.Numeric, 0, "V2", false, 0, ushort.MaxValue),
                new CommandValue(ValueType.Boolean, 0, "V3", false, 0, 1),
                false, false, ""),
            new Command(5,
                new CommandValue(ValueType.Numeric, 0, "V2", false, 0, ushort.MaxValue),
                new CommandValue(ValueType.Boolean, 0, "V3", false, 0, 1),
                null,
                false, false, "")
            };

        var testWriteCommands = new[] {
            new Command(4,
                new CommandValue(ValueType.Numeric, 0.5, "V1", true, 0, 1),
                new CommandValue(ValueType.Numeric, 12587, "V2", false, 0, ushort.MaxValue),
                new CommandValue(ValueType.Boolean, 1, "V3", true, 0, 1),
                false, false, ""),
            new Command(5,
                new CommandValue(ValueType.Numeric, 12587, "V2", false, 0, ushort.MaxValue),
                new CommandValue(ValueType.Boolean, 1, "V3", true, 0, 1),
                null,
                false, false, "")
            };

        this.configOptionsMock.SetupGet(x => x.CurrentValue).Returns(new Config { Commands = testConfigCommands });
        this.portConnectionMock.Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Callback(() => { });

        // Act & Assert for each command
        foreach (var command in testWriteCommands)
        {
            var testCommandBytes = command.ToByteArray();
            portConnectionMock.SetupGet(x => x.BytesToRead).Returns(testCommandBytes.Length);
            portConnectionMock.Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Callback<byte[], int, int>((buffer, offset, count) =>
            {
                Array.Copy(testCommandBytes.Skip(offset).ToArray(), buffer, count);
            });

            Command actualCommand = null;
            speedseatSettingsMock.Setup(x => x.SaveConfigurableSetting(It.IsAny<Command>())).Callback<Command>(x => actualCommand = x);

            portConnectionMock.Raise(x => x.DataReceived += null, EventArgs.Empty);
            Assert.IsNotNull(actualCommand);
            Assert.AreEqual(command.Id, actualCommand.Id);

            var expectedCommandValues = new[] { command.Value1, command.Value2, command.Value3 };
            var actualCommandValues = new[] { actualCommand.Value1, actualCommand.Value2, actualCommand.Value3 };
            for (int i = 0; i < expectedCommandValues.Count(); i++)
            {
                if (expectedCommandValues[i] == null)
                    Assert.IsNull(actualCommandValues[i]);
                else
                    Assert.IsTrue(Math.Abs(expectedCommandValues[i].Value - actualCommandValues[i].Value) < 0.001, $"Values differ by more than a rounding error: Expected {expectedCommandValues[i].Value}, Actual {actualCommandValues[i].Value}");
            }
        }
    }

    [TestMethod]
    public async Task CommandServiceShouldCorrectlyRespondToReadCommandRequest()
    {
        // Arrange 
        SetupPortResponses(new Dictionary<byte, Func<byte[], byte[]>>
        {
            [Command.InitiateConnectionCommandId] = x => new Command(Command.ConnectionInitiatedCommandId, null, null, null, false, false).ToByteArray()
        });
        await sut.Connect("");
        await Task.Delay(50); // let the background ack write from Connect finish before re-mocking the port
        this.portConnectionMock.Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Callback(() => { });

        double commandValue = 0.5;
        var testConfigCommands = new[] {
            new Command(0,
                new CommandValue(ValueType.Numeric, 0, "V1", true, 0, 1), null, null,
                false, false, ""),
            };
        var readRequest = new Command(0, null, null, null, false, true, "");
        var readRequestBytes = readRequest.ToByteArray();
        portConnectionMock.SetupGet(x => x.BytesToRead).Returns(readRequestBytes.Length);
        portConnectionMock.Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Callback<byte[], int, int>((buffer, offset, count) =>
         {
             Array.Copy(readRequestBytes.Skip(offset).ToArray(), buffer, count);
         });

        Command responseCommand = null;
        portConnectionMock.Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Callback<byte[], int, int>((buffer, offset, count) =>
        {
            if (buffer.Length > 1)
                responseCommand = testConfigCommands[0].CloneWithNewValuesFromByteArray(buffer);
            else
            {
                // Send success response
                portConnectionMock.SetupGet(x => x.BytesToRead).Returns(1);
                portConnectionMock.Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Callback<byte[], int, int>((x, y, z) =>
                {
                    x[0] = 255;
                }).Returns(1);
                portConnectionMock.Raise(x => x.DataReceived += null, EventArgs.Empty);
            }
        });

        configOptionsMock.SetupGet(x => x.CurrentValue).Returns(new Config { Commands = testConfigCommands });
        speedseatSettingsMock.Setup(x => x.GetConfigurableSettingsValues(It.IsAny<Command>())).Returns((commandValue, 0, 0));

        // Act
        portConnectionMock.Raise(x => x.DataReceived += null, EventArgs.Empty);

        // Assert
        // Should respond with ack byte
        portConnectionMock.Verify(x => x.Write(It.Is<byte[]>(b => b[0] == 0xFF), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        Assert.IsNotNull(responseCommand, "Did not receive any response command");
        Assert.IsFalse(responseCommand.IsReadRequest);
        Assert.IsTrue(Math.Abs(responseCommand.Value1.Value - commandValue) < 0.001);
    }

    [TestMethod]
    public void ToStringShouldCorrectlyPrintRawAndDoubleRepresentation()
    {
        var commandValue = new CommandValue(ValueType.Numeric, 0, "", false, 20, 1500);
        var res = commandValue.ToString();                
    }

    private void SetupPortResponses(IDictionary<byte, Func<byte[], byte[]>>? responseFuncs = null, bool alwaysSendAckByte = true, int millisecondDelayBeforeResponse = 0)
    {
        portConnectionMock.Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Callback<byte[], int, int>((buffer, length, offset) =>
        {
            if (buffer.Length == 8 && responseFuncs != null)
            {
                byte commandId = Command.ExtractIdFromByteArray(buffer);
                byte[] responseBytes = alwaysSendAckByte ? new byte[] { 0xFF } : new byte[] { };
                if (responseFuncs.ContainsKey(commandId))
                {
                    var funcResponse = responseFuncs[commandId](buffer);
                    responseBytes = responseBytes.Concat(funcResponse).ToArray();
                }

                portConnectionMock.SetupGet(x => x.BytesToRead).Returns(responseBytes.Length);
                portConnectionMock.Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Callback<byte[], int, int>((buffer, offset, count) =>
                       {
                           Array.Copy(responseBytes.Skip(offset).ToArray(), buffer, count);
                       });
                if (millisecondDelayBeforeResponse == 0)
                    RaisePortDataReceivedEvent();
                else
                    Task.Factory.StartNew(async () =>
                    {
                        await Task.Delay(millisecondDelayBeforeResponse);
                        RaisePortDataReceivedEvent();
                    });
            }
            else if (alwaysSendAckByte)
            {
                byte[] responseBytes = { 0xFF };
                portConnectionMock.SetupGet(x => x.BytesToRead).Returns(responseBytes.Length);
                portConnectionMock.Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Callback<byte[], int, int>((buffer, offset, count) =>
                       {
                           Array.Copy(responseBytes.Skip(offset).ToArray(), buffer, count);
                       });
                RaisePortDataReceivedEvent();
            }
        });
    }

    private void RaisePortDataReceivedEvent()
    {
        portConnectionMock.Raise(x => x.DataReceived += null, EventArgs.Empty);
    }
}