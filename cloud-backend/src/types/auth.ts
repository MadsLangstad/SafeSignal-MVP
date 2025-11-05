/**
 * Authentication Type Definitions
 * Defines types for SSO authentication providers and session management
 */

export type AuthProvider = 'email' | 'feide' | 'bankid' | 'biometric';

export interface User {
  id: string;
  email: string;
  name?: string;
  organizationId?: string;
  provider: AuthProvider;
  feideId?: string;
  bankIdSubject?: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken?: string;
  expiresAt?: Date;
}

// Feide OAuth Types
export interface FeideAuthRequest {
  state: string;
  codeVerifier: string;
  codeChallenge: string;
  redirectUri: string;
}

export interface FeideTokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
  refresh_token?: string;
  id_token?: string;
  scope: string;
}

export interface FeideUserInfo {
  sub: string;
  name?: string;
  email?: string;
  email_verified?: boolean;
  eduPersonPrincipalName?: string;
  eduPersonAffiliation?: string[];
}

// BankID Types
export interface BankIDInitResponse {
  sessionId: string;
  qrCodeData: string;
  autoStartToken: string;
}

export interface BankIDStatusResponse {
  status: 'pending' | 'complete' | 'failed' | 'expired';
  hintCode?: string;
  completionData?: {
    user: {
      personalNumber: string;
      name: string;
      givenName?: string;
      surname?: string;
    };
    signature?: string;
  };
  error?: string;
}

export interface BankIDCompleteRequest {
  sessionId: string;
}

// SSO Session Management
export interface SSOSession {
  provider: Exclude<AuthProvider, 'email' | 'biometric'>;
  sessionId: string;
  status: 'initiated' | 'pending' | 'completed' | 'failed' | 'expired';
  expiresAt?: Date;
  error?: string;

  // Provider-specific data
  feide?: {
    state: string;
    codeVerifier: string;
    redirectUri: string;
  };

  bankid?: {
    qrCodeData: string;
    autoStartToken: string;
    hintCode?: string;
  };
}

// API Response Types
export interface AuthResponse {
  success: boolean;
  token?: string;
  refreshToken?: string;
  user?: User;
  error?: string;
}
