import { spawnSync } from 'node:child_process';

import { parseMobileEnvironment } from '../src/config/environment.ts';

// Run with the same public environment as Metro; no API, database or login secret is used.
try {
  const config = parseMobileEnvironment(
    process.env.EXPO_PUBLIC_APP_ENV,
    process.env.EXPO_PUBLIC_API_BASE_URL,
  );
  const url = new URL(config.healthUrl);
  if (
    !['emulator', 'device'].includes(config.profile) ||
    url.protocol !== 'http:'
  ) {
    throw new Error(
      'This Android probe supports local HTTP emulator/device profiles only. Use the HTTPS procedure in the setup guide for remote environments.',
    );
  }
  const serial = process.env.ANDROID_SERIAL;
  const result = spawnSync(
    process.env.ADB_PATH || 'adb',
    [
      ...(serial ? ['-s', serial] : []),
      'shell',
      "(printf 'GET /health HTTP/1.0\\r\\nHost: " +
        url.host +
        "\\r\\nConnection: close\\r\\n\\r\\n'; sleep 4) | toybox nc -4 -w 10 -W 10 " +
        url.hostname +
        ' ' +
        (url.port || '80'),
    ],
    {
      encoding: 'utf8',
      timeout: 15000,
    },
  );
  if (
    result.error ||
    result.status !== 0 ||
    !/^HTTP\/1\.[01] 200\b/.test(result.stdout) ||
    result.stdout
      .replace(/\r/g, '')
      .split('\n\n')
      .slice(1)
      .join('\n')
      .trim() !== 'Healthy'
  ) {
    throw new Error(
      'Android /health probe failed. Check the device, API listener and network setup; no response body is logged.',
    );
  }
  console.log(
    'Android ' +
      (serial || '(single connected device)') +
      ': GET ' +
      config.healthUrl +
      ' -> 200 Healthy',
  );
} catch (error) {
  console.error(
    error instanceof Error
      ? error.message
      : 'Android connectivity check failed.',
  );
  process.exitCode = 1;
}
