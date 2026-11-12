import { useT } from '../store-settings/useStoreSettings';

interface QuantityStepperProps {
  /** The name of the line, for the names of the buttons. */
  name: string;
  quantity: number;
  canIncrease: boolean;
  /** A change is on its way: the buttons stay in place (the focus stays too) but do nothing until it is done. */
  busy: boolean;
  onChange: (quantity: number) => void;
}

/** "−", the number and "+". The quantity never goes below 1: to take a line out, the buyer uses "Remove". */
export function QuantityStepper({
  name,
  quantity,
  canIncrease,
  busy,
  onChange,
}: QuantityStepperProps) {
  const t = useT();
  const canDecrease = quantity > 1;

  return (
    <div className="quantity-stepper" role="group" aria-label={t('cart.quantity', { name })}>
      <button
        type="button"
        className="button-secondary"
        aria-label={t('cart.decrease', { name })}
        aria-disabled={busy || !canDecrease}
        onClick={() => {
          if (!busy && canDecrease) {
            onChange(quantity - 1);
          }
        }}
      >
        <span aria-hidden="true">−</span>
      </button>
      <output className="quantity-value" aria-live="polite">
        {quantity}
      </output>
      <button
        type="button"
        className="button-secondary"
        aria-label={t('cart.increase', { name })}
        aria-disabled={busy || !canIncrease}
        onClick={() => {
          if (!busy && canIncrease) {
            onChange(quantity + 1);
          }
        }}
      >
        <span aria-hidden="true">+</span>
      </button>
    </div>
  );
}
