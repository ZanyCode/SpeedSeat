import { Injectable } from '@angular/core';
import { Data } from '@angular/router';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { combineLatest, distinctUntilChanged, filter, from, Observable, pairwise, scan, Subject, throttleTime } from 'rxjs';
import { environment } from 'src/environments/environment';
import { SpeedseatSettings } from '../models/speedseat-settings';

export interface Speedseat {
  frontLeftMotorPosition: number;
  frontRightMotorPosition: number;
  backMotorPosition: number;
  isConnected: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class TelemetryDataService {
  connection!: HubConnection;
  isConnected: boolean = false;

  public async init(onUpdateTelemetry: (frontTiltTelemetry: DataPoint[], sideTiltTelemetry: DataPoint[]) => void) {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/telemetry`).build();
    this.connection.on('updateTelemetry', onUpdateTelemetry);
    await this.connection.start().then(() => this.isConnected = true);   
  }

  public async setFrontTiltGForceMultiplier(multiplier: number) {
    await this.connection.invoke("SetFrontTiltGForceMultiplier", multiplier);
  }

  public async setFrontTiltOutputCap(cap: number) {
    await this.connection.invoke("SetFrontTiltOutputCap", cap);
  }

  public async setFrontTiltSmoothing(smoothing: number) {
    await this.connection.invoke("SetFrontTiltSmoothing", smoothing);
  }

  public async setSideTiltGForceMultiplier(multiplier: number) {
    await this.connection.invoke("SetSideTiltGForceMultiplier", multiplier);
  }

  public async setSideTiltOutputCap(cap: number) {
    await this.connection.invoke("SetSideTiltOutputCap", cap);
  }

  public async setSideTiltSmoothing(smoothing: number) {
    await this.connection.invoke("SetSideTiltSmoothing", smoothing);
  }

  public async setFrontTiltReverse(reverse: boolean) {
    await this.connection.invoke("SetFrontTiltReverse", reverse);
  }

  public async setSideTiltReverse(reverse: boolean) {
    await this.connection.invoke("SetSideTiltReverse", reverse);
  }

  public async startStreaming() {
    await this.connection.invoke("StartStreaming");
  }

  public async stopStreaming() {
    await this.connection.invoke("StopStreaming");
  }

  public async getCurrentState() {
    return await this.connection.invoke<SpeedseatSettings>("GetCurrentState");
  }

  public async getIsStreaming() {
    return await this.connection.invoke<boolean>("GetIsStreaming");
  }

  public async destroy() {
    await this.connection.stop();
  }
}

export interface DataPoint {
  x: Date;
  y: number;
}

