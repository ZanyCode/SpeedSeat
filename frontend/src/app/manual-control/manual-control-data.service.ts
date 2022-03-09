import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { combineLatest, distinctUntilChanged, from, Observable, pairwise, scan, Subject, throttleTime } from 'rxjs';
import { environment } from 'src/environments/environment';


@Injectable({
  providedIn: 'root'
})
export class ManualControlDataService {
  connection!: HubConnection;
  private epsilon = 0.001;

  private motor0ValueSubject = new Subject<number>();   
  public motor0Value$!: Observable<number>;

  private motor1ValueSubject = new Subject<number>();   
  public motor1Value$!: Observable<number>;

  private motor2ValueSubject = new Subject<number>();     
  public motor2Value$!: Observable<number>;

  private frontTiltSubject = new Subject<number>();
  public frontTilt$!: Observable<number>;

  private sideTiltSubject = new Subject<number>();
  public sideTilt$!: Observable<number>;

  constructor() {
  }

  public updateMotorValue(motorIdx: number, value: number | null) {
    const subjects = [this.motor0ValueSubject, this.motor1ValueSubject, this.motor2ValueSubject];
    subjects[motorIdx].next(value as number);
  }
  
  public updateFrontTilt(value: number | null) {
    this.frontTiltSubject.next(value as number);
  }

  public updateSideTilt(value: number | null) {
    this.sideTiltSubject.next(value as number);
  }

  public init() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}manual`).build();
    this.connection.on('motor0position', pos => this.motor0ValueSubject.next(pos));
    this.connection.on('motor1position', pos => this.motor1ValueSubject.next(pos));
    this.connection.on('motor2position', pos => this.motor2ValueSubject.next(pos));
    this.connection.on('sideTilt', pos => this.sideTiltSubject.next(pos));
    this.connection.on('frontTilt', pos => this.frontTiltSubject.next(pos));
    const connectionSuccess$ = from(this.connection.start());

    combineLatest([connectionSuccess$, this.motor0ValueSubject.pipe(distinctWithMargin(this.epsilon))]).subscribe(([_, value]) => this.connection.invoke("UpdateMotorValue", 0, value));
    combineLatest([connectionSuccess$, this.motor1ValueSubject.pipe(distinctWithMargin(this.epsilon))]).subscribe(([_, value]) => this.connection.invoke("UpdateMotorValue", 1, value));
    combineLatest([connectionSuccess$, this.motor2ValueSubject.pipe(distinctWithMargin(this.epsilon))]).subscribe(([_, value]) => this.connection.invoke("UpdateMotorValue", 2, value));
    combineLatest([connectionSuccess$, this.frontTiltSubject.pipe(distinctWithMargin(this.epsilon))]).subscribe(([_, value]) => this.connection.invoke("UpdateFrontTilt", value));
    combineLatest([connectionSuccess$, this.sideTiltSubject.pipe(distinctWithMargin(this.epsilon))]).subscribe(([_, value]) => this.connection.invoke("UpdateSideTilt", value));

    this.motor0Value$ = this.motor0ValueSubject.asObservable();
    this.motor1Value$ = this.motor1ValueSubject.asObservable();
    this.motor2Value$ = this.motor2ValueSubject.asObservable();
    this.sideTilt$ = this.sideTiltSubject.asObservable();
    this.frontTilt$ = this.frontTiltSubject.asObservable();
  }

  public async destroy() {
    await this.connection.stop();
  }
}

// Only fires if two adjacent values are sufficiently distinct from each other. Also throttles the output
function distinctWithMargin(margin: number) {
  return (x: Observable<number>) => {
    var res = x.pipe(
      scan((acc, val) => {
        return Math.abs(acc - val) > margin ? val : acc;
      }, 0),
      distinctUntilChanged(),
      throttleTime(50, undefined, { leading: false, trailing: true  })
    );

    return res;
  }
}

