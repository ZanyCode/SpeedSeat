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
 
  public async connect(port: string) {
    await this.connection.invoke("Connect", port, 9600);
  }

  public async disconnect() {
    await this.connection.invoke("Disconnect");
  }

  public async getPorts() {
    return await this.connection.invoke<string[]>("GetPorts");
  }

  public async getIsConnected() {
    return await this.connection.invoke<boolean>("GetIsConnected");
  }

  public async init() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/connection`).build();   
    await this.connection.start();
  }

  public async destroy() {
    await this.connection.stop();
  }
}

