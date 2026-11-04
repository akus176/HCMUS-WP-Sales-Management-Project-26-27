import type { components, paths } from "./schema";

type Equal<Left, Right> =
  (<Value>() => Value extends Left ? 1 : 2) extends
  (<Value>() => Value extends Right ? 1 : 2) ? true : false;
type Assert<Condition extends true> = Condition;
type Json200<Operation extends { responses: object }> =
  Operation["responses"] extends { 200: { content: { "application/json": infer Body } } }
    ? Body
    : never;

type TicketPage = Json200<paths["/api/v1/preparation-tickets"]["get"]>;
type DiscountPage = Json200<paths["/api/v1/discounts/pending"]["get"]>;
type IngredientPage = Json200<paths["/api/v1/ingredients"]["get"]>;
type CourierPage = Json200<paths["/api/v1/courier/assignments"]["get"]>;

export type TicketPageMatches = Assert<Equal<TicketPage,
  components["schemas"]["PageResponseOfPreparationTicketResponse"]>>;
export type DiscountPageMatches = Assert<Equal<DiscountPage,
  components["schemas"]["PageResponseOfPendingDiscountResponse"]>>;
export type IngredientPageMatches = Assert<Equal<IngredientPage,
  components["schemas"]["PageResponseOfIngredientBalanceResponse"]>>;
export type CourierPageMatches = Assert<Equal<CourierPage,
  components["schemas"]["PageResponseOfCourierAssignmentResponse"]>>;

type Ticket = components["schemas"]["PreparationTicketResponse"];
type CourierSummary = components["schemas"]["CourierAssignmentResponse"];
type Adjustment = components["schemas"]["StockAdjustmentRequest"];
type Payment = components["schemas"]["PaymentResponse"];
export type TicketOmitsCustomer = Assert<Equal<Extract<"contactNumber" | "addressLine" | "customerId", keyof Ticket>, never>>;
export type CourierListOmitsContact = Assert<Equal<Extract<"contactNumber" | "addressLine", keyof CourierSummary>, never>>;
export type AdjustmentUsesClientId = Assert<Equal<Adjustment["requestId"], string>>;
export type AdjustmentOmitsActor = Assert<Equal<Extract<"actorId", keyof Adjustment>, never>>;
export type PaymentUsesGuid = Assert<Equal<Payment["paymentId"], string>>;

export type TicketStartIsNoContent = Assert<Equal<204 extends keyof
  paths["/api/v1/preparation-tickets/{id}/start"]["post"]["responses"] ? true : false, true>>;
export type CourierCompleteIsNoContent = Assert<Equal<204 extends keyof
  paths["/api/v1/courier/assignments/{id}/complete"]["post"]["responses"] ? true : false, true>>;
