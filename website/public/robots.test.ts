import { readFileSync } from 'fs';
import { join } from 'path';

describe('public/robots.txt', () => {
  const content = readFileSync(join(__dirname, 'robots.txt'), 'utf-8');

  it('CA-C6: permite indexação geral (Allow: /, sem bloqueio)', () => {
    expect(content).toMatch(/User-agent:\s*\*/i);
    expect(content).toMatch(/Allow:\s*\//i);
    expect(content).not.toMatch(/Disallow:\s*\/\s*$/im);
  });

  it('CA-C6: referencia o sitemap.xml', () => {
    expect(content).toMatch(/Sitemap:\s*https:\/\/omuletachou\.com\.br\/sitemap\.xml/i);
  });
});
