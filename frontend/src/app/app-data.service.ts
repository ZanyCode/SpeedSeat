import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';
import { environment } from 'src/environments/environment';

export interface UpdateInfo {
  currentVersion: string;
  latestVersion: string | null;
  updateAvailable: boolean;
  downloadUrl: string | null;
  error: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class AppDataService {
  connection!: HubConnection;
  isConnected: boolean = false;

  public async init() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/info`).build();
    await this.connection.start().then(() => this.isConnected = true);
  }

  public async GetOwnUrl() {
    return await this.connection.invoke<string>("GetOwnUrl");
  }

  public async getConfigValidityErrors() {
    return await this.connection.invoke<string>("GetConfigValidityErrors");
  }

  public async getUpdateInfo() {
    return await this.connection.invoke<UpdateInfo>("GetUpdateInfo");
  }

  public subscribeToLogs(): Observable<string> {
    let subject = new Subject<string>();
    this.connection.stream("LogMessages").subscribe(subject);
    return subject.asObservable();
  }

  public async destroy() {
    this.connection.off("LogMessages");
    await this.connection.stop();
  }
}
