//interfacce per gli export PDF: rispecchiano ExportRequestModel lato API

export type ExportStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed';

export interface ExportRequest {
  id: number;
  status: ExportStatus;
  createdAt: string;
  symptomIds: number[];
  fromDate?: string | null;
  toDate?: string | null;
  fileName?: string | null;
  errorMessage?: string | null;
}

//corpo di POST /api/exports/request: senza campi esporta tutti i sintomi,
//con symptomIds esporta solo quelli (le date vengono ignorate)
export interface ExportRequestCreate {
  symptomIds?: number[];
  fromDate?: string;
  toDate?: string;
}
