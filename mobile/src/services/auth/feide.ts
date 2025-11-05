import * as AuthSession from 'expo-auth-session';
import * as WebBrowser from 'expo-web-browser';
import * as Crypto from 'expo-crypto';
import axios from 'axios';
import { API_CONFIG } from '../../constants';
import type { SSOSession } from '../../types/auth';

WebBrowser.maybeCompleteAuthSession();

class FeideAuthService {
  private readonly clientId: string;
  private readonly discoveryEndpoint: string;
  private readonly redirectUri: string;
  private readonly scopes: string[];

  constructor() {
    this.clientId = process.env.EXPO_PUBLIC_FEIDE_CLIENT_ID || '';
    this.discoveryEndpoint = process.env.EXPO_PUBLIC_FEIDE_DISCOVERY_URL || 'https://auth.dataporten.no/.well-known/openid-configuration';
    this.redirectUri = AuthSession.makeRedirectUri({ scheme: process.env.EXPO_PUBLIC_APP_SCHEME || 'safesignal', path: 'auth/feide/callback' });
    this.scopes = ['openid', 'profile', 'email', 'eduPersonPrincipalName'];
  }

  private async generatePKCE(): Promise<{ codeVerifier: string; codeChallenge: string }> {
    const codeVerifier = Crypto.randomUUID();
    const codeChallengeBuffer = await Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, codeVerifier, { encoding: Crypto.CryptoEncoding.BASE64 });
    const codeChallenge = codeChallengeBuffer.replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
    return { codeVerifier, codeChallenge };
  }

  private async generateState(): Promise<string> {
    return Crypto.randomUUID();
  }

  async initiateAuth(): Promise<SSOSession> {
    try {
      const { codeVerifier, codeChallenge } = await this.generatePKCE();
      const state = await this.generateState();
      const discovery = await AuthSession.fetchDiscoveryAsync(this.discoveryEndpoint);
      
      const authUrl = `${discovery.authorizationEndpoint}?client_id=${this.clientId}&redirect_uri=${this.redirectUri}&response_type=code&scope=${this.scopes.join(' ')}&state=${state}&code_challenge=${codeChallenge}&code_challenge_method=S256`;
      
      const result = await WebBrowser.openAuthSessionAsync(authUrl, this.redirectUri);

      if (result.type === 'success' && result.url) {
        const url = new URL(result.url);
        const code = url.searchParams.get('code');
        const returnedState = url.searchParams.get('state');
        
        if (!code || returnedState !== state) {
          throw new Error('Invalid callback response');
        }
        
        return { provider: 'feide', sessionId: code, status: 'pending', feide: { state, codeVerifier, redirectUri: this.redirectUri } };
      } else if (result.type === 'cancel') {
        return { provider: 'feide', sessionId: '', status: 'failed', error: 'User cancelled authentication' };
      } else {
        return { provider: 'feide', sessionId: '', status: 'failed', error: 'Authentication failed' };
      }
    } catch (error: any) {
      return { provider: 'feide', sessionId: '', status: 'failed', error: error.message || 'Failed to initiate Feide authentication' };
    }
  }

  async exchangeCodeForToken(code: string, codeVerifier: string): Promise<{ success: boolean; token?: string; user?: any; error?: string }> {
    try {
      const response = await axios.post(`${API_CONFIG.BASE_URL}/auth/feide/callback`, { code, codeVerifier, redirectUri: this.redirectUri });
      if (response.data.token) {
        return { success: true, token: response.data.token, user: response.data.user };
      }
      return { success: false, error: response.data.error || 'Failed to exchange code' };
    } catch (error: any) {
      return { success: false, error: error.response?.data?.error || error.message || 'Token exchange failed' };
    }
  }

  getRedirectUri(): string {
    return this.redirectUri;
  }

  isConfigured(): boolean {
    return Boolean(this.clientId && this.discoveryEndpoint);
  }
}

export const feideAuth = new FeideAuthService();
