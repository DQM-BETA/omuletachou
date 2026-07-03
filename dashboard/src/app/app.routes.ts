import { Routes } from '@angular/router';
import { ProductsComponent } from './pages/products/products.component';
import { QueueComponent } from './pages/queue/queue.component';
import { FacebookManualComponent } from './pages/facebook-manual/facebook-manual.component';
import { SettingsComponent } from './pages/settings/settings.component';
import { ReportsComponent } from './pages/reports/reports.component';

export const routes: Routes = [
  { path: '', redirectTo: '/products', pathMatch: 'full' },
  { path: 'products', component: ProductsComponent },
  { path: 'queue', component: QueueComponent },
  { path: 'facebook-manual', component: FacebookManualComponent },
  { path: 'settings', component: SettingsComponent },
  { path: 'reports', component: ReportsComponent },
];
