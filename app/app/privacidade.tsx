import { ScrollView, Text } from 'react-native';
import { useRouter, type Href } from 'expo-router';
import { Button } from '@/components/ui/Button';
import { useTheme } from '@/context/ThemeContext';
import { screenStyles } from '@/lib/screenStyles';

export default function PrivacyScreen() {
  const { colors } = useTheme();
  const styles = screenStyles(colors);
  const router = useRouter();
  return (
    <ScrollView contentContainerStyle={styles.page}>
      <Text style={styles.kicker}>Círculos</Text>
      <Text style={styles.title}>Política de privacidade</Text>
      <Text style={styles.lead}>Última atualização: 22 de novembro de 2026.</Text>
      <Text style={styles.lead}>
        Controlador: ALLAN WANDERSON MENESES DE SOUSA. Esta política segue a Lei Geral de Proteção de
        Dados (Lei 13.709/2018) e descreve só o que o Círculos realmente trata hoje.
      </Text>
      <Text style={styles.section}>1. Quais dados</Text>
      <Text style={styles.lead}>
        Conta: e-mail, senha com hash, nome. Perfil: nome de exibição, data de nascimento, telefone,
        e-mail secundário, foto, se o perfil é público. Documentos, fotos e registros que você descreve.
        Círculos, membros, convites, pedidos de entrada, publicações, log e listas (itens, quem adicionou e quem marcou). Lembretes, atalhos e histórico de busca. E-mails de recuperação de
        senha, convite e exclusão de conta (hoje gravados num outbox local, sem SMTP de produção). O áudio da
        captura sobe só para transcrever e é apagado em seguida. Nada é gravado em silêncio.
      </Text>
      <Text style={styles.section}>2. Para que</Text>
      <Text style={styles.lead}>
        Executar o serviço que você pediu: autenticar, guardar a vida pessoal, compartilhar no círculo,
        lembrar datas e atalhos, buscar o que você registrou. Base legal: execução do contrato (art. 7,
        V) e consentimento geral no cadastro, revogável pela exclusão da conta (perguntas 61 a 66 da
        ligação, alinhadas a LGPD).
      </Text>
      <Text style={styles.section}>3. Compartilhamento</Text>
      <Text style={styles.lead}>
        Membros do círculo veem o que o círculo permite (nome, descrição, entradas, log). Círculos
        marcados para busca podem aparecer para outras contas pelo nome e descrição. Não vendemos dados
        a terceiros. Não há anúncios. Não há painel de transparência do que foi coletado (pergunta 69).
      </Text>
      <Text style={styles.section}>4. Segurança</Text>
      <Text style={styles.lead}>
        Documentos e fotos ficam cifrados em AES-256-GCM no servidor, com chave do app, não escolhida
        pela pessoa. Foto de entrada e de perfil é comprimida no padrão usado pelo app e substitui o
        original. Download só autenticado. O token JWT fica no aparelho (armazenamento seguro no nativo,
        localStorage na web). Se o app encerrar, não há exportação do material cifrado em formato que
        você mesmo abra (escolha de propósito).
      </Text>
      <Text style={styles.section}>5. Seus direitos</Text>
      <Text style={styles.lead}>
        Você pode corrigir o perfil e as entradas. Pode exportar o diário em texto corrido. Pode apagar
        foto, documento, atalho, entrada e a conta. A exclusão da conta pede senha e confirmação por
        e-mail, apaga o que é só seu e mantém rastro anônimo no círculo (quem apagou some, o fato de
        que existiu um evento pode permanecer). Círculos que você criou passam a um administrador ou
        sucessor; se não houver, o círculo trava sem dono.
      </Text>
      <Text style={styles.section}>6. Retenção e busca</Text>
      <Text style={styles.lead}>
        Os dados ficam enquanto a conta existir. O histórico de busca é gravado na sua conta para
        sincronizar entre aparelhos e sugerir termos frequentes. O log do círculo é visível aos membros,
        com data e hora no fuso do aparelho.
      </Text>
      <Text style={styles.section}>7. Contato</Text>
      <Text style={styles.lead}>
        Para exercer direitos de titular, escreva ao responsável ALLAN WANDERSON MENESES DE SOUSA pelo
        e-mail da conta de cadastro, usando o fluxo de exclusão no app quando quiser apagar a conta.
        Não há CNPJ neste documento.
      </Text>
      <Button label="Termos de uso" variant="ghost" onPress={() => router.push('/termos' as Href)} />
      <Button label="Voltar" variant="ghost" onPress={() => router.back()} />
    </ScrollView>
  );
}
