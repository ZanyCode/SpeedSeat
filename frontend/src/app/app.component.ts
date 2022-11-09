import { ChangeDetectorRef, Component, ElementRef, Inject, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Observable, Subscription } from 'rxjs';
import { map, shareReplay } from 'rxjs/operators';
import { AppDataService } from './app-data.service';
import { ConnectionDataService } from './connection-data.service';
import { AppEventsService } from './app-events.service';
import { MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';

export interface YesNoDialogData {
  title: string;
  content: string;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'SpeedSeat';
  url: string | undefined = undefined;
  logMessages = [{ id: 0, msg: '[LOG]' }];
  fatalErrorText: string | undefined = undefined;
  currentLogMessageId = 1;
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
  logSubscription: Subscription | undefined;

  constructor(private breakpointObserver: BreakpointObserver, private data: AppDataService, private connectionService: ConnectionDataService, private events: AppEventsService, private changeDetection: ChangeDetectorRef, public dialog: MatDialog) { }


  ngOnInit(): void {
    this.data.init().then(async () => {
      this.fatalErrorText = await this.data.getConfigValidityErrors();
      if (!this.fatalErrorText) {
        this.url = await this.data.GetOwnUrl();
        this.logSubscription = this.data.subscribeToLogs().subscribe(msg => {
          this.onLogReceived(msg);
        });

        this.connectionService.init().then(async () => {
          this.ports = await this.connectionService.getPorts();
          this.selectedPort = this.ports.length > 0 ? this.ports[0] : undefined;
          this.selectedBaudRate = await this.connectionService.getBaudRate();
          this.isConnected = await this.connectionService.getIsConnected();
          if (this.isConnected)
            this.events.signalConnectionStateChanged(true);
          else if (this.selectedPort)
            await this.connect();
        });
      }
    });
  }

  onLogReceived = (message: string) => {
    if (this.logMessages.length > 1000) {
      this.logMessages = [{ id: 0, msg: `[${new Date().toLocaleString()}]: Log buffer cleared to prevent overflow. Old messages discarded.` }];
      this.currentLogMessageId = 1;
    }

    const logMessage = `[${new Date().toLocaleString()}]: ${message}`;
    this.logMessages = [{ id: this.currentLogMessageId, msg: logMessage }, ...this.logMessages];
    this.currentLogMessageId++;
  }

  ngOnDestroy(): void {
    this.logSubscription?.unsubscribe();
    this.data.destroy();
    this.connectionService.destroy();
  }

  async connect() {
    if (this.selectedPort) {
      this.isConnecting = true;
      this.isConnected = await this.connectionService.connect(this.selectedPort, this.selectedBaudRate);;
      this.isConnecting = false;
      if (this.isConnected)
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

  clearLog() {
    this.logMessages = [{ id: 0, msg: '[LOG]' }];
    this.currentLogMessageId = 1;
  }

  identifyLogItem(_index: number, item: { id: number, msg: string }) {
    return item.id;
  }

  resetEEPROM() {
    const dialogRef = this.dialog.open(YesNoDialogComponent, {
      width: '250px',
      data: { title: "Delete EEPROM", content: "Do you really want to delete stored EEPROM-values?" },
    });

    dialogRef.afterClosed().subscribe(async result => {
      if (result && this.selectedPort) {
        await this.disconnect();
        await this.connectionService.deleteEEPROM(this.selectedPort, this.selectedBaudRate);
      }
    });
  }
}

@Component({
  selector: 'yes-no-dialog',
  templateUrl: './yes-no-dialog.html',
})
export class YesNoDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<YesNoDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: YesNoDialogData,
  ) { }

  onNoClick(): void {
    this.dialogRef.close();
  }
}
