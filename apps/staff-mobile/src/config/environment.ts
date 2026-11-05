export type MobileProfile = 'emulator' | 'device' | 'test' | 'release';
export type MobileEnvironment = Readonly<{
  profile: MobileProfile;
  apiBaseUrl: string;
  apiOrigin: string;
  healthUrl: string;
  allowCleartext: boolean;
}>;

const profiles = ['emulator', 'device', 'test', 'release'] as const;
export const configurationError =
  'Mobile configuration is invalid. Set EXPO_PUBLIC_APP_ENV and EXPO_PUBLIC_API_BASE_URL using the environment setup guide.';

function isPrivateIpv4(host: string) {
  const parts = host.split('.').map(Number);
  return (
    parts.length === 4 &&
    parts.every((part) => Number.isInteger(part) && part >= 0 && part <= 255) &&
    (parts[0] === 10 ||
      (parts[0] === 172 && parts[1]! >= 16 && parts[1]! <= 31) ||
      (parts[0] === 192 && parts[1] === 168))
  );
}

function isLoopback(host: string) {
  return (
    host === 'localhost' ||
    host === '[::1]' ||
    /^127\.\d+\.\d+\.\d+$/.test(host)
  );
}

// A release address must be an explicit public DNS name, never a local IP or alias.
function isPublicHost(host: string) {
  return (
    host.includes('.') &&
    host
      .split('.')
      .every((label) => /^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/.test(label)) &&
    !host.endsWith('.') &&
    !host.includes(':') &&
    !/^[\d.]+$/.test(host) &&
    ![
      'localhost',
      'local',
      'internal',
      'test',
      'invalid',
      'lan',
      'home',
      'home.arpa',
    ].some((suffix) => host === suffix || host.endsWith('.' + suffix))
  );
}

export function validateApiUrl(value: string | undefined) {
  try {
    if (!value || value !== value.trim() || /[\s\\]/.test(value))
      throw new Error();
    const url = new URL(value);
    if (
      !['http:', 'https:'].includes(url.protocol) ||
      url.username ||
      url.password ||
      url.search ||
      url.hash ||
      /[?#]/.test(value) ||
      !/^https?:\/\/[^/]+\/api\/v1\/?$/.test(value) ||
      url.pathname.replace(/\/$/, '') !== '/api/v1'
    )
      throw new Error();
    return url;
  } catch {
    // Never include the supplied URL, parser cause or credentials in diagnostics.
    throw new Error(configurationError);
  }
}

export function parseMobileEnvironment(
  profile: string | undefined,
  baseUrl: string | undefined,
  releaseBuild = false,
): MobileEnvironment {
  try {
    if (!profiles.includes(profile as MobileProfile)) throw new Error();
    const url = validateApiUrl(baseUrl);
    if (releaseBuild && profile !== 'release') throw new Error();
    if (
      profile === 'release' &&
      (url.protocol !== 'https:' || !isPublicHost(url.hostname))
    )
      throw new Error();
    if (url.protocol === 'http:') {
      const allowed =
        profile === 'emulator'
          ? url.hostname === '10.0.2.2'
          : profile === 'device'
            ? isPrivateIpv4(url.hostname) && url.hostname !== '10.0.2.2'
            : profile === 'test'
              ? isLoopback(url.hostname) || isPrivateIpv4(url.hostname)
              : false;
      if (!allowed) throw new Error();
    }
    if (
      profile === 'device' &&
      (isLoopback(url.hostname) || url.hostname === '10.0.2.2')
    )
      throw new Error();
    return Object.freeze({
      profile: profile as MobileProfile,
      apiBaseUrl: url.origin + '/api/v1',
      apiOrigin: url.origin,
      healthUrl: url.origin + '/health',
      allowCleartext: url.protocol === 'http:',
    });
  } catch {
    throw new Error(configurationError);
  }
}

export function readMobileEnvironment() {
  // Expo only substitutes direct EXPO_PUBLIC_* property access in native bundles.
  return parseMobileEnvironment(
    process.env.EXPO_PUBLIC_APP_ENV,
    process.env.EXPO_PUBLIC_API_BASE_URL,
    !__DEV__,
  );
}
