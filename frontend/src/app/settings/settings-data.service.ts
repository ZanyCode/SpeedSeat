import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { combineLatest, distinctUntilChanged, from, Observable, pairwise, scan, Subject, throttleTime } from 'rxjs';
import { environment } from 'src/environments/environment';

export interface SpeedseatSettings
{
  frontLeftMotorIdx: number;
  frontRightMotorIdx: number;
  backMotorIdx: number;
}

@Injectable({
  providedIn: 'root'
})
export class SettingsDataService {
  connection!: HubConnection;

  public async GetSettings() {
    return await this.connection.invoke<SpeedseatSettings>("GetSettings");
  }

  public async SetFrontLeftMotorIdx(idx: number) {
    await this.connection.invoke("SetFrontLeftMotorIdx", idx);
  }

  public async SetFrontRightMotorIdx(idx: number) {
    await this.connection.invoke("SetFrontRightMotorIdx", idx);
  }

  public async SetBackMotorIdx(idx: number) {
    await this.connection.invoke("SetBackMotorIdx", idx);
  }

  public async init() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/settings`).build();   
    await this.connection.start();
  }

  public async destroy() {
    await this.connection.stop();
  }
}

