import { Component, type ReactNode } from 'react';
import { t } from '../i18n';

interface State {
  failed: boolean;
}

/**
 * Catches a page that cannot be shown, most often a file of a lazy page that is gone after a new release,
 * and offers to reload instead of leaving a blank screen. React has no hook for this, so it is a class.
 */
export class PageLoadBoundary extends Component<{ children: ReactNode }, State> {
  state: State = { failed: false };

  static getDerivedStateFromError(): State {
    return { failed: true };
  }

  render() {
    if (!this.state.failed) {
      return this.props.children;
    }

    return (
      <>
        <p role="alert" className="form-error">
          {t('nav.pageFailed')}
        </p>
        <button
          type="button"
          onClick={() => {
            window.location.reload();
          }}
        >
          {t('auth.retry')}
        </button>
      </>
    );
  }
}
