import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { combineLatest, distinctUntilChanged, from, Observable, pairwise, scan, Subject, throttleTime } from 'rxjs';
import { environment } from 'src/environments/environment';
import { ResponseCurvePoint } from '../curve-editor/curve-editor.component';
import { SpeedseatSettings } from '../models/speedseat-settings';

@Injectable({
  providedIn: 'root'
})
export class SettingsDataService {
  connection!: HubConnection;

  public async getSettings() {
    return await this.connection.invoke<SpeedseatSettings>("GetSettings");
  }

  public async setFrontLeftMotorIdx(idx: number) {
    await this.connection.invoke("SetFrontLeftMotorIdx", idx);
  }

  public async setFrontRightMotorIdx(idx: number) {
    await this.connection.invoke("SetFrontRightMotorIdx", idx);
  }

  public async setBackMotorIdx(idx: number) {
    await this.connection.invoke("SetBackMotorIdx", idx);
  }

  public async setFrontTiltPriority(priority: number) {
    await this.connection.invoke("SetFrontTiltPriority", priority);
  }

  public async setBackMotorResponseCurve(curve: ResponseCurvePoint[]) {
    await this.connection.invoke("SetBackMotorResponseCurve", curve);
  }

  public async setSideMotorResponseCurve(curve: ResponseCurvePoint[]) {
    await this.connection.invoke("SetSideMotorResponseCurve", curve);
  }

  public async init() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/settings`).build();   
    await this.connection.start();
  }

  public async destroy() {
    await this.connection.stop();
  }
}

