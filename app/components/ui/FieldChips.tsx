import { useState } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { Button } from '@/components/ui/Button';
import { TextField } from '@/components/ui/TextField';
import { useTheme } from '@/context/ThemeContext';
import { addField, fieldKindLabel, renameField, toggleField, type SuggestedFieldDraft } from '@/lib/suggestFields';

type Props = {
  fields: SuggestedFieldDraft[];
  extraName: string;
  onChange: (fields: SuggestedFieldDraft[]) => void;
  onExtraName: (value: string) => void;
};

export function FieldChips({ fields, extraName, onChange, onExtraName }: Props) {
  const { colors } = useTheme();
  const [editingId, setEditingId] = useState<string | null>(null);

  return (
    <View style={styles.wrap}>
      <Text style={[styles.lead, { color: colors.muted }]}>
        Campos sugeridos. Toque para ligar ou desligar. O lápis renomeia na hora.
      </Text>
      {fields.map((field) => (
        <View key={field.id} style={styles.row}>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={field.enabled ? `Campo ativo: ${field.name}` : `Campo desligado: ${field.name}`}
            onPress={() => onChange(toggleField(fields, field.id))}
            style={[
              styles.chip,
              {
                backgroundColor: field.enabled ? colors.primarySoft : colors.surface,
                borderColor: colors.line,
              },
            ]}>
            <Text style={{ color: field.enabled ? colors.primaryDark : colors.muted, fontWeight: '700' }}>
              {field.name}
            </Text>
          </Pressable>
          <Text style={[styles.kind, { color: colors.muted }]}>{fieldKindLabel(field)}</Text>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={`Renomear ${field.name}`}
            onPress={() => setEditingId((current) => (current === field.id ? null : field.id))}
            style={[styles.pencil, { borderColor: colors.line }]}>
            <Text style={{ color: colors.ink, fontWeight: '700' }}>✎</Text>
          </Pressable>
          {editingId === field.id ? (
            <TextInput
              value={field.name}
              onChangeText={(value) => onChange(renameField(fields, field.id, value))}
              onBlur={() => setEditingId(null)}
              autoFocus
              accessibilityLabel="Novo nome"
              placeholder="Novo nome"
              placeholderTextColor={colors.placeholder}
              style={[
                styles.rename,
                {
                  borderColor: colors.line,
                  backgroundColor: colors.white,
                  color: colors.ink,
                },
              ]}
            />
          ) : null}
        </View>
      ))}
      <TextField label="Novo campo" value={extraName} onChangeText={onExtraName} placeholder="Ex.: Distância" />
      <Button
        label="Adicionar campo"
        variant="ghost"
        disabled={!extraName.trim()}
        onPress={() => {
          onChange(addField(fields, extraName.trim()));
          onExtraName('');
        }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    gap: 10,
  },
  lead: {
    lineHeight: 20,
  },
  row: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: 8,
  },
  chip: {
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  kind: {
    fontSize: 12,
    fontWeight: '600',
  },
  pencil: {
    minWidth: 36,
    minHeight: 36,
    borderWidth: 1,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
  },
  rename: {
    minHeight: 42,
    minWidth: 160,
    flexGrow: 1,
    borderWidth: 1,
    borderRadius: 8,
    paddingHorizontal: 10,
    fontSize: 14,
  },
});
