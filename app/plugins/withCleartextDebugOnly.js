const { withAndroidManifest, withDangerousMod, AndroidConfig } = require('@expo/config-plugins');
const fs = require('fs');
const path = require('path');

const DEBUG_MANIFEST = `<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
    xmlns:tools="http://schemas.android.com/tools">
  <application
      android:usesCleartextTraffic="true"
      tools:targetApi="28" />
</manifest>
`;

function withCleartextDebugOnly(config) {
  config = withAndroidManifest(config, (config) => {
    const app = AndroidConfig.Manifest.getMainApplicationOrThrow(config.modResults);
    app.$['android:usesCleartextTraffic'] = 'false';
    return config;
  });

  config = withDangerousMod(config, [
    'android',
    async (config) => {
      const dir = path.join(config.modRequest.platformProjectRoot, 'app/src/debug');
      await fs.promises.mkdir(dir, { recursive: true });
      await fs.promises.writeFile(path.join(dir, 'AndroidManifest.xml'), DEBUG_MANIFEST, 'utf8');
      return config;
    },
  ]);

  return config;
}

module.exports = withCleartextDebugOnly;
module.exports.DEBUG_MANIFEST = DEBUG_MANIFEST;
