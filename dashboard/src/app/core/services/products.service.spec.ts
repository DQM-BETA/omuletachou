import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';

import { ProductsService, ProductDetail } from './products.service';

describe('ProductsService', () => {
  let service: ProductsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
    });
    service = TestBed.inject(ProductsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('getById() chama GET /api/products/{id}', () => {
    const mock: ProductDetail = {
      id: '1',
      title: 'Produto X',
      salePrice: 10,
      originalPrice: 20,
      discountPct: 50,
      status: 'Published',
      platform: 'Amazon',
      slug: 'produto-x',
      category: 'cat',
      createdAt: '2026-01-01T00:00:00Z',
      description: 'Legenda completa do produto X',
      affiliateLink: 'https://x',
      imageUrl: 'https://img',
      mediaUrl: null,
      mediaLocalPath: null,
      updatedAt: '2026-01-01T00:00:00Z',
    };

    service.getById('1').subscribe((res) => {
      expect(res).toEqual(mock);
    });

    const req = httpMock.expectOne('/api/products/1');
    expect(req.request.method).toBe('GET');
    req.flush(mock);
  });
});
