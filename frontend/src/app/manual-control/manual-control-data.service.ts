import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { combineLatest, from, Subject } from 'rxjs';
import { environment } from 'src/environments/environment';


@Injectable({
  providedIn: 'root'
})
export class ManualControlDataService {
  connection: HubConnection;
  private motorValueSubject = new Subject<[number, number]>();   

  constructor() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}manual`).build();
    const connectionSuccess$ = from(this.connection.start());
    combineLatest([connectionSuccess$, this.motorValueSubject]).subscribe(([_, [motorIdx, value]]) => this.connection.invoke("UpdateMotorValue", motorIdx, value));
  }

  public updateMotorValue(motorIdx: number, value: number | null) {
    this.motorValueSubject.next([motorIdx, value as number]);
  }

  public async destroy() {
    await this.connection.stop();
  }
}

