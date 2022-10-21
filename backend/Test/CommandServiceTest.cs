using System.IO.Ports;
using System.Reflection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
public class CommandServiceTest
{
    CommandService sut;
    Mock<ISerialPortConnection> portConnectionMock;
    Mock<ISpeedseatSettings> speedseatSettingsMock;
    Mock<IOptionsMonitor<Config>> configOptionsMock;

    [TestInitialize]
    public void Init()
    {
        var connectionFactoryMock = new Mock<ISerialPortConnectionFactory>();
        portConnectionMock = new Mock<ISerialPortConnection>();
        connectionFactoryMock.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<int>())).Returns(portConnectionMock.Object);
        speedseatSettingsMock = new Mock<ISpeedseatSettings>();
        configOptionsMock = new Mock<IOptionsMonitor<Config>>();

        sut = new CommandService(new Mock<IFrontendLogger>().Object, connectionFactoryMock.Object, configOptionsMock.Object, speedseatSettingsMock.Object);
    }

    [TestMethod]
    public async Task CommandServiceShouldSuccessfullySendCommandIfResponseReceived()
    {
        // Arrange
        sut.Connect("", 0);
        portConnectionMock
            .Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
            .Raises(e => e.DataReceived += null, () => CreateEventArgs());
        portConnectionMock.SetupGet(x => x.BytesToRead).Returns(1);
        portConnectionMock.Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>())).Callback<byte[], int, int>((x, y, z) =>
        {
            x[0] = 255;
        }).Returns(1);

        // Act
        var result = await sut.WriteCommand(new Command(0, value1: null, value2: null, value3: null, false, false, "Fetter Command"));

        // Assert
        portConnectionMock.Verify(x => x.Write(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(1));
        Assert.AreEqual(SerialWriteResult.Success, result);
    }

    [TestMethod]
    public async Task CommandServiceShouldRetrySendingCommandThreeTimes()
    {
        // Arrange
        sut.Connect("", 0);

        // Act
        await sut.WriteCommand(new Command(0, value1: null, value2: null, value3: null, false, false, "Fetter Command"));

        // Assert
        portConnectionMock.Verify(x => x.Write(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(3));
    }

    [TestMethod]
    public async Task CommandServiceShouldCorrectlyWriteCommand()
    {
        // Arrange 
        sut.Connect("", 0);
        var testConfigCommands = new[] {
            new Command(0,
                new CommandValue(ValueType.Numeric, 0, "V1", true, 0, 1),
                new CommandValue(ValueType.Numeric, 0, "V2", false, 0, ushort.MaxValue),
                new CommandValue(ValueType.Boolean, 0, "V3", false, 0, 1),
                false, false, ""),
            new Command(1,
                new CommandValue(ValueType.Numeric, 0, "V2", false, 0, ushort.MaxValue),
                new CommandValue(ValueType.Boolean, 0, "V3", false, 0, 1),
                null,
                false, false, "")
            };

        var testWriteCommands = new[] {
            new Command(0,
                new CommandValue(ValueType.Numeric, 0.5, "V1", true, 0, 1),
                new CommandValue(ValueType.Numeric, 12587, "V2", false, 0, ushort.MaxValue),
                new CommandValue(ValueType.Boolean, 1, "V3", true, 0, 1),
                false, false, ""),
            new Command(1,
                new CommandValue(ValueType.Numeric, 12587, "V2", false, 0, ushort.MaxValue),
                new CommandValue(ValueType.Boolean, 1, "V3", true, 0, 1),
                null,
                false, false, "")
            };

        this.configOptionsMock.SetupGet(x => x.CurrentValue).Returns(new Config { Commands = testConfigCommands });

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

            portConnectionMock.Raise(x => x.DataReceived += null, CreateEventArgs());
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
        sut.Connect("", 0);
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
                portConnectionMock.Raise(x => x.DataReceived += null, CreateEventArgs());
            }
        });

        configOptionsMock.SetupGet(x => x.CurrentValue).Returns(new Config { Commands = testConfigCommands });
        speedseatSettingsMock.Setup(x => x.GetConfigurableSettingsValues(It.IsAny<Command>())).Returns((commandValue, 0, 0));

        // Act
        portConnectionMock.Raise(x => x.DataReceived += null, CreateEventArgs());

        // Assert
        // Should respond with ack byte
        portConnectionMock.Verify(x => x.Write(It.Is<byte[]>(b => b[0] == 0xFF), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        Assert.IsNotNull(responseCommand, "Did not receive any response command");
        Assert.IsFalse(responseCommand.IsReadRequest);
        Assert.IsTrue(Math.Abs(responseCommand.Value1.Value - commandValue) < 0.001);

    }

    // Test Timeout while reading command
    // Test To Send Commands quickly without waiting for response
    // Integration Test for saving and retrieving command value
    // Test that Command with invalid hash gets rejected

    public SerialDataReceivedEventArgs CreateEventArgs()
    {
        // the types of the constructor parameters, in order
        // use an empty Type[] array if the constructor takes no parameters
        Type[] paramTypes = new Type[] { typeof(SerialData) };

        // the values of the constructor parameters, in order
        // use an empty object[] array if the constructor takes no parameters
        object[] paramValues = new object[] { SerialData.Chars };

        SerialDataReceivedEventArgs instance =
            Construct<SerialDataReceivedEventArgs>(paramTypes, paramValues);

        return instance;
    }

    public static T Construct<T>(Type[] paramTypes, object[] paramValues)
    {
        Type t = typeof(T);

        ConstructorInfo ci = t.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null, paramTypes, null);

        return (T)ci.Invoke(paramValues);
    }
}