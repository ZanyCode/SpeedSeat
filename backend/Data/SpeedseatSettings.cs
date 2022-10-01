using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

public class SpeedseatSettings
{
    /* Definition of Motor Indexes */
    [JsonIgnore]
    public IObservable<int> FrontLeftMotorIdxObs => GetObservable<int>(nameof(FrontLeftMotorIdx), FrontLeftMotorIdx);
    public int FrontLeftMotorIdx { get => GetValue<int>(0); set => SetValue(value); }

    [JsonIgnore]
    public IObservable<int> FrontRightMotorIdxObs => GetObservable<int>(nameof(FrontRightMotorIdx), FrontRightMotorIdx);
    public int FrontRightMotorIdx { get => GetValue<int>(1); set => SetValue(value); }

    [JsonIgnore]
    public IObservable<int> BackMotorIdxObs => GetObservable<int>(nameof(BackMotorIdx), BackMotorIdx);
    public int BackMotorIdx { get => GetValue<int>(2); set => SetValue(value); }

    /* Definition of Motor Response Curves */
    [JsonIgnore]
    public IObservable<IEnumerable<ResponseCurvePoint>> BackMotorResponseCurveObs => GetObservable<IEnumerable<ResponseCurvePoint>>(nameof(BackMotorResponseCurve), BackMotorResponseCurve);
    public IEnumerable<ResponseCurvePoint> BackMotorResponseCurve
    {
        get => GetValue<IEnumerable<ResponseCurvePoint>>(Enumerable.Range(0, 11).Select(x => new ResponseCurvePoint { Output = x / 10.0, Input = x / 10.0 }));
        set => SetValue(value);
    }

    [JsonIgnore]
    public IObservable<IEnumerable<ResponseCurvePoint>> SideMotorResponseCurveObs => GetObservable<IEnumerable<ResponseCurvePoint>>(nameof(SideMotorResponseCurve), SideMotorResponseCurve);
    public IEnumerable<ResponseCurvePoint> SideMotorResponseCurve
    {
        get => GetValue<IEnumerable<ResponseCurvePoint>>(Enumerable.Range(0, 11).Select(x => new ResponseCurvePoint { Output = x / 10.0, Input = x / 10.0 }));
        set => SetValue(value);
    }


    /* Tilt Axis Priority */
    [JsonIgnore]
    public IObservable<double> FrontTiltPriorityObs => GetObservable<double>(nameof(FrontTiltPriority), FrontTiltPriority);
    public double FrontTiltPriority { get => GetValue<double>(1); set => SetValue(value); }


    /* Definition of Telemetry Stream Settings */
    [JsonIgnore]
    public IObservable<double> FrontTiltGforceMultiplierObs => GetObservable<double>(nameof(FrontTiltGforceMultiplier), FrontTiltGforceMultiplier);
    public double FrontTiltGforceMultiplier { get => GetValue<double>(0.3); set => SetValue(value); }

    [JsonIgnore]
    public IObservable<double> FrontTiltOutputCapObs => GetObservable<double>(nameof(FrontTiltOutputCap), FrontTiltOutputCap);
    public double FrontTiltOutputCap { get => GetValue<double>(1.0); set => SetValue(value); }

    [JsonIgnore]
    public IObservable<double> FrontTiltSmoothingObs => GetObservable<double>(nameof(FrontTiltSmoothing), FrontTiltSmoothing);
    public double FrontTiltSmoothing { get => GetValue<double>(1.0); set => SetValue(value); }

    [JsonIgnore]
    public IObservable<double> SideTiltGforceMultiplierObs => GetObservable<double>(nameof(SideTiltGforceMultiplier), SideTiltGforceMultiplier);
    public double SideTiltGforceMultiplier { get => GetValue<double>(0.3); set => SetValue(value); }

    [JsonIgnore]
    public IObservable<double> SideTiltOutputCapObs => GetObservable<double>(nameof(SideTiltOutputCap), SideTiltOutputCap);
    public double SideTiltOutputCap { get => GetValue<double>(1.0); set => SetValue(value); }

    [JsonIgnore]
    public IObservable<double> SideTiltSmoothingObs => GetObservable<double>(nameof(SideTiltSmoothing), SideTiltSmoothing);
    public double SideTiltSmoothing { get => GetValue<double>(1.0); set => SetValue(value); }

    [JsonIgnore]
    public IObservable<bool> FrontTiltReverseObs => GetObservable<bool>(nameof(FrontTiltReverse), FrontTiltReverse);
    public bool FrontTiltReverse { get => GetValue<bool>(false); set => SetValue(value); }

    [JsonIgnore]
    public IObservable<bool> SideTiltReverseObs => GetObservable<bool>(nameof(SideTiltReverse), SideTiltReverse);
    public bool SideTiltReverse { get => GetValue<bool>(false); set => SetValue(value); }


    /* Definition of Misc Settings */
    public int BaudRate { get => GetValue<int>(9600); set => SetValue(value); }

    /* Configurable settings */
    public (double value1, double value2, double value3) GetConfigurableSettingsValues(Command command)
    {
        using (var scope = scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SpeedseatContext>();
            var value1 = JsonSerializer.Deserialize<double>(context.Get(ConstructSettingsValueId(command, 1)) ?? JsonSerializer.Serialize(command.Value1.Default));
            var value2 = JsonSerializer.Deserialize<double>(context.Get(ConstructSettingsValueId(command, 2)) ?? JsonSerializer.Serialize(command.Value2.Default));
            var value3 = JsonSerializer.Deserialize<double>(context.Get(ConstructSettingsValueId(command, 3)) ?? JsonSerializer.Serialize(command.Value3.Default));
            return (value1, value2, value3);
        }
    }

    public void SaveConfigurableSetting(Command command)
    {
         using (var scope = scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SpeedseatContext>();
            var value1Id = ConstructSettingsValueId(command, 1);
            var value2Id = ConstructSettingsValueId(command, 2);
            var value3Id = ConstructSettingsValueId(command, 3);

            var value1 = JsonSerializer.Serialize(command.Value1.Value);
            var value2 = JsonSerializer.Serialize(command.Value2.Value);
            var value3 = JsonSerializer.Serialize(command.Value3.Value);

            context.Set(value1Id, value1);
            context.Set(value2Id, value2);
            context.Set(value3Id, value3);            
        }
    }

    private string ConstructSettingsValueId(Command command, int valueNr)
    {
        return $"{command.ReadId}-Value{valueNr}";
    }    

    /* Implementation of boilerplate */
    private ConcurrentDictionary<string, ISubject<string?>> subjects = new ConcurrentDictionary<string, ISubject<string?>>();
    private readonly IServiceScopeFactory scopeFactory;

    public SpeedseatSettings(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;
    }

    private ISubject<string?> GetSubject<T>(string id = "", T defaultValue = default(T))
    {
        return subjects.GetOrAdd(id, id =>
        {
            using (var outerScope = scopeFactory.CreateScope())
            {
                var outerContext = outerScope.ServiceProvider.GetRequiredService<SpeedseatContext>();
                var subject = Subject.Synchronize<string?>(new BehaviorSubject<string?>(outerContext.Get(id) ?? JsonSerializer.Serialize<T>(defaultValue)));

                subject.Where(x => x != null).Subscribe(x =>
                {
                    using (var scope = scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<SpeedseatContext>();
                        context.Set(id, x);
                    }
                });

                return subject;
            }
        });
    }

    private IObservable<T> GetObservable<T>(string id, T startValue)
    {
        return GetSubject(id, startValue)
        .Select(x => x == null ? default(T) : JsonSerializer.Deserialize<T>(x));
    }

    private void SetValue<T>(T value, [CallerMemberName] string id = "")
    {
        var stringValue = JsonSerializer.Serialize<T>(value);
        GetSubject<T>(id)?.OnNext(stringValue);
    }

    private T GetValue<T>(T defaultValue = default(T), [CallerMemberName] string id = "")
    {
        using (var scope = scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SpeedseatContext>();
            var strVal = context.Get(id);
            return strVal == null ? defaultValue : JsonSerializer.Deserialize<T>(strVal);
        }
    }
}

public class ResponseCurvePoint
{
    public double Output { get; set; }
    public double Input { get; set; }
}