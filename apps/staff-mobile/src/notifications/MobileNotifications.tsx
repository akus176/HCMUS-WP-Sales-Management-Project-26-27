import Constants, { ExecutionEnvironment } from 'expo-constants';
import { router, useRootNavigationState } from 'expo-router';
import type * as Notifications from 'expo-notifications';
import { useEffect, useEffectEvent, useRef, useState } from 'react';
import { AppState, Platform } from 'react-native';

import { useConnectivity } from '../connectivity/connectivity';
import { useSession } from '../session/context';
import {
  notificationBody,
  notificationDestination,
  notificationTitle,
} from './destination';
import { registerForNotifications } from './registerForNotifications';

const channelId = 'staff-updates';

export function MobileNotifications() {
  const { client, state } = useSession();
  const { status } = useConnectivity();
  const navigationKey = useRootNavigationState()?.key;
  const platform = Platform.OS;
  const userId = state.user?.id;
  const projectId =
    Constants.easConfig?.projectId ??
    Constants.expoConfig?.extra?.eas?.projectId;
  const [native, setNative] = useState<typeof Notifications>();
  const pending = useRef<
    | {
        target: NonNullable<ReturnType<typeof notificationDestination>>;
        owner?: string;
      }
    | undefined
  >(undefined);
  const [foreground, setForeground] = useState(
    AppState.currentState !== 'background' &&
      AppState.currentState !== 'inactive',
  );
  const prompted = useRef(false);
  const openPending = useEffectEvent(() => {
    const entry = pending.current;
    const currentUserId = client?.getSnapshot().user?.id;
    if (!entry || !navigationKey || !currentUserId || currentUserId !== userId)
      return;
    pending.current = undefined;
    if (entry.owner && entry.owner !== currentUserId) return;
    router.push({
      pathname: '/notifications/[kind]/[recordId]',
      params: { kind: entry.target.kind, recordId: entry.target.recordId },
    });
  });

  useEffect(() => {
    if (
      (platform !== 'android' && platform !== 'ios') ||
      typeof projectId !== 'string' ||
      Constants.executionEnvironment === ExecutionEnvironment.StoreClient
    )
      return;
    let active = true;
    void import('expo-notifications')
      .then((module) => {
        if (active) setNative(module);
      })
      .catch(() => undefined);
    return () => {
      active = false;
    };
  }, [platform, projectId]);

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (next) =>
      setForeground(next === 'active'),
    );
    return () => subscription.remove();
  }, []);

  useEffect(() => {
    if (!native || !client) return;
    native.setNotificationHandler({
      handleNotification: async ({ request: { content } }) => {
        const show =
          !!notificationDestination(content.data) &&
          content.title === notificationTitle &&
          content.body === notificationBody;
        return {
          shouldShowBanner: show,
          shouldShowList: show,
          shouldPlaySound: false,
          shouldSetBadge: false,
        };
      },
    });
    let lastResponse: string | undefined;
    function respond(response: Notifications.NotificationResponse) {
      const { request } = response.notification;
      if (
        response.actionIdentifier !== native!.DEFAULT_ACTION_IDENTIFIER ||
        request.identifier === lastResponse
      )
        return;
      lastResponse = request.identifier;
      native!.clearLastNotificationResponse();
      const target = notificationDestination(request.content.data);
      if (target) {
        pending.current = { target, owner: client!.getSnapshot().user?.id };
        openPending();
      }
    }
    const subscription =
      native.addNotificationResponseReceivedListener(respond);
    const previous = native.getLastNotificationResponse();
    if (previous) respond(previous);
    return () => {
      subscription.remove();
      native.setNotificationHandler(null);
    };
  }, [client, native]);

  useEffect(() => {
    openPending();
  }, [navigationKey, userId]);

  useEffect(() => {
    if (
      !native ||
      !client ||
      !userId ||
      !foreground ||
      status !== 'online' ||
      (platform !== 'android' && platform !== 'ios') ||
      typeof projectId !== 'string'
    )
      return;
    const registration = registerForNotifications({
      hasPermission: async () => {
        if (platform === 'android')
          await native.setNotificationChannelAsync(channelId, {
            name: 'Staff updates',
            importance: native.AndroidImportance.DEFAULT,
            lockscreenVisibility: native.AndroidNotificationVisibility.PRIVATE,
          });
        const current = await native.getPermissionsAsync();
        if (current.granted || !current.canAskAgain || prompted.current)
          return current.granted;
        prompted.current = true;
        return (await native.requestPermissionsAsync()).granted;
      },
      readToken: async (devicePushToken?: Notifications.DevicePushToken) =>
        (await native.getExpoPushTokenAsync({ projectId, devicePushToken }))
          .data,
      listen: native.addPushTokenListener,
      sync: (token) => client.syncDevice(userId, platform, token),
      remove: () => client.removeDevice(userId),
    });
    return registration.stop;
  }, [client, foreground, native, platform, projectId, status, userId]);
  return null;
}
