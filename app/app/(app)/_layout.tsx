import { Redirect, Tabs } from 'expo-router';
import { Pressable, Text } from 'react-native';
import { SymbolView } from 'expo-symbols';
import { LoadingScreen } from '@/components/LoadingScreen';
import { colors } from '@/constants/theme';
import { useAuth } from '@/context/AuthContext';

export default function AppLayout() {
  const { loading, user, logout } = useAuth();
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
          <Pressable onPress={() => void logout()} style={{ marginRight: 16, paddingVertical: 8 }}>
            <Text style={{ color: colors.primary, fontWeight: '700' }}>Sair</Text>
          </Pressable>
        ),
      }}>
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
      <Tabs.Screen
        name="documents"
        options={{
          title: 'Documentos',
          tabBarLabel: 'Documentos',
          tabBarIcon: ({ color }) => (
            <SymbolView
              name={{ ios: 'folder', android: 'folder', web: 'folder' }}
              tintColor={color}
              size={26}
            />
          ),
        }}
      />
    </Tabs>
  );
}
