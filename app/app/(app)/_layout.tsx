import { Redirect, Tabs } from 'expo-router';
import { Platform, Pressable, Text } from 'react-native';
import { SymbolView } from 'expo-symbols';
import { LoadingScreen } from '@/components/LoadingScreen';
import { useAuth } from '@/context/AuthContext';
import { useTheme } from '@/context/ThemeContext';

export default function AppLayout() {
  const { loading, user, logout } = useAuth();
  const { colors } = useTheme();
  if (loading) {
    return <LoadingScreen />;
  }
  if (!user) {
    return <Redirect href="/(auth)/login" />;
  }

  return (
    <Tabs
      screenOptions={{
        headerStyle: { backgroundColor: colors.surface },
        headerTintColor: colors.ink,
        headerTitleStyle: { fontWeight: '700' },
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.muted,
        tabBarStyle: { backgroundColor: colors.surface, borderTopColor: colors.line },
        headerRight: () => (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Sair"
            onPress={() => void logout()}
            style={{
              marginRight: Platform.OS === 'web' ? 16 : 72,
              paddingVertical: 8,
              paddingHorizontal: 12,
            }}>
            <Text style={{ color: colors.primary, fontWeight: '700' }}>Sair</Text>
          </Pressable>
        ),
      }}>
      <Tabs.Screen
        name="home"
        options={{
          title: 'Hoje',
          tabBarLabel: 'Hoje',
          tabBarIcon: ({ color }) => (
            <SymbolView
              name={{ ios: 'sun.max', android: 'wb_sunny', web: 'sunny' }}
              tintColor={color}
              size={26}
            />
          ),
        }}
      />
      <Tabs.Screen
        name="circles"
        options={{
          title: 'Circulos',
          tabBarLabel: 'Circulos',
          tabBarIcon: ({ color }) => (
            <SymbolView
              name={{ ios: 'circle.grid.3x3', android: 'group', web: 'group' }}
              tintColor={color}
              size={26}
            />
          ),
        }}
      />
      <Tabs.Screen
        name="profile"
        options={{
          title: 'Meu perfil',
          tabBarLabel: 'Perfil',
          tabBarIcon: ({ color }) => (
            <SymbolView
              name={{ ios: 'person.crop.circle', android: 'person', web: 'person' }}
              tintColor={color}
              size={26}
            />
          ),
        }}
      />
      <Tabs.Screen name="index" options={{ href: null }} />
      <Tabs.Screen
        name="documents"
        options={{ title: 'Documentos', tabBarButton: () => null, tabBarItemStyle: { display: 'none' } }}
      />
      <Tabs.Screen
        name="reminders"
        options={{ title: 'Lembretes', tabBarButton: () => null, tabBarItemStyle: { display: 'none' } }}
      />
      <Tabs.Screen
        name="settings"
        options={{ title: 'Ajustes', tabBarButton: () => null, tabBarItemStyle: { display: 'none' } }}
      />
      <Tabs.Screen
        name="life/[id]"
        options={{ title: 'Area', tabBarButton: () => null, tabBarItemStyle: { display: 'none' } }}
      />
      <Tabs.Screen
        name="circle/[id]"
        options={{ title: 'Circulo', tabBarButton: () => null, tabBarItemStyle: { display: 'none' } }}
      />
    </Tabs>
  );
}
