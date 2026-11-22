import { useEffect, useState } from 'react';
import { Image, Platform, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/context/ThemeContext';
import { apiBlob } from '@/lib/api';
import { API_URL } from '@/lib/config';

type Props = {
  name: string;
  hasPhoto: boolean;
  token: string | null;
  revision: number;
};

export function ProfilePhoto({ name, hasPhoto, token, revision }: Props) {
  const { colors } = useTheme();
  const [webUri, setWebUri] = useState<string | null>(null);
  const initials = name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('');

  useEffect(() => {
    if (Platform.OS !== 'web' || !hasPhoto || !token) {
      setWebUri(null);
      return;
    }

    let objectUrl: string | null = null;
    let cancelled = false;
    (async () => {
      try {
        const blob = await apiBlob('/api/profile/photo', token);
        objectUrl = URL.createObjectURL(blob);
        if (!cancelled) {
          setWebUri(objectUrl);
        } else {
          URL.revokeObjectURL(objectUrl);
        }
      } catch {
        if (!cancelled) {
          setWebUri(null);
        }
      }
    })();

    return () => {
      cancelled = true;
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [hasPhoto, token, revision]);

  if (!hasPhoto || !token) {
    return (
      <View style={[styles.placeholder, { backgroundColor: colors.primary }]}>
        <Text style={[styles.initials, { color: colors.white }]}>{initials || '?'}</Text>
      </View>
    );
  }

  if (Platform.OS === 'web') {
    if (!webUri) {
      return (
        <View style={[styles.placeholder, { backgroundColor: colors.primary }]}>
          <Text style={[styles.initials, { color: colors.white }]}>{initials || '?'}</Text>
        </View>
      );
    }
    return <Image source={{ uri: webUri }} style={[styles.photo, { backgroundColor: colors.primarySoft }]} />;
  }

  return (
    <Image
      source={{
        uri: `${API_URL}/api/profile/photo?r=${revision}`,
        headers: { Authorization: `Bearer ${token}` },
      }}
      style={[styles.photo, { backgroundColor: colors.primarySoft }]}
    />
  );
}

const styles = StyleSheet.create({
  photo: {
    width: 96,
    height: 96,
    borderRadius: 48,
  },
  placeholder: {
    width: 96,
    height: 96,
    borderRadius: 48,
    alignItems: 'center',
    justifyContent: 'center',
  },
  initials: {
    fontSize: 28,
    fontWeight: '800',
  },
});
