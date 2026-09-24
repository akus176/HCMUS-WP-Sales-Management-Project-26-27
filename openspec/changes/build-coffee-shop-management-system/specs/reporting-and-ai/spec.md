## Purpose

Defines operational reports and a grounded, permission-aware AI manager assistant that answers questions by calling read-only reporting tools.

## ADDED Requirements

### Requirement: Revenue reporting
The system SHALL report paid revenue and order counts for a selected date range and SHALL support breakdowns by day and payment method.

#### Scenario: Request monthly revenue
- **WHEN** an authorized user selects a calendar month
- **THEN** the system returns paid revenue and order counts limited to that month

### Requirement: Best-seller reporting
The system SHALL rank sold menu items and variants by quantity and revenue for a selected date range while excluding cancelled orders.

#### Scenario: View best-selling drinks
- **WHEN** an authorized user requests best sellers for a valid date range
- **THEN** the system returns ranked items with sold quantity and revenue for that range

### Requirement: Order-channel reporting
The system SHALL summarize order count and revenue by dine-in, takeaway, and delivery channels for a selected date range.

#### Scenario: Compare sales channels
- **WHEN** an authorized user requests channel performance
- **THEN** the system returns separate metrics for each supported channel

### Requirement: Low-stock reporting
The system SHALL report active ingredients at or below their reorder thresholds, including current quantity and threshold.

#### Scenario: Request the low-stock report
- **WHEN** authorized staff request current low-stock ingredients
- **THEN** the system returns only active ingredients whose balances meet the low-stock condition

### Requirement: Read-only AI reporting assistant
The system SHALL provide managers with an AI assistant that answers operational questions only through approved read-only reporting tools.

#### Scenario: Ask for this week's best sellers
- **WHEN** an authenticated manager asks which products sold best this week
- **THEN** the assistant calls the appropriate reporting tool and explains the returned result

#### Scenario: Ask the assistant to change data
- **WHEN** a user asks the reporting assistant to create, update, delete, approve, or cancel business data
- **THEN** the assistant refuses the action and does not call a state-changing operation

### Requirement: Permission-aware AI access
The AI assistant MUST use the current authenticated user's identity and permissions and MUST NOT return report data the user cannot access directly.

#### Scenario: Unauthorized user requests management revenue
- **WHEN** a user without revenue-report permission asks the assistant for revenue data
- **THEN** the assistant refuses or limits the response without exposing restricted data

### Requirement: Grounded AI responses
The AI assistant MUST distinguish tool-returned facts from unavailable information and MUST NOT invent menu items, orders, inventory values, customers, or financial results.

#### Scenario: Reporting tool returns no data
- **WHEN** the selected reporting tool returns no records for the requested period
- **THEN** the assistant states that no matching data was found instead of fabricating an answer

### Requirement: AI interaction audit
The system SHALL record the requesting user, question, reporting tools invoked, invocation outcomes, and response time for each AI reporting interaction without storing access tokens or secrets.

#### Scenario: Complete an AI report query
- **WHEN** the assistant finishes answering a manager's report question
- **THEN** an audit record links the user request to the read-only tools that were invoked
