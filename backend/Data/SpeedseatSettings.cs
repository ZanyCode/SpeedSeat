using System;
using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;

public class SpeedseatSettings {
    /* Definition of reactive settings */
    public IObservable<double> Motor0Position => GetObservable<double>();
    public void SetMotor0Position(double value) => SetValue(nameof(Motor0Position), value);
    public IObservable<double> Motor1Position => GetObservable<double>();
    public void SetMotor1Position(double value) => SetValue(nameof(Motor1Position), value);

    public IObservable<double> Motor2Position => GetObservable<double>();
    public void SetMotor2Position(double value) => SetValue(nameof(Motor2Position), value); 

    public IObservable<double> FrontTilt => GetObservable<double>();
    public void SetFrontTilt(double value) => SetValue(nameof(FrontTilt), value);

    public IObservable<double> SideTilt => GetObservable<double>();
    public void SetSideTilt(double value) => SetValue(nameof(SideTilt), value);


    /* Implementation of technical noise */
    private ConcurrentDictionary<string, ISubject<string?>> subjects = new ConcurrentDictionary<string, ISubject<string?>>();
    private readonly IServiceScopeFactory scopeFactory;

    public SpeedseatSettings(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;
    }

    private ISubject<string?> GetSubject(string id = "") {
        return subjects.GetOrAdd(id, id => {        
            using(var outerScope = scopeFactory.CreateScope())
            {
                var outerContext = outerScope.ServiceProvider.GetRequiredService<SpeedseatContext>();
                var subject = Subject.Synchronize<string?>(new BehaviorSubject<string?>(outerContext.Get(id)));

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

    private IObservable<T> GetObservable<T>([CallerMemberName] string id = "") {
        return GetSubject(id).Select(x => x == null? default(T) : (T)Convert.ChangeType(x, typeof(T)));
    }

    private void SetValue(string id, object value) {
        GetSubject(id).OnNext(value.ToString());
    }    
}