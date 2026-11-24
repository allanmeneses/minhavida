import { Pressable, StyleSheet, Text, View } from 'react-native';
import { Button } from '@/components/ui/Button';
import { TextField } from '@/components/ui/TextField';
import { useTheme } from '@/context/ThemeContext';
import { addField, renameField, toggleField, type SuggestedFieldDraft } from '@/lib/suggestFields';

type Props = {
  fields: SuggestedFieldDraft[];
  extraName: string;
  onChange: (fields: SuggestedFieldDraft[]) => void;
  onExtraName: (value: string) => void;
};

export function FieldChips({ fields, extraName, onChange, onExtraName }: Props) {
  const { colors } = useTheme();
  return (
    <View style={styles.wrap}>
      <Text style={[styles.lead, { color: colors.muted }]}>Campos sugeridos. Toque para ligar ou desligar. Renomeie se quiser.</Text>
      {fields.map((field) => (
        <View key={field.id} style={[styles.row, { borderColor: colors.line }]}>
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
              {field.enabled ? field.name : `${field.name} (fora)`}
            </Text>
          </Pressable>
          <View style={styles.rename}>
            <TextField
              label={`Nome de ${field.name}`}
              value={field.name}
              onChangeText={(value) => onChange(renameField(fields, field.id, value))}
            />
          </View>
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
    gap: 8,
    borderBottomWidth: 1,
    paddingBottom: 8,
  },
  chip: {
    alignSelf: 'flex-start',
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  rename: {
    flex: 1,
  },
});
