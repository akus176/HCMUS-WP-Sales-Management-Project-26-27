import { Stack, usePathname } from 'expo-router';
import { useFonts } from 'expo-font';
import { StatusBar } from 'expo-status-bar';
import { KeyboardAvoidingView, Text } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { ConfigurationGate } from '../src/config/ConfigurationGate';
import { ConnectivityPresentation } from '../src/connectivity/ConnectivityBanner';
import { ConnectivityProvider } from '../src/connectivity/connectivity';
import { MobileNotifications } from '../src/notifications/MobileNotifications';
import { OperationalUpdates } from '../src/realtime/OperationalUpdates';
import { SessionProvider, useSession } from '../src/session/context';
import { ShellPage } from '../src/shell/controls';
import { mobileTokens } from '../src/shell/designTokens';

export default function RootLayout() {
  return (
    <ConfigurationGate>
      <SessionProvider>
        <SessionRoutes />
      </SessionProvider>
    </ConfigurationGate>
  );
}

export function SessionRoutes() {
  const { state } = useSession();
  return (
    <ConnectivityProvider>
      <OperationalUpdates />
      <SafeAreaView
        edges={['top', 'left', 'right']}
        style={{ flex: 1, backgroundColor: mobileTokens.color.background }}
      >
        <SessionNavigation state={state} />
      </SafeAreaView>
    </ConnectivityProvider>
  );
}

function SessionNavigation({
  state,
}: {
  state: ReturnType<typeof useSession>['state'];
}) {
  const pathname = usePathname();
  const [fontsLoaded, fontError] = useFonts({
    Kalam: require('../assets/fonts/Kalam-Bold.ttf'),
    PatrickHand: require('../assets/fonts/PatrickHand-Regular.ttf'),
  });
  if (!fontsLoaded && !fontError)
    return (
      <>
        <StatusBar style="dark" />
        <ShellPage>
          <Text accessibilityRole="text">Loading interface…</Text>
        </ShellPage>
      </>
    );
  if (state.status === 'restoring')
    return (
      <>
        <StatusBar style="dark" />
        <ShellPage>
          <Text accessibilityRole="alert">Restoring session…</Text>
        </ShellPage>
      </>
    );
  return (
    <KeyboardAvoidingView behavior="height" style={{ flex: 1 }}>
      <StatusBar style="dark" />
      <ConnectivityPresentation
        branded={
          pathname === '/modules/barista' ||
          pathname.startsWith('/notifications/urgent-order/')
        }
      >
        <Stack screenOptions={{ headerShown: false }}>
          <Stack.Protected guard={!state.user}>
            <Stack.Screen name="index" />
            <Stack.Screen name="login" />
          </Stack.Protected>
          <Stack.Protected guard={!!state.user}>
            <Stack.Screen name="session" />
            <Stack.Screen name="modules/[module]" />
            <Stack.Screen name="modules/delivery/[orderId]" />
            <Stack.Screen name="modules/approvals/[discountId]" />
            <Stack.Screen name="modules/approvals/operations" />
            <Stack.Screen name="notifications/[kind]/[recordId]" />
          </Stack.Protected>
        </Stack>
        <MobileNotifications />
      </ConnectivityPresentation>
    </KeyboardAvoidingView>
  );
}
