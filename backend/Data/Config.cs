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
    public bool ScaleToFullRange{ get; set; }
    public double Min { get; set; }
    public double Max { get; set; }    
    public double Value { get; set; }

    public double Default { get; set; }

    public CommandValue()
    {
        
    }

    public CommandValue(ValueType type, double value, string? label="", bool scaleToFullRange = true, double min = 0, double max = 1)
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
        switch(Type)
        {
            case ValueType.Boolean:
                return (ushort)(Value != 0? 1 : 0);
            case ValueType.Numeric:
                var clampedValue = Math.Clamp(Value, Min, Max);
                if(ScaleToFullRange)
                {
                    var normedValue = (Value - Min) / (Max - Min);
                    var scaledValue = (ushort)Math.Clamp(normedValue * ushort.MaxValue, 0, ushort.MaxValue); 
                    return scaledValue;
                }
                else
                {
                    return (ushort)clampedValue;
                }
            case ValueType.Action:
               return 0;
            default:
                throw new Exception("Invalid Value Type");
        }
    }
}

public class Command
{
    public string GroupLabel { get; set; }

    public byte ReadId { get; set; }
    public byte WriteId { get; set; }

    public bool IsReadonly { get; set; }

    public CommandValue Value1 { get; set; }
  
    public CommandValue Value2 { get; set; }

    public CommandValue Value3 { get; set; }

    public Command()
    {
        
    }

    public Command(byte writeId, byte readId, CommandValue value1, CommandValue value2, CommandValue value3, bool isReadonly, string groupLabel = "")
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        IsReadonly = isReadonly;
        WriteId = writeId;
        ReadId = readId;
        GroupLabel = groupLabel;
    }

    public byte[] ToByteArray(bool write)
    {    
        var (val1Msb, val1Lsb) = UShortToBytes(Value1?.ToUShort());
        var (val2Msb, val2Lsb) = UShortToBytes(Value2?.ToUShort());
        var (val3Msb, val3Lsb) = UShortToBytes(Value3?.ToUShort());

        var bytes = new byte[8];
        bytes[0] = write ? WriteId : ReadId;
        bytes[1] = val1Msb;
        bytes[2] = val1Lsb;
        bytes[3] = val2Msb;
        bytes[4] = val2Lsb;
        bytes[5] = val3Msb;
        bytes[6] = val3Lsb;
        bytes[7] = (byte)(bytes[0] ^ bytes[1] ^ bytes[2] ^ bytes[3] ^ bytes[4] ^ bytes[5] ^ bytes[6]);

        return bytes;
    }

    private (byte msb, byte lsb) UShortToBytes(ushort? value) {
        if(value == null)
            return (0, 0);

        var bytes = BitConverter.GetBytes((ushort)value);
        return (bytes[1], bytes[0]);
    }
}

public class Config
{
    public IEnumerable<Command> Commands { get; set; }
}