public enum ValueType
{
    Numeric,
    Boolean,
    Action
}

public class CommandValue
{
    public string? Label { get; set; }
    public ValueType Type { get; set; }
    public bool ScaleToFullRange { get; set; }
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 0xFFFF;
    public double Value { get; set; }

    public double Default { get; set; }

    public CommandValue()
    {

    }

    public CommandValue(ValueType type, double value, string? label = "", bool scaleToFullRange = true, double min = 0, double max = 1)
    {
        Label = label;
        Type = type;
        ScaleToFullRange = scaleToFullRange;
        Min = min;
        Max = max;
        Value = value;
    }

    public ushort ToUShort()
    {
        switch (Type)
        {
            case ValueType.Boolean:
                return (ushort)(Value != 0 ? 1 : 0);
            case ValueType.Numeric:
                return GetRawValue();
            case ValueType.Action:
                return (ushort)(Value != 0 ? 1 : 0);
            default:
                throw new Exception("Invalid Value Type");
        }
    }

    public CommandValue CloneWithNewValuesFromBytes(byte msb, byte lsb)
    {
        ushort fullValue = (ushort)((msb << 8) | lsb);
        double value = -1;
        if (ScaleToFullRange)
        {
            var normedValue = fullValue / (double)0xFFFF;
            value = this.Min + ((this.Max - this.Min) * normedValue);
        }
        else
        {
            if (fullValue < this.Min || fullValue > this.Max)
            {
                throw new Exception($"If ScaleToFullRange is false, the value has to be between Min({this.Min}) and Max({this.Max}) but instead was {fullValue}");
            }
            value = fullValue;
        }

        return new CommandValue(this.Type, value, this.Label, this.ScaleToFullRange, this.Min, this.Max);
    }

    public CommandValue CloneWithNewValue(double value)
    {
        return new CommandValue(this.Type, value, this.Label, this.ScaleToFullRange, this.Min, this.Max);
    }

    public ushort GetRawValue()
    {
        var clampedValue = Math.Clamp(Value, Min, Max);
        if (ScaleToFullRange)
        {
            var normedValue = (Value - Min) / (Max - Min);
            var scaledValue = (ushort)Math.Clamp(normedValue * ushort.MaxValue, 0, ushort.MaxValue);
            return scaledValue;
        }
        else
        {
            return (ushort)clampedValue;
        }
    }

    public override string ToString()
    {
        switch (this.Type)
        {
            case ValueType.Action:
                return "Action";
            case ValueType.Boolean:
                return $"Bool({(Value != 0 ? 1 : 0)})";
            case ValueType.Numeric:
                return $"Numeric(16 bit Raw Value: {GetRawValue()}, double representation: {Value})";
            default:
                return "WTF";
        }
    }
}

public class Command
{
    public const byte MotorPositionCommandId = 0;
    public const byte InitiateConnectionCommandId = 1;
    public const byte ConnectionInitiatedCommandId = 2;

    public string GroupLabel { get; set; }

    public byte Id { get; set; }

    public bool Readonly { get; set; }

    public CommandValue Value1 { get; set; }

    public CommandValue Value2 { get; set; }

    public CommandValue Value3 { get; set; }
    public bool IsReadRequest { get; private set; }

    public Command()
    {

    }

    public Command(byte id, CommandValue value1, CommandValue value2, CommandValue value3, bool isReadonly, bool isReadRequest, string groupLabel = "")
    {
        Id = id;
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Readonly = isReadonly;
        IsReadRequest = isReadRequest;
        GroupLabel = groupLabel;
    }

    public byte[] ToByteArray()
    {
        var (val1Msb, val1Lsb) = UShortToBytes(Value1?.ToUShort());
        var (val2Msb, val2Lsb) = UShortToBytes(Value2?.ToUShort());
        var (val3Msb, val3Lsb) = UShortToBytes(Value3?.ToUShort());

        var bytes = new byte[8];
        bytes[0] = (byte)((IsReadRequest ? 1 : 0) | Id << 1);
        bytes[1] = val1Msb;
        bytes[2] = val1Lsb;
        bytes[3] = val2Msb;
        bytes[4] = val2Lsb;
        bytes[5] = val3Msb;
        bytes[6] = val3Lsb;
        bytes[7] = (byte)(bytes[0] ^ bytes[1] ^ bytes[2] ^ bytes[3] ^ bytes[4] ^ bytes[5] ^ bytes[6]);

        return bytes;
    }

    public static bool IsHashValid(byte[] currentCommandBytes)
    {
        if (currentCommandBytes.Count() != 8)
            return false;

        return currentCommandBytes.Take(7).Aggregate((acc, val) => (byte)(acc ^ val)) == currentCommandBytes.Last();
    }

    public static byte ExtractIdFromByteArray(byte[] data)
    {
        if (data == null || data.Count() <= 0)
            throw new Exception("Can't extract id from empty/null array in Command.ExtractIdFromByteArray()");

        return (byte)(data[0] >> 1);
    }

    public Command CloneWithNewValuesFromByteArray(byte[] data)
    {
        var id = ExtractIdFromByteArray(data);
        if (id != this.Id)
            throw new Exception($"Tried to set values for command with id {this.Id} from byte array, but that byte array represents command with a different id: {id}");

        bool isReadRequest = (data[0] & 0x01) == 1;
        var value1 = this.Value1 == null ? null : this.Value1.CloneWithNewValuesFromBytes(data[1], data[2]);
        var value2 = this.Value2 == null ? null : this.Value2.CloneWithNewValuesFromBytes(data[3], data[4]);
        var value3 = this.Value3 == null ? null : this.Value3.CloneWithNewValuesFromBytes(data[5], data[6]);
        return new Command(this.Id, value1, value2, value3, this.Readonly, isReadRequest, this.GroupLabel);
    }

    public Command CloneWithNewValues(double value1, double value2, double value3, bool isReadRequest)
    {
        var commandValue1 = this.Value1 == null ? null : this.Value1.CloneWithNewValue(value1);
        var commandValue2 = this.Value2 == null ? null : this.Value2.CloneWithNewValue(value2);
        var commandValue3 = this.Value3 == null ? null : this.Value3.CloneWithNewValue(value3);
        return new Command(this.Id, commandValue1, commandValue2, commandValue3, this.Readonly, isReadRequest, this.GroupLabel);
    }


    private (byte msb, byte lsb) UShortToBytes(ushort? value)
    {
        if (value == null)
            return (0, 0);

        var bytes = BitConverter.GetBytes((ushort)value);
        return (bytes[1], bytes[0]);
    }

    public override string ToString()
    {
        return $"Id: {Id}, Value1: {(Value1 == null ? "null" : Value1.ToString())}, Value2: {(Value2 == null ? "null" : Value2.ToString())}, Value3: {(Value3 == null ? "null" : Value3.ToString())}";
    }
}

public class Config
{
    public int ConnectionResponseTimeoutMs { get; set; } = 1000;
    public int CommandSendRetryIntervalMs { get; set; } = 1000;    
    public int UiUpdateIntervalMs { get; set; } = 600;    

    public IEnumerable<Command> Commands { get; set; }
}