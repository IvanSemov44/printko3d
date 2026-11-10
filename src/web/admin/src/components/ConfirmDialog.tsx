import { useEffect, useId, useRef, type ReactNode } from 'react';

export interface ConfirmDialogProps {
  open: boolean;
  title: string;
  children: ReactNode;
  confirmLabel: string;
  cancelLabel: string;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * A modal made with the browser's own dialog element: the page behind it cannot be reached with the keyboard,
 * Esc closes it, and the focus goes back to the button that opened it. "Cancel" comes first and has the focus,
 * so pressing Enter by mistake does not confirm.
 */
export function ConfirmDialog({
  open,
  title,
  children,
  confirmLabel,
  cancelLabel,
  busy = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const dialog = useRef<HTMLDialogElement>(null);
  const cancelButton = useRef<HTMLButtonElement>(null);
  const titleId = useId();
  const messageId = useId();

  useEffect(() => {
    const element = dialog.current;
    if (!element) {
      return;
    }
    if (open && !element.open) {
      element.showModal();
      cancelButton.current?.focus();
    } else if (!open && element.open) {
      element.close();
    }
  }, [open]);

  return (
    <dialog
      ref={dialog}
      aria-labelledby={titleId}
      aria-describedby={messageId}
      onCancel={(event) => {
        // Esc: let the state decide, so the dialog and the page never disagree.
        event.preventDefault();
        if (!busy) {
          onCancel();
        }
      }}
    >
      <h2 id={titleId}>{title}</h2>
      <div id={messageId}>{children}</div>
      <div className="dialog-actions">
        <button
          ref={cancelButton}
          type="button"
          className="secondary"
          onClick={onCancel}
          disabled={busy}
        >
          {cancelLabel}
        </button>
        <button type="button" className="danger" onClick={onConfirm} disabled={busy}>
          {confirmLabel}
        </button>
      </div>
    </dialog>
  );
}
