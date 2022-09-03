import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ConnectionComponent } from './connection/connection.component';
import { ManualControlComponent } from './manual-control/manual-control.component';
import { ProgramSettingsComponent } from './program-settings/program-settings.component';
import { SeatSettingsComponent } from './seat-settings/seat-settings.component';
import { TelemetryComponent } from './telemetry/telemetry.component';

const routes: Routes = [
  { path: '', redirectTo: '/manual', pathMatch: 'full' },
  { path: 'manual', component: ManualControlComponent },
  { path: 'program-settings', component: ProgramSettingsComponent },
  { path: 'seat-settings', component: SeatSettingsComponent },
  { path: 'connection', component: ConnectionComponent },
  { path: 'telemetry', component: TelemetryComponent },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
