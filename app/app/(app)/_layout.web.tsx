import { Redirect, Slot, usePathname, useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { BrandMark } from '@/components/BrandMark';
import { LoadingScreen } from '@/components/LoadingScreen';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';

export default function AppWebLayout() {
  const { loading, user, logout } = useAuth();
  const { colors } = useTheme();
  const pathname = usePathname();
  const router = useRouter();

  if (loading) {
    return <LoadingScreen />;
  }
  if (!user) {
    return <Redirect href="/(auth)/login" />;
  }

  return (
    <View style={[styles.shell, { backgroundColor: colors.bg }]}>
      <View style={[styles.sidebar, { backgroundColor: colors.surface, borderRightColor: colors.line }]}>
        <BrandMark compact />
        <Text style={[styles.userName, { color: colors.ink }]}>{user.name}</Text>
        <Text style={[styles.userEmail, { color: colors.muted }]}>{user.email}</Text>
        <View style={styles.nav}>
          <NavItem label="Hoje" active={pathname.includes('home') || pathname === '/'} onPress={() => router.push('/(app)/home')} />
          <NavItem label="Circulos" active={pathname.includes('circle')} onPress={() => router.push('/(app)/circles')} />
          <NavItem label="Perfil" active={pathname.includes('profile') || pathname.includes('life')} onPress={() => router.push('/(app)/profile')} />
          <NavItem label="Documentos" active={pathname.includes('documents')} onPress={() => router.push('/(app)/documents')} />
          <NavItem label="Lembretes" active={pathname.includes('reminders')} onPress={() => router.push('/(app)/reminders')} />
          <NavItem label="Ajustes" active={pathname.includes('settings')} onPress={() => router.push('/(app)/settings')} />
        </View>
        <Pressable accessibilityRole="button" accessibilityLabel="Sair" onPress={() => void logout()} style={styles.logout}>
          <Text style={[styles.logoutText, { color: colors.primary }]}>Sair</Text>
        </Pressable>
      </View>
      <View style={styles.main}>
        <Slot />
      </View>
    </View>
  );
}

function NavItem({ label, active, onPress }: { label: string; active: boolean; onPress: () => void }) {
  const { colors } = useTheme();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      onPress={onPress}
      style={[styles.navItem, active && { backgroundColor: colors.primarySoft }]}>
      <Text style={[styles.navLabel, { color: active ? colors.primaryDark : colors.muted }]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  shell: {
    flex: 1,
    flexDirection: 'row',
    minHeight: '100%' as unknown as number,
  },
  sidebar: {
    width: 260,
    borderRightWidth: 1,
    padding: 24,
  },
  userName: {
    marginTop: 28,
    fontWeight: '700',
    fontSize: 16,
  },
  userEmail: {
    fontSize: 13,
    marginTop: 4,
  },
  nav: {
    marginTop: 28,
    gap: 6,
    flex: 1,
  },
  navItem: {
    paddingVertical: 10,
    paddingHorizontal: 12,
    borderRadius: 8,
  },
  navLabel: {
    fontWeight: '600',
  },
  logout: {
    paddingVertical: 10,
  },
  logoutText: {
    fontWeight: '700',
  },
  main: {
    flex: 1,
    padding: 32,
  },
});
