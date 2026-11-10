import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { AppState } from 'react-native';

import { useConnectivity } from '../connectivity/connectivity';
import { useSession } from '../session/context';
import { ShellStatus } from '../shell/controls';
import {
  operationalEventType,
  refreshOperationalQueries,
} from './operationalEvents';

export function OperationalUpdates() {
  const { client, state } = useSession();
  const { status } = useConnectivity();
  const queries = useQueryClient();
  const userId = state.user?.id;
  const [foreground, setForeground] = useState(
    AppState.currentState !== 'background' &&
      AppState.currentState !== 'inactive',
  );
  const [degraded, setDegraded] = useState(false);

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (next) =>
      setForeground(next === 'active'),
    );
    return () => subscription.remove();
  }, []);

  useEffect(() => {
    if (!client || !userId || status !== 'online' || !foreground) return;
    let stopped = false;
    let retry: ReturnType<typeof setTimeout> | undefined;
    const isCurrent = () =>
      !stopped && client.getSnapshot().user?.id === userId;
    const connection = new HubConnectionBuilder()
      .withUrl(client.operationalHubUrl, {
        accessTokenFactory: () => client.getOperationalAccessToken(userId),
        logMessageContent: false,
        timeout: 10000,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();
    function connected() {
      if (!isCurrent()) return;
      setDegraded(false);
      void refreshOperationalQueries(queries, userId!, isCurrent);
    }
    function disconnected() {
      if (!isCurrent()) return;
      setDegraded(true);
      clearTimeout(retry);
      retry = setTimeout(start, 5000);
    }
    function start() {
      if (isCurrent()) void connection.start().then(connected, disconnected);
    }
    connection.onreconnecting(() => {
      if (isCurrent()) setDegraded(true);
    });
    connection.onreconnected(connected);
    connection.onclose(disconnected);
    connection.on('OperationalEvent', (event: unknown) => {
      const type = operationalEventType(event);
      if (type && isCurrent())
        void refreshOperationalQueries(queries, userId, isCurrent, type);
    });
    start();
    return () => {
      stopped = true;
      clearTimeout(retry);
      void connection.stop();
    };
  }, [client, foreground, queries, status, userId]);

  if (!userId || status !== 'online' || !degraded) return null;
  return (
    <ShellStatus>
      Live updates unavailable. Refresh to check for changes.
    </ShellStatus>
  );
}
