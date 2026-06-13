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

  // Backend self-update (in-place install) status
  updateInstalling = false;
  updateInstallState: string | undefined = undefined;
  updateInstallMessage: string | undefined = undefined;
  private updateFallbackDone = false;

  // First-time-setup / USB-flash help, shown after the seat stays disconnected for a while
  showFlashHelp = false;
  canFlashViaUsb = false;
  usbFlashState: string | undefined = undefined;
  usbFlashMessage: string | undefined = undefined;
  usbFlashing = false;
  private flashHelpTimer: any = undefined;
  private static readonly FLASH_HELP_DELAY_MS = 5000;


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
        this.data.onUpdateInstallState((state, message) => this.onUpdateInstallState(state, message));

        this.connectionService.init().then(async () => {
          this.connectionService.onConnectionStateChanged(isConnected => this.onConnectionStateChanged(isConnected));
          this.connectionService.onFirmwareUpdateState((state, message) => this.onFirmwareUpdateState(state, message));
          this.connectionService.onUsbFlashState((state, message) => this.onUsbFlashState(state, message));
          this.canFlashViaUsb = await this.connectionService.getCanFlashViaUsb();

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

  async installUpdate() {
    if (this.updateInstalling)
      return;

    this.updateInstalling = true;
    this.updateFallbackDone = false;
    this.updateInstallState = 'downloading';
    this.updateInstallMessage = 'Starting update…';

    try {
      const started = await this.data.installUpdate();
      if (started)
        this.scheduleReloadAfterRestart();
      else
        this.fallbackToManualDownload();
    } catch {
      // The hub call can reject if the backend already exited mid-restart — that's expected
      // once we've seen the 'restarting' state; otherwise fall back to a manual download.
      if (this.updateInstallState === 'restarting')
        this.scheduleReloadAfterRestart();
      else
        this.fallbackToManualDownload();
    }
  }

  onUpdateInstallState(state: string, message: string) {
    this.updateInstallState = state;
    this.updateInstallMessage = message;

    if (state === 'failed')
      this.fallbackToManualDownload();
  }

  private scheduleReloadAfterRestart() {
    this.updateInstallState = 'restarting';
    this.updateInstallMessage = this.updateInstallMessage ?? 'Update installed. Restarting SpeedSeat…';
    // Give the new backend time to start and bind port 5000, then reload this tab onto it.
    setTimeout(() => window.location.reload(), 9000);
  }

  private fallbackToManualDownload() {
    if (this.updateFallbackDone)
      return;
    this.updateFallbackDone = true;
    this.updateInstalling = false;
    this.updateInstallState = undefined;
    this.updateInstallMessage = undefined;
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

    // Show the first-time-setup / USB-flash help only after the seat has failed to connect
    // for a few seconds (a freshly flashed/unconfigured seat never shows up over WiFi).
    if (isConnected) {
      clearTimeout(this.flashHelpTimer);
      this.flashHelpTimer = undefined;
      this.showFlashHelp = false;
    } else if (this.flashHelpTimer === undefined) {
      this.flashHelpTimer = setTimeout(() => this.showFlashHelp = true, AppComponent.FLASH_HELP_DELAY_MS);
    }
  }

  async flashViaUsb() {
    if (this.usbFlashing)
      return;
    this.usbFlashing = true;
    this.usbFlashState = 'flashing';
    this.usbFlashMessage = 'Starting USB flash…';
    try {
      await this.connectionService.flashViaUsb();
    } catch {
      // Progress and the final result arrive via the usbFlashState push events.
    }
  }

  onUsbFlashState(state: string, message: string) {
    this.usbFlashState = state;
    this.usbFlashMessage = message;
    this.usbFlashing = state === 'flashing';

    if (state === 'success' || state === 'failed') {
      setTimeout(() => {
        if (this.usbFlashState === state) {
          this.usbFlashState = undefined;
          this.usbFlashMessage = undefined;
        }
      }, 15000);
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
