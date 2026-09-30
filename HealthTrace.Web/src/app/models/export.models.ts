//PDF export interfaces: they mirror ExportRequestModel on the API side

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

//body of POST /api/exports/request: with no fields it exports all symptoms,
//with symptomIds it exports only those (dates are ignored)
export interface ExportRequestCreate {
  symptomIds?: number[];
  fromDate?: string;
  toDate?: string;
}
