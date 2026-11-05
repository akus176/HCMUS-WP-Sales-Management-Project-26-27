import {
  configurationError,
  parseMobileEnvironment,
} from '../src/config/environment';

test.each([
  ['emulator', 'http://10.0.2.2:5232/api/v1'],
  ['device', 'http://192.168.1.20:5232/api/v1/'],
  ['device', 'http://172.16.0.2:5232/api/v1'],
  ['test', 'http://127.0.0.1:5232/api/v1'],
  ['test', 'http://[::1]:5232/api/v1'],
  ['test', 'https://test.example.com/api/v1'],
  ['release', 'https://api.example.com/api/v1/'],
])('accepts the %s profile with a valid non-secret URL', (profile, base) => {
  const config = parseMobileEnvironment(profile, base);
  expect(config.apiBaseUrl).toBe(new URL(base).origin + '/api/v1');
  expect(config.healthUrl).toBe(new URL(base).origin + '/health');
  expect(Object.isFrozen(config)).toBe(true);
});

test.each([
  [undefined, undefined],
  ['', 'https://api.example.com/api/v1'],
  ['staging', 'https://api.example.com/api/v1'],
  ['emulator', undefined],
  ['emulator', ''],
  ['emulator', 'not-a-url'],
  ['emulator', 'http://localhost:5232/api/v1'],
  ['emulator', 'http://example.com/api/v1'],
  ['device', 'http://localhost:5232/api/v1'],
  ['device', 'http://10.0.2.2:5232/api/v1'],
  ['device', 'http://8.8.8.8/api/v1'],
  ['test', 'http://example.com/api/v1'],
  ['release', 'http://api.example.com/api/v1'],
  ['release', 'https://localhost/api/v1'],
  ['release', 'https://192.168.1.20/api/v1'],
  ['release', 'https://[::1]/api/v1'],
  ['release', 'https://shop.local/api/v1'],
  ['release', 'https://localhost./api/v1'],
  ['release', 'https://api..example.com/api/v1'],
  ['test', 'http://127.example.com/api/v1'],
  ['test', 'ftp://localhost/api/v1'],
  ['test', 'https://api.example.com/wrong'],
  ['test', 'https://api.example.com/api/v1/../v1'],
  ['test', 'https://api.example.com/api/v1?'],
  ['test', 'https://api.example.com/api/v1#'],
  ['test', ' https://api.example.com/api/v1'],
  ['test', 'https://api.example.com/api/v1\\'],
  ['test', 'https://api.example.com/api/v1?token=private-value'],
  ['test', 'https://staff:private-value@api.example.com/api/v1'],
])(
  'rejects missing, malformed or unsafe %s configuration without echoing it',
  (profile, base) => {
    expect(() => parseMobileEnvironment(profile, base)).toThrow(
      configurationError,
    );
    try {
      parseMobileEnvironment(profile, base);
    } catch (error) {
      expect(String(error)).not.toContain('private-value');
      expect((error as Error).cause).toBeUndefined();
    }
  },
);

test('a release bundle cannot select a development profile', () => {
  expect(() =>
    parseMobileEnvironment('test', 'https://api.example.com/api/v1', true),
  ).toThrow(configurationError);
  expect(
    parseMobileEnvironment('release', 'https://api.example.com/api/v1', true)
      .allowCleartext,
  ).toBe(false);
});
