import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ConnectionComponent } from './connection/connection.component';
import { ManualControlComponent } from './manual-control/manual-control.component';
import { SettingsComponent } from './settings/settings.component';

const routes: Routes = [
  { path: '', redirectTo: '/manual', pathMatch: 'full' },
  { path: 'manual', component: ManualControlComponent },
  { path: 'settings', component: SettingsComponent },
  { path: 'connection', component: ConnectionComponent },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
