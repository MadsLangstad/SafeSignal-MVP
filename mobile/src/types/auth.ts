// Authentication provider types for SSO
export type AuthProvider = 'email' | 'feide' | 'bankid' | 'biometric';

// Feide-specific types
export interface FeideTokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
  refresh_token?: string;
  id_token?: string;
  scope?: string;
}

export interface FeideUserInfo {
  sub: string;
  name?: string;
  email?: string;
  email_verified?: boolean;
  'eduPersonPrincipalName'?: string;
  'eduPersonAffiliation'?: string[];
}

export interface FeideAuthRequest {
  responseType: string;
  clientId: string;
  redirectUri: string;
  scope: string[];
  state: string;
  codeVerifier?: string;
  codeChallenge?: string;
  codeChallengeMethod?: string;
}

// BankID-specific types
export interface BankIDInitResponse {
  sessionId: string;
  qrCodeData: string;
  autoStartToken: string;
  expiresAt: string;
}

export interface BankIDStatusResponse {
  status: 'pending' | 'complete' | 'failed' | 'expired';
  hintCode?: string;
  completionData?: {
    user: {
      personalNumber: string;
      name: string;
      givenName: string;
      surname: string;
    };
    signature: string;
    ocspResponse: string;
  };
}

export interface BankIDAuthRequest {
  personalNumber?: string; // Optional for QR code flow
  endUserIp: string;
  requirement?: {
    pinCode?: boolean;
    mrtd?: boolean;
    cardReader?: 'class1' | 'class2';
    certificatePolicies?: string[];
  };
}

// Unified SSO session state
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

// Auth store state
export interface AuthState {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;

  // SSO session tracking
  ssoSession: SSOSession | null;

  // Biometric state
  biometricEnabled: boolean;
  biometricType: 'faceId' | 'fingerprint' | 'iris' | null;

  // Actions
  login: (email: string, password: string) => Promise<void>;
  loginWithFeide: () => Promise<void>;
  loginWithBankID: (personalNumber?: string) => Promise<void>;
  handleFeideCallback: (code: string, state: string) => Promise<void>;
  pollBankIDStatus: (sessionId: string) => Promise<void>;
  loginWithBiometric: () => Promise<void>;
  logout: () => Promise<void>;
  clearError: () => void;
  clearSSOSession: () => void;
}

// Re-export existing User type
export type { User } from './index';
