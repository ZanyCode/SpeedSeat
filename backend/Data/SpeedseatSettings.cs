using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

public class SpeedseatSettings {
    /* Definition of reactive settings */
    [JsonIgnore]
    public IObservable<int> FrontLeftMotorIdxObs => GetObservable<int>(nameof(FrontLeftMotorIdx), FrontLeftMotorIdx);
    public int FrontLeftMotorIdx { get => GetValue<int>(0); set => SetValue(value.ToString()); }

    [JsonIgnore]
    public IObservable<int> FrontRightMotorIdxObs => GetObservable<int>(nameof(FrontRightMotorIdx), FrontRightMotorIdx);
    public int FrontRightMotorIdx { get => GetValue<int>(1); set => SetValue(value.ToString()); }

    [JsonIgnore]
    public IObservable<int> BackMotorIdxObs => GetObservable<int>(nameof(BackMotorIdx), BackMotorIdx);
    public int BackMotorIdx { get => GetValue<int>(2); set => SetValue(value.ToString()); }

    public int BaudRate {get => GetValue<int>(9600); set => SetValue(value.ToString()); }
  

    /* Implementation of technical noise */
    private ConcurrentDictionary<string, ISubject<string?>> subjects = new ConcurrentDictionary<string, ISubject<string?>>();
    private readonly IServiceScopeFactory scopeFactory;

    public SpeedseatSettings(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;
    }

    private ISubject<string?> GetSubject(string id = "", object defaultValue = null) {
        return subjects.GetOrAdd(id, id => {        
            using(var outerScope = scopeFactory.CreateScope())
            {
                var outerContext = outerScope.ServiceProvider.GetRequiredService<SpeedseatContext>();
                var subject = Subject.Synchronize<string?>(new BehaviorSubject<string?>(outerContext.Get(id) ?? defaultValue?.ToString()));

                subject.Where(x => x != null).Subscribe(x => {
                    using(var scope = scopeFactory.CreateScope()) {
                        var context = scope.ServiceProvider.GetRequiredService<SpeedseatContext>();
                        context.Set(id, x);                   
                    }
                });

                return subject;
            }           
        });
    }

    private IObservable<T> GetObservable<T>(string id, T startValue) {
        return GetSubject(id, startValue)
        .Select(x => x == null? default(T) : (T)Convert.ChangeType(x, typeof(T)));
    }

    private void SetValue(object value, [CallerMemberName]string id = "") {
        GetSubject(id)?.OnNext(value.ToString());
    }   

    private T GetValue<T>(T defaultValue = default(T), [CallerMemberName]string id = "") {
        using(var scope = scopeFactory.CreateScope()) {
            var context = scope.ServiceProvider.GetRequiredService<SpeedseatContext>();
            var strVal = context.Get(id);     
            return strVal == null? defaultValue : (T)Convert.ChangeType(strVal, typeof(T)); 
        }    
    }   
}