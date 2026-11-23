import { Platform, StyleSheet, View } from 'react-native';
import { useTheme } from '@/context/ThemeContext';

type Props = {
  uri: string;
  alt: string;
  size?: number;
};

export function LazyPhoto({ uri, alt, size = 96 }: Props) {
  const { colors } = useTheme();
  if (Platform.OS === 'web') {
    return (
      <img
        src={uri}
        alt={alt}
        loading="lazy"
        width={size}
        height={size}
        style={{
          width: size,
          height: size,
          borderRadius: 12,
          objectFit: 'cover',
          backgroundColor: colors.primarySoft,
        }}
      />
    );
  }

  return <View style={[styles.fallback, { width: size, height: size, backgroundColor: colors.primarySoft }]} />;
}

const styles = StyleSheet.create({
  fallback: {
    borderRadius: 12,
  },
});
