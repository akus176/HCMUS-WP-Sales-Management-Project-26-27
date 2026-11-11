import { useState } from 'react';
import {
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { ApiError, NetworkError } from '../src/api/errors';
import { useSession } from '../src/session/context';

export default function LoginScreen() {
  const { client } = useSession();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function submit() {
    if (busy || !client) return;
    if (!username.trim() || !password) {
      setError('Enter your username and password.');
      return;
    }
    setBusy(true);
    setError('');
    try {
      await client.login(username, password);
    } catch (failure) {
      setPassword('');
      setError(
        failure instanceof ApiError || failure instanceof NetworkError
          ? failure.message
          : 'Sign-in could not be completed. Try again.',
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <SafeAreaView style={styles.page}>
      <ScrollView
        contentContainerStyle={styles.scroll}
        keyboardShouldPersistTaps="handled"
      >
        <View style={styles.content}>
          <Text accessibilityRole="header" style={styles.title}>
            Staff sign-in
          </Text>
          {!client ? (
            <Text accessibilityRole="alert">
              Sign-in is not configured. Contact your administrator.
            </Text>
          ) : (
            <>
              <TextInput
                accessibilityLabel="Username"
                placeholder="Username"
                autoCapitalize="none"
                autoCorrect={false}
                autoComplete="username"
                maxLength={50}
                style={styles.input}
                editable={!busy}
                value={username}
                onChangeText={setUsername}
              />
              <TextInput
                accessibilityLabel="Password"
                placeholder="Password"
                secureTextEntry
                autoComplete="current-password"
                maxLength={1024}
                style={styles.input}
                editable={!busy}
                value={password}
                onChangeText={setPassword}
                onSubmitEditing={() => {
                  void submit();
                }}
              />
              {!!error && <Text accessibilityRole="alert">{error}</Text>}
              <Pressable
                accessibilityRole="button"
                accessibilityLabel="Sign in"
                accessibilityState={{ disabled: busy, busy }}
                disabled={busy}
                style={styles.button}
                onPress={() => {
                  void submit();
                }}
              >
                <Text style={styles.buttonText}>
                  {busy ? 'Signing in…' : 'Sign in'}
                </Text>
              </Pressable>
            </>
          )}
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  page: { flex: 1, backgroundColor: '#fff9ed' },
  scroll: { flexGrow: 1 },
  content: {
    flex: 1,
    justifyContent: 'center',
    alignSelf: 'center',
    width: '100%',
    maxWidth: 600,
    padding: 24,
    gap: 16,
  },
  title: { color: '#302820', fontSize: 32, fontWeight: '700' },
  input: {
    minHeight: 48,
    borderWidth: 1,
    borderColor: '#6d4936',
    borderRadius: 8,
    padding: 12,
    color: '#302820',
    fontSize: 18,
  },
  button: {
    minHeight: 48,
    backgroundColor: '#6d4936',
    padding: 12,
    borderRadius: 8,
    alignItems: 'center',
  },
  buttonText: { color: '#fff9ed', fontSize: 18 },
});
