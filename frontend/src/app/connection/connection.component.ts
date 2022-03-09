import { Component, OnDestroy, OnInit } from '@angular/core';
import { ConnectionDataService } from './connection-data.service';

@Component({
  selector: 'app-connection',
  templateUrl: './connection.component.html',
  styleUrls: ['./connection.component.scss']
})
export class ConnectionComponent implements OnInit, OnDestroy {
  selected = 'option2';

  constructor(public data: ConnectionDataService) { }

  ngOnInit(): void {
    this.data.init();
    this.data.serialPorts$.subscribe(console.log)
  }

  ngOnDestroy(): void {
    this.data.destroy();
  }
}
