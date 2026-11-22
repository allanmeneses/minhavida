import { Redirect, Slot, usePathname, useRouter } from 'expo-router';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { BrandMark } from '@/components/BrandMark';
import { LoadingScreen } from '@/components/LoadingScreen';
import { colors } from '@/constants/theme';
import { useAuth } from '@/context/AuthContext';

export default function AppWebLayout() {
  const { loading, user, logout } = useAuth();
  const pathname = usePathname();
  const router = useRouter();

  if (loading) {
    return <LoadingScreen />;
  }
  if (!user) {
    return <Redirect href="/(auth)/login" />;
  }

  return (
    <View style={styles.shell}>
      <View style={styles.sidebar}>
        <BrandMark compact />
        <Text style={styles.userName}>{user.name}</Text>
        <Text style={styles.userEmail}>{user.email}</Text>
        <View style={styles.nav}>
          <NavItem
            label="Perfil"
            active={pathname.includes('profile')}
            onPress={() => router.push('/(app)/profile')}
          />
          <NavItem
            label="Documentos"
            active={pathname.includes('documents')}
            onPress={() => router.push('/(app)/documents')}
          />
        </View>
        <Pressable onPress={() => void logout()} style={styles.logout}>
          <Text style={styles.logoutText}>Sair</Text>
        </Pressable>
      </View>
      <View style={styles.main}>
        <Slot />
      </View>
    </View>
  );
}

function NavItem({ label, active, onPress }: { label: string; active: boolean; onPress: () => void }) {
  return (
    <Pressable onPress={onPress} style={[styles.navItem, active && styles.navItemActive]}>
      <Text style={[styles.navLabel, active && styles.navLabelActive]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  shell: {
    flex: 1,
    flexDirection: 'row',
    minHeight: '100%' as unknown as number,
    backgroundColor: colors.bg,
  },
  sidebar: {
    width: 260,
    backgroundColor: colors.surface,
    borderRightWidth: 1,
    borderRightColor: colors.line,
    padding: 24,
  },
  userName: {
    marginTop: 28,
    fontWeight: '700',
    color: colors.ink,
    fontSize: 16,
  },
  userEmail: {
    color: colors.muted,
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
  navItemActive: {
    backgroundColor: colors.primarySoft,
  },
  navLabel: {
    color: colors.muted,
    fontWeight: '600',
  },
  navLabelActive: {
    color: colors.primaryDark,
  },
  logout: {
    paddingVertical: 10,
  },
  logoutText: {
    color: colors.primary,
    fontWeight: '700',
  },
  main: {
    flex: 1,
    padding: 32,
  },
});
