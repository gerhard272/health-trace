//interfaces for authentication and registration requests and responses
//exported so they can be used by AuthService and the login and register pages

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
