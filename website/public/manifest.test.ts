import fs from 'fs';
import path from 'path';

describe('manifest.json', () => {
  const manifest = JSON.parse(
    fs.readFileSync(path.join(__dirname, 'manifest.json'), 'utf-8')
  );

  it('contém name, short_name e display standalone', () => {
    expect(manifest.name).toBe('O Mulet Achou');
    expect(manifest.short_name).toBe('Mulet Achou');
    expect(manifest.display).toBe('standalone');
  });

  it('contém theme_color e background_color esperados', () => {
    expect(manifest.theme_color).toBe('#e63946');
    expect(manifest.background_color).toBe('#ffffff');
  });

  it('contém ícones 192x192 e 512x512', () => {
    const sizes = manifest.icons.map((icon: { sizes: string }) => icon.sizes);
    expect(sizes).toContain('192x192');
    expect(sizes).toContain('512x512');
    manifest.icons.forEach((icon: { type: string }) => {
      expect(icon.type).toBe('image/png');
    });
  });
});
