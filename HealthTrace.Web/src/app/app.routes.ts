import { Routes } from '@angular/router';
import { Login } from './pages/login/login';
import { Register } from './pages/register/register';
import { Symptoms } from './pages/symptoms/symptoms';
import { SymptomDetail } from './pages/symptom-detail/symptom-detail';
import { SymptomForm } from './pages/symptom-form/symptom-form';
import { Export } from './pages/export/export';
import { authGuard, guestGuard } from './guards/auth.guard';

export const routes: Routes = [
  {
  path:'', pathMatch:'full', redirectTo:'symptoms'
  },
  {
  path:'login', component:Login, canActivate:[guestGuard]
  },
  {
  path:'register', component:Register, canActivate:[guestGuard]
  },
  {
  path:'symptoms', component:Symptoms, canActivate:[authGuard]
  },
  {
  path:'symptoms/new', component:SymptomForm, canActivate:[authGuard]
  },
  {
  path:'symptoms/:id', component:SymptomDetail, canActivate:[authGuard]
  },
  {
  path:'symptoms/:id/edit', component:SymptomForm, canActivate:[authGuard]
  },
  {
  path:'export', component:Export, canActivate:[authGuard]
  },
  {
  path:'**', redirectTo:'symptoms'
  }];
