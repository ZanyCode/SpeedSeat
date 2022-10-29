import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Observable } from 'rxjs';
import { map, shareReplay } from 'rxjs/operators';
import { AppDataService } from './app-data.service';
import { ConnectionDataService } from './connection-data.service';
import { AppEventsService } from './app-events.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'SpeedSeat';
  url: string | undefined = undefined;
  log = '[LOG]';
  logTextareaScrolltop: number | null = null;
  @ViewChild('textarea') textarea: ElementRef | undefined;

  // Connection properties
  ports: string[] = [];
  selectedPort: string | undefined = undefined;
  selectedBaudRate: number = 9600;
  isConnected = false;
  isConnecting = false;


  isHandset$: Observable<boolean> = this.breakpointObserver.observe(Breakpoints.Handset)
    .pipe(
      map(result => result.matches),
      shareReplay()
    );

  constructor(private breakpointObserver: BreakpointObserver, private data: AppDataService, private connectionService: ConnectionDataService, private events: AppEventsService) { }


  ngOnInit(): void {
    this.data.init(this.onLogReceived).then(async () => {
      this.url = await this.data.GetOwnUrl();
    });

    this.connectionService.init().then(async () => {
      this.ports = await this.connectionService.getPorts();
      this.selectedPort = this.ports.length > 0 ? this.ports[0] : undefined;
      this.selectedBaudRate = await this.connectionService.getBaudRate();
      this.isConnected = await this.connectionService.getIsConnected();
      if(this.isConnected)
        this.events.signalConnectionStateChanged(true);
    });
  }

  onLogReceived = (message: string) => {
    this.log += `\n[${new Date().toLocaleString()}]: ${message}`;
    setTimeout(() => this.logTextareaScrolltop = this.textarea?.nativeElement.scrollHeight, 0);
  }

  ngOnDestroy(): void {
    this.data.destroy();
    this.connectionService.destroy();
  }

  async connect() {
    if (this.selectedPort) {      
      this.isConnecting = true;
      this.isConnected = await this.connectionService.connect(this.selectedPort, this.selectedBaudRate);;
      this.isConnecting = false;
      if(this.isConnected)
        this.events.signalConnectionStateChanged(true);
    }
  }

  async disconnect() {
    await this.connectionService.disconnect();
    this.isConnected = false;
    this.events.signalConnectionStateChanged(false);
  }

  async refreshPorts() {
    this.ports = await this.connectionService.getPorts();
    this.selectedPort = this.ports.length > 0 ? this.ports[0] : undefined;
  }

  async fakeConnectionConfirmation() {
    await this.connectionService.fakeConnectionConfirmation();
  }

  async cancelConnectionProcess() {
    await this.connectionService.cancelConnectionProcess();
  }
}
