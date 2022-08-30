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
    public bool Readonly { get; set; }
    public bool ScaleToFullRange{ get; set; }
    public double Min { get; set; }
    public double Max { get; set; }    
    public double Value { get; set; }

    public CommandValue(ValueType type, double value, string? label="", bool @readonly=false, bool scaleToFullRange = true, double min = 0, double max = 1)
    {
        Label = label;
        Type = type;
        Readonly = @readonly;
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
            default:
                throw new Exception("Invalid Value Type");
        }
    }
}

public class Command
{
    public string GroupLabel { get; set; }

    public byte Id { get; set; }

    public CommandValue Value1 { get; set; }
  
    public CommandValue Value2 { get; set; }

    public CommandValue Value3 { get; set; }

    public Command(byte id, CommandValue value1, CommandValue value2, CommandValue value3, string groupLabel = "")
    {
        Value1 = value1;
        Value2 = value2;
        Value3 = value3;
        Id = id;
        GroupLabel = groupLabel;
    }


    public byte[] ToByteArray()
    {
        var (val1Msb, val1Lsb) = UShortToBytes(Value1.ToUShort());
        var (val2Msb, val2Lsb) = UShortToBytes(Value2.ToUShort());
        var (val3Msb, val3Lsb) = UShortToBytes(Value3.ToUShort());

        var bytes = new byte[7];
        bytes[0] = 0;
        bytes[1] = val1Msb;
        bytes[2] = val1Lsb;
        bytes[3] = val2Msb;
        bytes[4] = val2Lsb;
        bytes[5] = val3Msb;
        bytes[6] = val3Lsb;

        return bytes;
    }

    private (byte msb, byte lsb) UShortToBytes(ushort value) {
        var bytes = BitConverter.GetBytes(value);
        return (bytes[1], bytes[0]);
    }
}