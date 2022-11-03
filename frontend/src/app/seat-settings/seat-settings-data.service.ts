import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { BehaviorSubject, combineLatest, distinctUntilChanged, from, Observable, pairwise, scan, Subject, throttleTime } from 'rxjs';
import { environment } from 'src/environments/environment';
import { SpeedseatSettings } from '../models/speedseat-settings';
import { Command } from '../models/command';

@Injectable({
  providedIn: 'root'
})
export class SeatSettingsDataService {

  connection!: HubConnection;

  public async init() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/seatSettings`).build();
    await this.connection.start();
  }

  public async getCommands() {
    return this.connection.invoke<Command[]>("GetCommands");
  }

  public async updateSetting(updateCommand: Command) {
    return this.connection.invoke("UpdateSetting", updateCommand);
  }

  public subscribeToConfigurableSetting(command: Command): Observable<Command> {
    let subject = new Subject<Command>();
    this.connection.stream("SubscribeToConfigurableSetting", command).subscribe(subject);
    return subject.asObservable();
  }

  fakeWriteRequest(command: Command) {
    return this.connection.invoke("FakeWriteRequest", command);
  }

  public async destroy() {
    this.connection.off("SettingChanged");
    await this.connection.stop();
  }
}

