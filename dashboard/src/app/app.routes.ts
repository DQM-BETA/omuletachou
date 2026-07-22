import { Routes } from '@angular/router';
import { ProductsComponent } from './pages/products/products.component';
import { QueueComponent } from './pages/queue/queue.component';
import { FacebookManualComponent } from './pages/facebook-manual/facebook-manual.component';
import { SettingsComponent } from './pages/settings/settings.component';
import { ReportsComponent } from './pages/reports/reports.component';
import { JobsComponent } from './pages/jobs/jobs.component';
import { LoginComponent } from './pages/login/login.component';
import { ShellComponent } from './core/shell/shell.component';
import { authGuard, loginGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent, canActivate: [loginGuard] },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'products', pathMatch: 'full' },
      { path: 'products', component: ProductsComponent },
      { path: 'queue', component: QueueComponent },
      { path: 'facebook-manual', component: FacebookManualComponent },
      { path: 'settings', component: SettingsComponent },
      { path: 'jobs', component: JobsComponent },
      { path: 'reports', component: ReportsComponent },
    ],
  },
];
