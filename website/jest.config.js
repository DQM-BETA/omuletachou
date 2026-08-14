const nextJest = require('next/jest');

const createJestConfig = nextJest({
  dir: './',
});

/** @type {import('jest').Config} */
const customJestConfig = {
  setupFilesAfterEnv: ['<rootDir>/jest.setup.js'],
  testEnvironment: 'jest-environment-jsdom',
  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/$1',
  },
  modulePathIgnorePatterns: ['<rootDir>/.next/'],
  // e2e/ é a suíte do Playwright (test:visual) — runner e sintaxe (@playwright/test)
  // diferentes de Jest; sem isso o Jest tenta carregar visual.spec.ts e quebra.
  testPathIgnorePatterns: ['<rootDir>/node_modules/', '[\\\\/]e2e[\\\\/]'],
  coverageThreshold: {
    global: {
      branches: 80,
      functions: 80,
      lines: 80,
      statements: 80,
    },
  },
  collectCoverageFrom: [
    'lib/**/*.{ts,tsx}',
    'components/**/*.{ts,tsx}',
    'app/**/page.tsx',
    'app/sitemap.ts',
    'app/categoria/**/*.tsx',
    '!**/*.d.ts',
  ],
};

module.exports = createJestConfig(customJestConfig);
