import { Linking, Platform } from 'react-native';
import axios from 'axios';
import { API_CONFIG } from '../../constants';
import type { BankIDInitResponse, BankIDStatusResponse, SSOSession } from '../../types/auth';

class BankIDAuthService {
  private readonly pollInterval = 2000;
  private readonly maxPollAttempts = 90;
  private pollingTimer: NodeJS.Timeout | null = null;

  async initiateAuth(personalNumber?: string): Promise<SSOSession> {
    try {
      const endUserIp = await this.getUserIp();
      const request: any = { endUserIp, ...(personalNumber && { personalNumber }) };
      const response = await axios.post<BankIDInitResponse>(`${API_CONFIG.BASE_URL}/api/auth/bankid/initiate`, request);
      
      if (!response.data) throw new Error('Failed to initiate BankID authentication');
      
      const { sessionId, qrCodeData, autoStartToken } = response.data;
      if (Platform.OS !== 'web' && autoStartToken) await this.launchBankIDApp(autoStartToken);
      
      return { provider: 'bankid', sessionId, status: 'pending', bankid: { qrCodeData, autoStartToken } };
    } catch (error: any) {
      return { provider: 'bankid', sessionId: '', status: 'failed', error: error.message || 'Failed to initiate BankID authentication' };
    }
  }

  private async launchBankIDApp(autoStartToken: string): Promise<void> {
    try {
      const scheme = Platform.OS === 'ios' ? 'bankid:///' : 'bankid://';
      const url = `${scheme}?autostarttoken=${autoStartToken}&redirect=null`;
      const supported = await Linking.canOpenURL(url);
      if (supported) await Linking.openURL(url);
    } catch (error) { console.warn('Failed to launch BankID app:', error); }
  }

  async pollStatus(sessionId: string, onStatusChange?: (status: SSOSession) => void, initialQrData?: string, initialAutoStartToken?: string): Promise<SSOSession> {
    let attempts = 0;
    return new Promise((resolve) => {
      this.pollingTimer = setInterval(async () => {
        attempts++;
        try {
          const response = await axios.get<BankIDStatusResponse>(`${API_CONFIG.BASE_URL}/api/auth/bankid/status/${sessionId}`);
          const { status, hintCode } = response.data;
          const session: SSOSession = {
            provider: 'bankid',
            sessionId,
            status: this.mapBankIDStatus(status),
            bankid: {
              qrCodeData: initialQrData || '',
              autoStartToken: initialAutoStartToken || '',
              hintCode
            }
          };

          if (onStatusChange) onStatusChange(session);

          if (status === 'complete') { this.stopPolling(); resolve({ ...session, status: 'completed' }); }
          else if (status === 'failed' || status === 'expired') { this.stopPolling(); resolve({ ...session, status: 'failed', error: this.getHintMessage(hintCode) }); }
          if (attempts >= this.maxPollAttempts) { this.stopPolling(); resolve({ ...session, status: 'expired', error: 'Authentication timed out' }); }
        } catch (error: any) {
          this.stopPolling();
          resolve({ provider: 'bankid', sessionId, status: 'failed', error: error.message || 'Polling failed', bankid: { qrCodeData: initialQrData || '', autoStartToken: initialAutoStartToken || '' } });
        }
      }, this.pollInterval);
    });
  }

  stopPolling(): void { if (this.pollingTimer) { clearInterval(this.pollingTimer); this.pollingTimer = null; } }

  async cancelAuth(sessionId: string): Promise<void> {
    try { this.stopPolling(); await axios.post(`${API_CONFIG.BASE_URL}/api/auth/bankid/cancel/${sessionId}`); }
    catch (error) { console.warn('Failed to cancel BankID session:', error); }
  }

  async completeAuth(sessionId: string): Promise<{ success: boolean; tokens?: { accessToken: string; refreshToken: string; expiresAt: string }; user?: any; error?: string }> {
    try {
      const response = await axios.post(`${API_CONFIG.BASE_URL}/api/auth/bankid/complete`, { sessionId });
      if (response.data.tokens && response.data.user) {
        return {
          success: true,
          tokens: response.data.tokens,
          user: response.data.user
        };
      }
      return { success: false, error: response.data.error || 'Failed to complete authentication' };
    } catch (error: any) {
      return { success: false, error: error.response?.data?.error || error.message || 'Authentication completion failed' };
    }
  }

  private mapBankIDStatus(status: BankIDStatusResponse['status']): SSOSession['status'] {
    switch (status) {
      case 'pending': return 'pending';
      case 'complete': return 'completed';
      case 'failed':
      case 'expired': return 'failed';
      default: return 'pending';
    }
  }

  private getHintMessage(hintCode?: string): string {
    const messages: Record<string, string> = {
      outstandingTransaction: 'Åpne BankID-appen på enheten din',
      noClient: 'BankID-appen er ikke installert',
      started: 'Signering startet i BankID-appen',
      userSign: 'Skriv inn sikkerhetskode i BankID-appen',
      expiredTransaction: 'BankID-sesjonen har utløpt',
      certificateErr: 'BankID-sertifikatet er ugyldig',
      userCancel: 'Innlogging avbrutt',
      cancelled: 'Innlogging avbrutt',
      startFailed: 'Kunne ikke starte BankID-appen',
    };
    return hintCode ? messages[hintCode] || 'Ukjent feil' : 'Autentisering feilet';
  }

  private async getUserIp(): Promise<string> {
    try {
      const response = await fetch('https://api.ipify.org?format=json');
      const data = await response.json();
      return data.ip;
    } catch (error) { return '0.0.0.0'; }
  }

  isConfigured(): boolean { return true; }
}

export const bankIDAuth = new BankIDAuthService();
