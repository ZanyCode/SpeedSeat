import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { combineLatest, distinctUntilChanged, from, Observable, pairwise, scan, Subject, throttleTime } from 'rxjs';
import { environment } from 'src/environments/environment';
import { SpeedseatSettings } from '../models/speedseat-settings';
import { Command } from '../models/command';

@Injectable({
  providedIn: 'root'
})
export class SeatSettingsDataService {
  connection!: HubConnection;

  public async init() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}hub/seatSettings`).build();   
    await this.connection.start();
  }

  public async getCommands() {
    return this.connection.invoke<Command[]>("GetCommands");
  }

  public async destroy() {
    await this.connection.stop();
  }
}

