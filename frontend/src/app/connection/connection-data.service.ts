import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { combineLatest, distinctUntilChanged, from, Observable, pairwise, scan, Subject, throttleTime } from 'rxjs';
import { environment } from 'src/environments/environment';


@Injectable({
  providedIn: 'root'
})
export class ConnectionDataService {
  connection!: HubConnection;

  private serialPortsSubject = new Subject<string[]>();   
  public serialPorts$!: Observable<string[]>;  

  constructor() {
  }
 
  public connect() {
    this.connection.invoke("Connect", "COM3");
  }

  public init() {
    this.connection = new HubConnectionBuilder().withUrl(`${environment.backendUrl}connection`).build();
    this.connection.on('serialPorts', ports => {
      this.serialPortsSubject.next(ports);
    });  
    this.connection.start();
    this.serialPorts$ = this.serialPortsSubject.asObservable();   
  }

  public async destroy() {
    await this.connection.stop();
  }
}

