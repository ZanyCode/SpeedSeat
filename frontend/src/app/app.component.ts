import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Observable } from 'rxjs';
import { map, shareReplay } from 'rxjs/operators';
import { AppDataService } from './app-data.service';

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


  isHandset$: Observable<boolean> = this.breakpointObserver.observe(Breakpoints.Handset)
    .pipe(
      map(result => result.matches),
      shareReplay()
    );

  constructor(private breakpointObserver: BreakpointObserver, private data: AppDataService) {}

 
  ngOnInit(): void {
    this.data.init(this.onLogReceived).then(async () =>{
      this.url = await this.data.GetOwnUrl();
    });
  } 

  onLogReceived = (message: string) => {
    this.log += `\n[${new Date().toLocaleString()}]: ${message}`;
    setTimeout(() => this.logTextareaScrolltop = this.textarea?.nativeElement.scrollHeight, 0);
  }

  ngOnDestroy(): void {
    this.data.destroy();
  }
}
