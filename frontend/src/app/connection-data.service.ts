import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { combineLatest, distinctUntilChanged, from, Observable, pairwise, scan, Subject, throttleTime } from 'rxjs';
import { environment } from 'src/environments/environment';


@Injectable({
  providedIn: 'root'
})
export class ConnectionDataService {
  connection!: HubConnection;

  constructor() {
  }

  public async connect(port: string, baudRate: number) {
    return await this.connection.invoke<boolean>("Connect", port, baudRate);
  }

  public async disconnect() {
    await this.connection.invoke("Disconnect");
  }

  public async getPorts() {
    return await this.connection.invoke<string[]>("GetPorts");
  }

  public async getBaudRate() {
    return await this.connection.invoke<number>("GetBaudRate");
  }

  public async getIsConnected() {
    return await this.connection.invoke<boolean>("GetIsConnected");
  }

  public async fakeConnectionConfirmation() {
    // Here we actually open a new Hub connection, so we don't get blocked by existing pending calls
    const tempConnection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/connection`).build();
    await tempConnection.start();
    await tempConnection.invoke("FakeConnectionConfirmation");
    await tempConnection.stop();
  }

  public async cancelConnectionProcess() {
    // Here we actually open a new Hub connection, so we don't get blocked by existing pending calls
    const tempConnection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/connection`).build();
    await tempConnection.start();
    await tempConnection.invoke("CancelConnectionProcess");
    await tempConnection.stop();
  }

  public async init() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/connection`).build();
    await this.connection.start();
  }

  public async destroy() {
    await this.connection.stop();
  }
}

