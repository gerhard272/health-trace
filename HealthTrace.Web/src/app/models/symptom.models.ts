//interfacce per i sintomi: rispecchiano SymptomModel lato API (JSON in camelCase)

export interface Symptom {
  id: number;
  userId: number;
  eventName: string;
  description?: string | null;
  eventDate: string; //data/ora ISO senza fuso, es. "2026-09-23T08:30:00"
}

//corpo di POST e PUT: userId non si invia, l'API lo ricava dall'utente autenticato
export interface SymptomRequest {
  eventName: string;
  description?: string | null;
  eventDate: string;
}

//filtri della lista: l'API accetta date oppure name, mai entrambi
export interface SymptomFilter {
  date?: string; //formato yyyy-MM-dd
  name?: string;
}
