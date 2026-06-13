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

  // Whether this backend build can flash a USB-connected seat (packaged release builds only).
  public async getCanFlashViaUsb() {
    return await this.connection.invoke<boolean>("GetCanFlashViaUsb");
  }

  // Flashes the bundled firmware to a USB-connected seat (first-time setup / recovery).
  // Returns immediately; progress arrives via onUsbFlashState.
  public async flashViaUsb() {
    await this.connection.invoke("FlashViaUsb");
  }

  // Progress of a USB flash pushed by the backend (flashing / success / failed).
  public onUsbFlashState(callback: (state: string, message: string) => void) {
    this.connection.on('usbFlashState', callback);
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
