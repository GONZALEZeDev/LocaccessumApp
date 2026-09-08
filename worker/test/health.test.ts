import { describe, it, expect } from 'vitest';
import { WORKER_NAME } from '../src/health.js';

describe('health', () => {
  it('exports the worker name', () => {
    expect(WORKER_NAME).toBe('locaccessum-reminder-worker');
  });
});
