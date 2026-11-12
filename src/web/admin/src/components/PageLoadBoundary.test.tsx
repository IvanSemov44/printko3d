import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PageLoadBoundary } from './PageLoadBoundary';

function Broken(): never {
  throw new Error('The file of the page is gone');
}

describe('PageLoadBoundary', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('shows the page when nothing is wrong', () => {
    render(
      <PageLoadBoundary>
        <p>The page</p>
      </PageLoadBoundary>,
    );

    expect(screen.getByText('The page')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('says so and reloads on the button when the page cannot be shown', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const reload = vi.fn();
    vi.stubGlobal('location', { reload });

    render(
      <PageLoadBoundary>
        <Broken />
      </PageLoadBoundary>,
    );

    expect(screen.getByRole('alert')).toHaveTextContent('Страницата не можа да се покаже');
    fireEvent.click(screen.getByRole('button', { name: 'Опитай отново' }));
    expect(reload).toHaveBeenCalledOnce();
  });
});
