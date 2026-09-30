//symptom interfaces: they mirror SymptomModel on the API side (camelCase JSON)

export interface Symptom {
  id: number;
  userId: number;
  eventName: string;
  description?: string | null;
  eventDate: string; //ISO date/time without time zone, e.g. "2026-09-23T08:30:00"
}

//body of POST and PUT: userId is not sent, the API takes it from the authenticated user
export interface SymptomRequest {
  eventName: string;
  description?: string | null;
  eventDate: string;
}

//list filters: the API accepts date or name, never both
export interface SymptomFilter {
  date?: string; //yyyy-MM-dd format
  name?: string;
}
