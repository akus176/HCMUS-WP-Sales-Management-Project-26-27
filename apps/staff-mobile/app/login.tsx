import { useRef, useState, type ComponentRef } from 'react';
import { TextInput } from 'react-native';

import { ApiError, NetworkError } from '../src/api/errors';
import { useSession } from '../src/session/context';
import {
  ShellButton,
  ShellFeedback,
  ShellHeading,
  ShellInput,
  ShellPage,
} from '../src/shell/controls';

export default function LoginScreen() {
  const { client } = useSession();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const passwordInput = useRef<ComponentRef<typeof TextInput>>(null);
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
    <ShellPage>
      <ShellHeading>Staff sign-in</ShellHeading>
      {!client ? (
        <ShellFeedback
          kind="error"
          label="Sign-in is not configured. Contact your administrator."
        />
      ) : (
        <>
          <ShellInput
            label="Username"
            accessibilityLabel="Username"
            placeholder="Username"
            autoCapitalize="none"
            autoCorrect={false}
            autoComplete="username"
            maxLength={50}
            editable={!busy}
            value={username}
            onChangeText={setUsername}
            returnKeyType="next"
            onSubmitEditing={() => passwordInput.current?.focus()}
          />
          <ShellInput
            ref={passwordInput}
            label="Password"
            accessibilityLabel="Password"
            placeholder="Password"
            secureTextEntry
            autoComplete="current-password"
            maxLength={1024}
            editable={!busy}
            value={password}
            onChangeText={setPassword}
            onSubmitEditing={() => {
              void submit();
            }}
          />
          {!!error && <ShellFeedback kind="error" label={error} />}
          <ShellButton
            label="Sign in"
            loading={busy}
            loadingLabel="Signing in…"
            onPress={() => {
              void submit();
            }}
          />
        </>
      )}
    </ShellPage>
  );
}
