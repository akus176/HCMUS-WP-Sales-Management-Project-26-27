import type { ConfigContext } from 'expo/config';
import { withAndroidManifest } from 'expo/config-plugins';

import { parseMobileEnvironment } from './src/config/environment.ts';

export default ({ config }: ConfigContext) => {
  const environment = parseMobileEnvironment(
    process.env.EXPO_PUBLIC_APP_ENV,
    process.env.EXPO_PUBLIC_API_BASE_URL,
    process.env.EAS_BUILD_PROFILE === 'production',
  );
  return withAndroidManifest(
    {
      ...config,
      name: config.name ?? 'Coffee Shop Staff',
      slug: config.slug ?? 'coffee-shop-staff',
    },
    (nativeConfig) => {
      const application = nativeConfig.modResults.manifest.application?.[0];
      if (!application)
        throw new Error('Android application configuration is missing.');
      application.$['android:usesCleartextTraffic'] = environment.allowCleartext
        ? 'true'
        : 'false';
      return nativeConfig;
    },
  );
};
