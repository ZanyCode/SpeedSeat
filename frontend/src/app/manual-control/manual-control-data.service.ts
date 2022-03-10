import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { combineLatest, distinctUntilChanged, filter, from, Observable, pairwise, scan, Subject, throttleTime } from 'rxjs';
import { environment } from 'src/environments/environment';

export interface Speedseat {
  frontLeftMotorPosition: number;
  frontRightMotorPosition: number;
  backMotorPosition: number;
  isConnected: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ManualControlDataService {
  connection!: HubConnection;
  isConnected: boolean = false;

  public async init() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}manual`).build();
    await this.connection.start().then(() => this.isConnected = true);   
  }

  public async setFrontLeftMotorPosition(position: number) {
    await this.connection.invoke("SetFrontLeftMotorPosition", position);
  }

  public async setFrontRightMotorPosition(position: number) {
    await this.connection.invoke("SetFrontRightMotorPosition", position);
  }

  public async setBackMotorPosition(position: number) {
    await this.connection.invoke("SetBackMotorPosition", position);
  }

  public async getCurrentState() {
    return await this.connection.invoke<Speedseat>("GetCurrentState");
  }

  public async destroy() {
    await this.connection.stop();
  }
}

