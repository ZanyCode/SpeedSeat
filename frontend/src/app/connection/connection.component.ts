import { Component, OnDestroy, OnInit } from '@angular/core';
import { ConnectionDataService } from './connection-data.service';

@Component({
  selector: 'app-connection',
  templateUrl: './connection.component.html',
  styleUrls: ['./connection.component.scss']
})
export class ConnectionComponent implements OnInit, OnDestroy {
  ports: string[] = [];
  selectedPort: string | undefined = undefined;
  selectedBaudRate: number = 9600;
  isConnected = false;

  constructor(public data: ConnectionDataService) { }

  async connect() {
    if(this.selectedPort){
      await this.data.connect(this.selectedPort, this.selectedBaudRate);
      this.isConnected = true;    
    }
  }

  async disconnect() {
    await this.data.disconnect();
    this.isConnected = false;
  }

  async refreshPorts() {
    this.ports = await this.data.getPorts();
    this.selectedPort = this.ports.length > 0? this.ports[0] : undefined;
  }

  ngOnInit(): void {
    this.data.init().then(async () => {
      this.ports = await this.data.getPorts();
      this.selectedPort = this.ports.length > 0? this.ports[0] : undefined;
      this.selectedBaudRate = await this.data.getBaudRate();
      this.isConnected = await this.data.getIsConnected();
    });
  }

  ngOnDestroy(): void {
    this.data.destroy();
  }
}
