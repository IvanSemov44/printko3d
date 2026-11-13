import { describe, expect, it } from 'vitest';
import { lineStateNoteKey } from './lineStateNote';

describe('lineStateNoteKey', () => {
  it('has no note for a line that can be bought and a note for the others', () => {
    expect(lineStateNoteKey('Available')).toBeNull();
    expect(lineStateNoteKey('InsufficientStock')).toBe('cart.state.insufficientStock');
    expect(lineStateNoteKey('Inactive')).toBe('cart.state.unavailable');
    expect(lineStateNoteKey('Archived')).toBe('cart.state.unavailable');
  });
});
