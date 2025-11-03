import * as Notifications from 'expo-notifications';
import * as Device from 'expo-device';
import Constants from 'expo-constants';
import { Platform } from 'react-native';
import { NOTIFICATION_CONFIG, ERROR_MESSAGES } from '../constants';
import { apiClient } from './api';
import type { NotificationPayload } from '../types';

// Configure notification behavior
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true as boolean,
    shouldPlaySound: true as boolean,
    shouldSetBadge: true as boolean,
  }),
});

class NotificationService {
  private pushToken: string | null = null;
  private deviceId: string | null = null;
  private notificationListener: Notifications.Subscription | null = null;
  private responseListener: Notifications.Subscription | null = null;

  async initialize(deviceId: string): Promise<void> {
    console.log('NotificationService: Starting initialization...');
    this.deviceId = deviceId;

    // Request permissions
    console.log('NotificationService: Requesting permissions...');
    const { status } = await this.requestPermissions();
    console.log('NotificationService: Permission status:', status);
    if (status !== 'granted') {
      console.warn('Notification permissions not granted');
      return;
    }

    // Get push token
    console.log('NotificationService: Getting push token...');
    const token = await this.getPushToken();
    if (token) {
      this.pushToken = token;
      console.log('NotificationService: Registering push token...');
      await this.registerPushToken(token);
    }

    // Setup notification listeners
    console.log('NotificationService: Setting up listeners...');
    this.setupListeners();

    // Configure Android notification channel
    if (Platform.OS === 'android') {
      console.log('NotificationService: Setting up Android channel...');
      await this.setupAndroidChannel();
    }

    console.log('NotificationService: Initialization complete!');
  }

  async requestPermissions(): Promise<{ status: string }> {
    if (!Device.isDevice) {
      console.warn('Must use physical device for push notifications');
      return { status: 'unavailable' };
    }

    const { status: existingStatus } = await Notifications.getPermissionsAsync();
    let finalStatus = existingStatus;

    if (existingStatus !== 'granted') {
      const { status } = await Notifications.requestPermissionsAsync();
      finalStatus = status;
    }

    return { status: finalStatus };
  }

  private async getPushToken(): Promise<string | null> {
    if (!Device.isDevice) {
      console.warn('Must use physical device for push notifications');
      return null;
    }

    try {
      const projectId = Constants.expoConfig?.extra?.eas?.projectId;

      if (!projectId) {
        console.error('EAS project ID not found');
        return null;
      }

      const token = await Notifications.getExpoPushTokenAsync({
        projectId,
      });

      return token.data;
    } catch (error) {
      console.error('Failed to get push token:', error);
      return null;
    }
  }

  private async registerPushToken(token: string): Promise<void> {
    if (!this.deviceId) {
      console.error('Device ID not set');
      return;
    }

    try {
      await apiClient.updateDevicePushToken(this.deviceId, token);
      console.log('Push token registered successfully');
    } catch (error) {
      console.error('Failed to register push token:', error);
    }
  }

  private setupListeners(): void {
    // Listener for notifications received while app is foregrounded
    this.notificationListener = Notifications.addNotificationReceivedListener(
      (notification) => {
        console.log('Notification received:', notification);
        const payload = notification.request.content.data as NotificationPayload;
        this.handleNotification(payload);
      }
    );

    // Listener for when user interacts with notification
    this.responseListener = Notifications.addNotificationResponseReceivedListener(
      (response) => {
        console.log('Notification response:', response);
        const payload = response.notification.request.content.data as NotificationPayload;
        this.handleNotificationResponse(payload);
      }
    );
  }

  private async setupAndroidChannel(): Promise<void> {
    await Notifications.setNotificationChannelAsync(NOTIFICATION_CONFIG.CHANNEL_ID, {
      name: NOTIFICATION_CONFIG.CHANNEL_NAME,
      description: NOTIFICATION_CONFIG.CHANNEL_DESCRIPTION,
      importance: Notifications.AndroidImportance.MAX,
      vibrationPattern: [0, 250, 250, 250],
      // sound and vibrate are controlled by importance level in newer Android versions
    });
  }

  private handleNotification(payload: NotificationPayload): void {
    // Custom logic for foreground notifications
    console.log('Handling foreground notification:', payload);

    // You can emit events here to update UI, trigger sounds, etc.
    // For now, just log the alert
  }

  private handleNotificationResponse(payload: NotificationPayload): void {
    // Handle user tapping on notification
    console.log('User tapped notification:', payload);

    // Navigate to alert details or home screen
    // This would typically use your navigation service
  }

  async sendLocalNotification(
    title: string,
    body: string,
    data?: Record<string, any>
  ): Promise<void> {
    try {
      await Notifications.scheduleNotificationAsync({
        content: {
          title,
          body,
          data: data || {},
          sound: true,
          priority: Notifications.AndroidNotificationPriority.MAX,
          categoryIdentifier: NOTIFICATION_CONFIG.CHANNEL_ID,
        },
        trigger: null, // Deliver immediately
      });
    } catch (error) {
      console.error('Failed to send local notification:', error);
    }
  }

  async clearAllNotifications(): Promise<void> {
    await Notifications.dismissAllNotificationsAsync();
  }

  async setBadgeCount(count: number): Promise<void> {
    await Notifications.setBadgeCountAsync(count);
  }

  cleanup(): void {
    if (this.notificationListener) {
      Notifications.removeNotificationSubscription(this.notificationListener);
    }
    if (this.responseListener) {
      Notifications.removeNotificationSubscription(this.responseListener);
    }
  }

  getPushToken(): string | null {
    return this.pushToken;
  }
}

export const notificationService = new NotificationService();
