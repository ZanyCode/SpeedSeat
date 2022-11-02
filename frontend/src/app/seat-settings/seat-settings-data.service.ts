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
  private settingChangedSubject = new Subject<Command>();
  public $settingChanged: Observable<Command>;

  constructor() {
    this.$settingChanged = this.settingChangedSubject.asObservable();
  }

  public async init() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/seatSettings`).build();
    this.connection.on("SettingChanged", command => this.SettingChanged(command));
    await this.connection.start();
  }

  public async getCommands() {
    return this.connection.invoke<Command[]>("GetCommands");
  }

  public async updateSetting(updateCommand: Command) {
    return this.connection.invoke("UpdateSetting", updateCommand);
  }

  public SettingChanged(command: Command)
  {
    console.log(command);
    this.settingChangedSubject.next(command);
  }

  fakeWriteRequest(command: Command) {
    return this.connection.invoke("FakeWriteRequest", command);
  }

  public async destroy() {
    this.connection.off("SettingChanged");
    await this.connection.stop();
  }
}

