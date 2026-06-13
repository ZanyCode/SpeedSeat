import { ChangeDetectorRef, Component, ElementRef, Inject, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Observable, Subscription } from 'rxjs';
import { map, shareReplay } from 'rxjs/operators';
import { AppDataService, UpdateInfo } from './app-data.service';
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

  // The backend discovers the seat over WiFi and (re)connects on its own; we only reflect
  // the live connection state it pushes to us.
  isConnected = false;

  // Backend update check (GitHub releases)
  updateInfo: UpdateInfo | undefined = undefined;

  // Firmware version handshake / OTA status
  firmwareState: string | undefined = undefined;
  firmwareMessage: string | undefined = undefined;


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

        this.data.getUpdateInfo().then(info => this.updateInfo = info);

        this.connectionService.init().then(async () => {
          this.connectionService.onConnectionStateChanged(isConnected => this.onConnectionStateChanged(isConnected));
          this.connectionService.onFirmwareUpdateState((state, message) => this.onFirmwareUpdateState(state, message));

          // Pick up whatever the backend is doing right now (it may already be connected
          // or mid firmware update when this page (re)loads).
          this.onConnectionStateChanged(await this.connectionService.getIsConnected());
          const firmware = await this.connectionService.getFirmwareUpdateState();
          if (firmware?.state && firmware.state !== 'unknown')
            this.onFirmwareUpdateState(firmware.state, firmware.message);
        });
      }
    });
  }

  downloadUpdate() {
    if (this.updateInfo?.downloadUrl)
      window.open(this.updateInfo.downloadUrl, '_blank');
  }

  onConnectionStateChanged(isConnected: boolean) {
    this.isConnected = isConnected;
    this.events.signalConnectionStateChanged(isConnected);

    // A successful (re)connect ends any visible OTA update flow.
    if (isConnected && this.firmwareState === 'updating') {
      this.firmwareState = undefined;
      this.firmwareMessage = undefined;
    }
  }

  onFirmwareUpdateState(state: string, message: string) {
    this.firmwareState = state;
    this.firmwareMessage = message;

    if (state === 'updating') {
      // The seat flashes the new firmware and restarts — reflect the lost connection.
      // The backend keeps rediscovering and reconnects automatically afterwards, which
      // clears this state again (see onConnectionStateChanged).
      this.isConnected = false;
      this.events.signalConnectionStateChanged(false);
    }

    if (state === 'upToDate') {
      // Everything is fine — don't keep the banner around forever.
      setTimeout(() => {
        if (this.firmwareState === 'upToDate') {
          this.firmwareState = undefined;
          this.firmwareMessage = undefined;
        }
      }, 10000);
    }
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

  clearLog() {
    this.logMessages = [{ id: 0, msg: '[LOG]' }];
    this.currentLogMessageId = 1;
  }

  identifyLogItem(_index: number, item: { id: number, msg: string }) {
    return item.id;
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
