//definizione di interfacce per le richieste e risposte di autenticazione e registrazione
//uso di export per rendere le interfacce disponibili in authservice login e register

export interface LoginRequest {
  username: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  password: string;
  passwordConfirmation: string;
  firstName: string;
  lastName: string;
  cf: string;
  birthDate?: string;
  birthPlace?: string;
}

export interface UserResponse {
  id: number;
  username: string;
  firstName: string;
  lastName: string;
  cf: string;
  birthDate?: string;
  birthPlace?: string;
}
