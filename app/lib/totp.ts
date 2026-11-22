function base32ToBytes(secret: string): Uint8Array {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  const cleaned = secret.replace(/[^A-Z2-7]/gi, '').toUpperCase();
  let buffer = 0;
  let bits = 0;
  const out: number[] = [];
  for (const ch of cleaned) {
    const index = alphabet.indexOf(ch);
    if (index < 0) {
      continue;
    }
    buffer = (buffer << 5) | index;
    bits += 5;
    if (bits >= 8) {
      out.push((buffer >> (bits - 8)) & 0xff);
      bits -= 8;
    }
  }
  return Uint8Array.from(out);
}

export async function totpCode(secret: string, nowMs = Date.now()): Promise<string> {
  const counter = Math.floor(nowMs / 1000 / 30);
  const payload = new Uint8Array(8);
  let value = counter;
  for (let i = 7; i >= 0; i -= 1) {
    payload[i] = value & 0xff;
    value = Math.floor(value / 256);
  }
  const secretBytes = base32ToBytes(secret);
  const keyData = new ArrayBuffer(secretBytes.byteLength);
  new Uint8Array(keyData).set(secretBytes);
  const key = await crypto.subtle.importKey('raw', keyData, { name: 'HMAC', hash: 'SHA-1' }, false, [
    'sign',
  ]);
  const hash = new Uint8Array(await crypto.subtle.sign('HMAC', key, payload));
  const offset = hash[hash.length - 1] & 0x0f;
  const binary =
    ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
  return String(binary % 1_000_000).padStart(6, '0');
}
