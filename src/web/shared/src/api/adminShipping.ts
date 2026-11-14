import { apiRequest, apiSend } from './http';
import { field, readArray, readInteger, readObject, type Reader } from './decode';

/** One step of the tariff: a parcel up to `maxWeightGrams` costs `price` (the smallest unit of the shop currency). */
export interface ShippingTariffStep {
  maxWeightGrams: number;
  price: number;
}

const TARIFF_PATH = '/api/v1/admin/shipping-tariff';

const readStep: Reader<ShippingTariffStep> = (value, path) => {
  const object = readObject(value, path);
  return {
    maxWeightGrams: field(object, 'maxWeightGrams', readInteger, path),
    price: field(object, 'price', readInteger, path),
  };
};

function readTariff(value: unknown): ShippingTariffStep[] {
  return field(readObject(value, 'tariff'), 'steps', readArray(readStep), 'tariff');
}

/** The steps from the lightest to the heaviest. An empty list means the shop offers no delivery. */
export async function getShippingTariff(signal?: AbortSignal): Promise<ShippingTariffStep[]> {
  return readTariff(await apiRequest(TARIFF_PATH, { signal }));
}

/**
 * Replaces the whole tariff and returns it sorted by weight. Errors: 400 `validation.failed`, 409 `shipping.tariff_conflict`
 * when someone saved a tariff at the same moment.
 */
export async function saveShippingTariff(
  steps: readonly ShippingTariffStep[],
): Promise<ShippingTariffStep[]> {
  return readTariff(await apiSend('PUT', TARIFF_PATH, { steps }));
}
