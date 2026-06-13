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

  // Installs the newer release in place and restarts the backend. Resolves to true when the
  // update started (the backend will relaunch and exit), false when it isn't possible — the
  // caller then falls back to a manual browser download.
  public async installUpdate() {
    return await this.connection.invoke<boolean>("InstallUpdate");
  }

  // Progress of the in-place self-update pushed by the backend (downloading / restarting / failed).
  public onUpdateInstallState(callback: (state: string, message: string) => void) {
    this.connection.on('updateInstallState', callback);
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
