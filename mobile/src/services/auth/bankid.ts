import { Linking, Platform } from 'react-native';
import { apiClient } from '../api';
import type {
  BankIDInitResponse,
  BankIDStatusResponse,
  SSOSession,
} from '../../types/auth';

/**
 * BankID Authentication Service
 * Implements BankID authentication with QR code and polling
 */
class BankIDAuthService {
  private readonly pollInterval = 2000; // 2 seconds
  private readonly maxPollAttempts = 90; // 3 minutes total
  private pollingTimer: NodeJS.Timeout | null = null;

  /**
   * Initiate BankID authentication
   * @param personalNumber Optional personal number for direct flow
   */
  async initiateAuth(personalNumber?: string): Promise<SSOSession> {
    try {
      const endUserIp = await this.getUserIp();

      const request: any = {
        endUserIp,
        ...(personalNumber && { personalNumber }),
      };

      const response = await apiClient.post<BankIDInitResponse>(
        '/auth/bankid/initiate',
        request
      );

      if (!response.data) {
        throw new Error('Failed to initiate BankID authentication');
      }

      const { sessionId, qrCodeData, autoStartToken } = response.data;

      // Launch BankID app if on mobile
      if (Platform.OS !== 'web' && autoStartToken) {
        await this.launchBankIDApp(autoStartToken);
      }

      return {
        provider: 'bankid',
        sessionId,
        status: 'pending',
        bankid: {
          qrCodeData,
          autoStartToken,
        },
      };
    } catch (error: any) {
      return {
        provider: 'bankid',
        sessionId: '',
        status: 'failed',
        error: error.message || 'Failed to initiate BankID authentication',
      };
    }
  }

  /**
   * Launch BankID app on mobile devices
   */
  private async launchBankIDApp(autoStartToken: string): Promise<void> {
    try {
      const scheme = Platform.OS === 'ios' ? 'bankid:///' : 'bankid://';
      const url = `${scheme}?autostarttoken=${autoStartToken}&redirect=null`;

      const supported = await Linking.canOpenURL(url);
      if (supported) {
        await Linking.openURL(url);
      }
    } catch (error) {
      console.warn('Failed to launch BankID app:', error);
    }
  }

  /**
   * Poll BankID session status
   */
  async pollStatus(
    sessionId: string,
    onStatusChange?: (status: SSOSession) => void
  ): Promise<SSOSession> {
    let attempts = 0;

    return new Promise((resolve) => {
      this.pollingTimer = setInterval(async () => {
        attempts++;

        try {
          const response = await apiClient.get<BankIDStatusResponse>(
            `/auth/bankid/status/${sessionId}`
          );

          const { status, hintCode } = response.data;

          const session: SSOSession = {
            provider: 'bankid',
            sessionId,
            status: this.mapBankIDStatus(status),
            bankid: {
              qrCodeData: '',
              autoStartToken: '',
              hintCode,
            },
          };

          if (onStatusChange) {
            onStatusChange(session);
          }

          if (status === 'complete') {
            this.stopPolling();
            resolve({ ...session, status: 'completed' });
          } else if (status === 'failed' || status === 'expired') {
            this.stopPolling();
            resolve({
              ...session,
              status: 'failed',
              error: this.getHintMessage(hintCode),
            });
          }

          if (attempts >= this.maxPollAttempts) {
            this.stopPolling();
            resolve({
              ...session,
              status: 'expired',
              error: 'Authentication timed out',
            });
          }
        } catch (error: any) {
          this.stopPolling();
          resolve({
            provider: 'bankid',
            sessionId,
            status: 'failed',
            error: error.message || 'Polling failed',
            bankid: { qrCodeData: '', autoStartToken: '' },
          });
        }
      }, this.pollInterval);
    });
  }

  stopPolling(): void {
    if (this.pollingTimer) {
      clearInterval(this.pollingTimer);
      this.pollingTimer = null;
    }
  }

  async cancelAuth(sessionId: string): Promise<void> {
    try {
      this.stopPolling();
      await apiClient.post(`/auth/bankid/cancel/${sessionId}`);
    } catch (error) {
      console.warn('Failed to cancel BankID session:', error);
    }
  }

  async completeAuth(
    sessionId: string
  ): Promise<{ success: boolean; token?: string; user?: any; error?: string }> {
    try {
      const response = await apiClient.post('/auth/bankid/complete', {
        sessionId,
      });

      if (response.data.token) {
        return {
          success: true,
          token: response.data.token,
          user: response.data.user,
        };
      }

      return {
        success: false,
        error: response.data.error || 'Failed to complete authentication',
      };
    } catch (error: any) {
      return {
        success: false,
        error: error.response?.data?.error || error.message || 'Authentication completion failed',
      };
    }
  }

  private mapBankIDStatus(
    status: BankIDStatusResponse['status']
  ): SSOSession['status'] {
    switch (status) {
      case 'pending':
        return 'pending';
      case 'complete':
        return 'completed';
      case 'failed':
      case 'expired':
        return 'failed';
      default:
        return 'pending';
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
    } catch (error) {
      return '0.0.0.0';
    }
  }

  isConfigured(): boolean {
    return true;
  }
}

export const bankIDAuth = new BankIDAuthService();
