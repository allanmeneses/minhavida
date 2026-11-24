import { useState } from 'react';
import { Redirect, Slot, usePathname, useRouter } from 'expo-router';
import { Pressable, ScrollView, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { BrandMark } from '@/components/BrandMark';
import { LoadingScreen } from '@/components/LoadingScreen';
import { TourGuide } from '@/components/TourGuide';
import { useAuth } from '@/context/AuthContext';
import { useOffline } from '@/context/OfflineContext';
import { useTheme } from '@/context/ThemeContext';
import { useTour } from '@/context/TourContext';

const NARROW = 768;

type NavEntry = {
  label: string;
  href: string;
  match: (pathname: string) => boolean;
  admin?: boolean;
};

const PRIMARY_NAV: NavEntry[] = [
  { label: 'Hoje', href: '/(app)/home', match: (path) => path.includes('home') || path === '/' },
  { label: 'Círculos', href: '/(app)/circles', match: (path) => path.includes('circle') },
  {
    label: 'Perfil',
    href: '/(app)/profile',
    match: (path) => path.includes('profile'),
  },
];

const MORE_NAV: NavEntry[] = [
  {
    label: 'Tipos',
    href: '/(app)/types',
    match: (path) => path.includes('/types') || path.includes('/type/') || (path.includes('life') && !path.includes('living')),
  },
  { label: 'Busca', href: '/(app)/search', match: (path) => path.includes('search') },
  { label: 'Documentos', href: '/(app)/documents', match: (path) => path.includes('documents') },
  { label: 'Lembretes', href: '/(app)/reminders', match: (path) => path.includes('reminders') },
  { label: 'Calendário', href: '/(app)/calendar', match: (path) => path.includes('calendar') },
  { label: 'Memórias', href: '/(app)/memories', match: (path) => path.includes('memories') },
  { label: 'Modelos', href: '/(app)/templates', match: (path) => path.includes('templates') },
  { label: 'FAQ', href: '/(app)/faq', match: (path) => path.includes('faq') },
  { label: 'Ajustes', href: '/(app)/settings', match: (path) => path.includes('settings') },
  { label: 'Painel interno', href: '/(app)/admin', match: (path) => path.includes('admin'), admin: true },
  { label: 'Preços', href: '/pricing', match: (path) => path.includes('pricing') },
];

export default function AppWebLayout() {
  const { loading, user, logout } = useAuth();
  const { colors } = useTheme();
  const { online } = useOffline();
  const tour = useTour();
  const pathname = usePathname();
  const router = useRouter();
  const { width } = useWindowDimensions();
  const narrow = width < NARROW;
  const [moreOpen, setMoreOpen] = useState(false);

  if (loading || !tour.ready) {
    return <LoadingScreen />;
  }
  if (!user) {
    return <Redirect href="/(auth)/login" />;
  }
  if (!tour.done) {
    return (
      <View style={[styles.shell, { backgroundColor: colors.bg, padding: narrow ? 16 : 32 }]}>
        <TourGuide onSkip={() => void tour.finish()} onFinish={() => void tour.finish()} />
      </View>
    );
  }

  const moreItems = MORE_NAV.filter((item) => !item.admin || user.isSystemAdmin);
  const go = (href: string) => {
    setMoreOpen(false);
    router.push(href as never);
  };

  if (narrow) {
    return (
      <View style={[styles.column, { backgroundColor: colors.bg }]}>
        <View style={[styles.topBar, { backgroundColor: colors.surface, borderBottomColor: colors.line }]}>
          <BrandMark compact />
          <Text
            accessibilityRole="text"
            accessibilityLabel={online ? 'Com rede' : 'Sem rede'}
            style={[styles.topStatus, { color: colors.muted }]}>
            {online ? 'Com rede' : 'Sem rede'}
          </Text>
          <Pressable accessibilityRole="button" accessibilityLabel="Sair" onPress={() => void logout()} style={styles.topLogout}>
            <Text style={[styles.logoutText, { color: colors.primary }]}>Sair</Text>
          </Pressable>
        </View>
        <View style={styles.mainNarrow}>
          <Slot />
        </View>
        {moreOpen ? (
          <View style={styles.moreBackdrop}>
            <Pressable
              accessibilityRole="button"
              accessibilityLabel="Fechar menu"
              onPress={() => setMoreOpen(false)}
              style={[styles.moreScrim, { backgroundColor: 'rgba(31, 26, 22, 0.35)' }]}
            />
            <View style={[styles.moreSheet, { backgroundColor: colors.surface, borderColor: colors.line }]}>
              <Text style={[styles.moreTitle, { color: colors.ink }]}>Mais</Text>
              <ScrollView>
                {moreItems.map((item) => (
                  <NavItem
                    key={item.href}
                    label={item.label}
                    active={item.match(pathname)}
                    onPress={() => go(item.href)}
                  />
                ))}
              </ScrollView>
            </View>
          </View>
        ) : null}
        <View style={[styles.tabBar, { backgroundColor: colors.surface, borderTopColor: colors.line }]}>
          {PRIMARY_NAV.map((item) => (
            <NavItem
              key={item.href}
              label={item.label}
              active={item.match(pathname) && !moreOpen}
              onPress={() => go(item.href)}
              stacked
            />
          ))}
          <NavItem label="Mais" active={moreOpen} onPress={() => setMoreOpen((open) => !open)} stacked />
        </View>
      </View>
    );
  }

  return (
    <View style={[styles.shell, { backgroundColor: colors.bg }]}>
      <View style={[styles.sidebar, { backgroundColor: colors.surface, borderRightColor: colors.line }]}>
        <View style={styles.sidebarTop}>
          <BrandMark compact />
          <Text
            accessibilityRole="text"
            accessibilityLabel={online ? 'Com rede' : 'Sem rede'}
            style={[styles.userEmail, { color: colors.muted, fontWeight: '700' }]}>
            {online ? 'Com rede' : 'Sem rede'}
          </Text>
          <Text style={[styles.userName, { color: colors.ink }]}>{user.name}</Text>
          <Text style={[styles.userEmail, { color: colors.muted }]}>{user.email}</Text>
        </View>
        <ScrollView style={styles.nav} contentContainerStyle={styles.navInner} nestedScrollEnabled>
          {[...PRIMARY_NAV, ...moreItems].map((item) => (
            <NavItem
              key={item.href}
              label={item.label}
              active={item.match(pathname)}
              onPress={() => go(item.href)}
            />
          ))}
        </ScrollView>
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

function NavItem({
  label,
  active,
  onPress,
  stacked = false,
}: {
  label: string;
  active: boolean;
  onPress: () => void;
  stacked?: boolean;
}) {
  const { colors } = useTheme();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      onPress={onPress}
      style={[
        stacked ? styles.tabItem : styles.navItem,
        active && { backgroundColor: colors.primarySoft },
      ]}>
      <Text
        numberOfLines={1}
        style={[
          stacked ? styles.tabLabel : styles.navLabel,
          { color: active ? colors.primaryDark : colors.muted },
        ]}>
        {label}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  shell: {
    flex: 1,
    flexDirection: 'row',
    height: '100vh' as unknown as number,
    maxHeight: '100vh' as unknown as number,
    overflow: 'hidden',
  },
  column: {
    flex: 1,
    minHeight: '100%' as unknown as number,
  },
  sidebar: {
    width: 260,
    borderRightWidth: 1,
    paddingHorizontal: 20,
    paddingTop: 16,
    paddingBottom: 12,
    height: '100%' as unknown as number,
    maxHeight: '100vh' as unknown as number,
    position: 'sticky' as unknown as 'relative',
    top: 0,
    flexShrink: 0,
    overflow: 'hidden',
  },
  sidebarTop: {
    flexShrink: 0,
  },
  topBar: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    borderBottomWidth: 1,
  },
  topStatus: {
    flex: 1,
    fontSize: 12,
    fontWeight: '700',
  },
  topLogout: {
    paddingVertical: 8,
    paddingHorizontal: 8,
  },
  userName: {
    marginTop: 10,
    fontWeight: '700',
    fontSize: 16,
  },
  userEmail: {
    fontSize: 13,
    marginTop: 4,
  },
  nav: {
    marginTop: 10,
    flex: 1,
    minHeight: 0,
  },
  navInner: {
    gap: 2,
    paddingBottom: 4,
  },
  navItem: {
    paddingVertical: 6,
    paddingHorizontal: 12,
    borderRadius: 8,
  },
  navLabel: {
    fontWeight: '600',
  },
  logout: {
    paddingVertical: 8,
    flexShrink: 0,
    marginTop: 4,
  },
  logoutText: {
    fontWeight: '700',
  },
  main: {
    flex: 1,
    minWidth: 0,
    minHeight: 0,
    height: '100%' as unknown as number,
    padding: 32,
    overflow: 'auto' as unknown as 'visible',
  },
  mainNarrow: {
    flex: 1,
    minWidth: 0,
    minHeight: 0,
    paddingHorizontal: 16,
    paddingTop: 16,
    paddingBottom: 8,
    overflow: 'auto' as unknown as 'visible',
  },
  tabBar: {
    flexDirection: 'row',
    borderTopWidth: 1,
    paddingHorizontal: 4,
    paddingVertical: 6,
    gap: 4,
  },
  tabItem: {
    flex: 1,
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 8,
    paddingHorizontal: 4,
  },
  tabLabel: {
    fontWeight: '700',
    fontSize: 12,
  },
  moreBackdrop: {
    ...StyleSheet.absoluteFill,
    justifyContent: 'flex-end',
    paddingBottom: 58,
  },
  moreScrim: {
    ...StyleSheet.absoluteFill,
  },
  moreSheet: {
    maxHeight: '70%',
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    borderWidth: 1,
    padding: 16,
    gap: 8,
  },
  moreTitle: {
    fontWeight: '800',
    fontSize: 18,
    marginBottom: 8,
  },
});
