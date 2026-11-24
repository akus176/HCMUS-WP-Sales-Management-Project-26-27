import type { components } from '../../api/schema';
import { ApiError } from '../../api/errors';
import { OfflineWriteError } from '../../connectivity/connectivity';

export type MenuItem = components['schemas']['MenuItemResponse'];
export type Size = components['schemas']['SizeVariantResponse'];
export type Addon = components['schemas']['AddonResponse'];
export type SavedOrder = components['schemas']['OrderResponse'];
export type Customer = components['schemas']['CustomerDetailResponse'];
export type CartLine = {
  id: string;
  item: MenuItem;
  size: Size;
  addons: Addon[];
  quantity: number;
};
export type ServiceForm = {
  channel: 'takeaway' | 'dine_in' | 'delivery';
  tableId: string | null;
  customerId: string | null;
  customerAddressId: string | null;
  recipientName: string;
  contactNumber: string;
  addressLine: string;
};
export const emptyService = (): ServiceForm => ({
  channel: 'takeaway',
  tableId: null,
  customerId: null,
  customerAddressId: null,
  recipientName: '',
  contactNumber: '',
  addressLine: '',
});
export function lineEstimate(line: CartLine) {
  const cents =
    Math.round(Number(line.size.price) * 100) +
    line.addons.reduce(
      (sum, addon) => sum + Math.round(Number(addon.price) * 100),
      0,
    );
  return (cents * line.quantity) / 100;
}
export function createTicket(
  orderId: string,
  form: ServiceForm,
  cart: CartLine[],
): components['schemas']['CreateOrderRequest'] {
  if (!cart.length) throw new Error('Add at least one item to the ticket.');
  if (
    cart.some(
      (line) =>
        !Number.isInteger(line.quantity) ||
        line.quantity < 1 ||
        line.quantity > 2147483647,
    )
  )
    throw new Error('Use a positive whole-number quantity for every item.');
  if (form.channel === 'dine_in' && !form.tableId)
    throw new Error('Choose an available table for dine-in.');
  if (
    form.channel === 'delivery' &&
    (!form.recipientName.trim() ||
      !form.contactNumber.trim() ||
      !form.addressLine.trim())
  )
    throw new Error('Enter the delivery recipient, phone number and address.');
  return {
    orderId,
    channel: form.channel,
    tableId: form.channel === 'dine_in' ? form.tableId : null,
    customer:
      form.channel === 'delivery'
        ? {
            customerId: form.customerId,
            customerAddressId: form.customerAddressId,
            recipientName: form.recipientName.trim(),
            contactNumber: form.contactNumber.trim(),
            addressLine: form.addressLine.trim(),
          }
        : form.customerId
          ? { customerId: form.customerId }
          : null,
    lines: cart.map((line) => ({
      menuItemId: line.item.id,
      sizeVariantId: line.size.id,
      quantity: line.quantity,
      addonIds: line.addons.map((addon) => addon.id),
    })),
  };
}
export function uncertainOutcome(error: Error) {
  return (
    !(error instanceof OfflineWriteError) &&
    (!(error instanceof ApiError) || error.status >= 500)
  );
}
export function counterError(error: Error) {
  return error instanceof ApiError
    ? error.problem.detail || error.message
    : error.message;
}
