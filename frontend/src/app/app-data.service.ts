import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AppDataService {
  connection!: HubConnection;
  isConnected: boolean = false;
  
  public async init(logCallback: (message: string) => void) {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/info`).build();
    await this.connection.start().then(() => this.isConnected = true);   
    this.connection.on('log', logCallback);
  }

  public async GetOwnUrl() {
    return await this.connection.invoke<string>("GetOwnUrl");
  }
  
  public async destroy() {
    await this.connection.stop();
  }
}
