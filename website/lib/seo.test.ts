import {
  buildDealTitle,
  buildDealDescription,
  buildDealCanonicalUrl,
  resolveDealOgImage,
  buildDealJsonLd,
} from './seo';
import type { Deal } from './types';

function buildDeal(overrides: Partial<Deal> = {}): Deal {
  return {
    title: 'Fone Bluetooth XYZ',
    salePrice: 99.9,
    originalPrice: 149.9,
    discountPct: 33,
    affiliateLink: 'https://amazon.com/xyz?tag=abc',
    mediaUrl: 'https://cdn.example.com/xyz.jpg',
    mediaLocalPath: null,
    slug: 'fone-bluetooth-xyz',
    category: 'eletronicos',
    collectedAt: '2026-07-01T12:00:00Z',
    platform: 'Amazon',
    ...overrides,
  };
}

describe('lib/seo', () => {
  describe('buildDealTitle', () => {
    it('CA-B5: monta title com nome do produto e sufixo do site', () => {
      expect(buildDealTitle(buildDeal())).toBe('Fone Bluetooth XYZ | O Mulet Achou');
    });
  });

  describe('buildDealDescription', () => {
    it('CA-B5: monta description a partir dos dados do produto', () => {
      const description = buildDealDescription(buildDeal());
      expect(description).toContain('Fone Bluetooth XYZ');
      expect(description.length).toBeLessThanOrEqual(160);
    });

    it('trunca descrições muito longas', () => {
      const description = buildDealDescription(
        buildDeal({ title: 'Produto com nome extremamente longo '.repeat(6) })
      );
      expect(description.length).toBeLessThanOrEqual(160);
      expect(description.endsWith('…')).toBe(true);
    });
  });

  describe('buildDealCanonicalUrl', () => {
    it('CA-B6: monta a URL canônica da oferta', () => {
      expect(buildDealCanonicalUrl(buildDeal())).toBe(
        'https://omuletachou.com.br/oferta/fone-bluetooth-xyz'
      );
    });
  });

  describe('resolveDealOgImage', () => {
    it('CA-B6: usa mediaUrl quando presente', () => {
      expect(resolveDealOgImage(buildDeal())).toBe('https://cdn.example.com/xyz.jpg');
    });

    it('CA-B6: usa mediaLocalPath quando mediaUrl ausente', () => {
      expect(
        resolveDealOgImage(buildDeal({ mediaUrl: null, mediaLocalPath: 'http://api:8080/media/x.jpg' }))
      ).toBe('http://api:8080/media/x.jpg');
    });

    it('CA-B8: usa /og-default.png quando ambas as mídias estão ausentes', () => {
      expect(resolveDealOgImage(buildDeal({ mediaUrl: null, mediaLocalPath: null }))).toBe(
        '/og-default.png'
      );
    });
  });

  describe('buildDealJsonLd', () => {
    it('CA-B9: monta Schema.org Product com Offer aninhado', () => {
      const jsonLd = buildDealJsonLd(buildDeal());

      expect(jsonLd['@context']).toBe('https://schema.org');
      expect(jsonLd['@type']).toBe('Product');
      expect(jsonLd.name).toBe('Fone Bluetooth XYZ');
      expect(jsonLd.image).toContain('https://cdn.example.com/xyz.jpg');
      expect(jsonLd.offers['@type']).toBe('Offer');
      expect(jsonLd.offers.price).toBe(99.9);
      expect(jsonLd.offers.priceCurrency).toBe('BRL');
      expect(jsonLd.offers.availability).toBe('https://schema.org/InStock');
    });
  });
});
