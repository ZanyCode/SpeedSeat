import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { environment } from 'src/environments/environment';


@Injectable({
  providedIn: 'root'
})
export class ConnectionDataService {
  connection!: HubConnection;

  constructor() {
  }

  public async getIsConnected() {
    return await this.connection.invoke<boolean>("GetIsConnected");
  }

  public async init() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/connection`).build();
    await this.connection.start();
  }

  // The backend binds to the seat on its own and pushes every connect/disconnect here.
  public onConnectionStateChanged(callback: (isConnected: boolean) => void) {
    this.connection.on('connectionStateChanged', callback);
  }

  // Firmware version handshake / OTA progress pushed by the backend after every connect.
  public onFirmwareUpdateState(callback: (state: string, message: string) => void) {
    this.connection.on('firmwareUpdateState', callback);
  }

  public async getFirmwareUpdateState() {
    return await this.connection.invoke<{ state: string, message: string }>("GetFirmwareUpdateState");
  }

  public async destroy() {
    await this.connection.stop();
  }
}
