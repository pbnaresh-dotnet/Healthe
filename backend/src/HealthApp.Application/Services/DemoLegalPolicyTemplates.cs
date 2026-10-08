namespace HealthApp.Application.Services;

internal static class DemoLegalPolicyTemplates
{
    public static string Terms(string outletName) => $@"1. About these terms
These customer Terms & Conditions apply to meals, packages and subscriptions sold by {outletName} during this demonstration.

2. Orders and subscriptions
Customers may create subscriptions, select meals and manage delivery dates through the outlet portal. The outlet is responsible for the accuracy of menus, prices, preparation and delivery.

3. Customer responsibilities
Customers must provide accurate account and delivery information and follow the published service instructions.

4. Changes and cancellation
Meal changes, skips, reschedules and cancellations are subject to the outlet's published operational cut-offs.

5. Demo notice
This is demonstration content. It is preloaded so the complete customer journey can be explored before a real outlet publishes its own legal policies.

Last updated: {DateTime.UtcNow:dd MMM yyyy}.";

    public static string Privacy(string outletName) => $@"1. Scope
This demonstration privacy notice explains how {outletName} may use customer information entered into the demo workspace.

2. Information used
Account, contact, delivery, meal preference and operational information may be displayed within the demo workflow.

3. Service use
Information is used only to demonstrate account, subscription, meal planning, kitchen and delivery features.

4. Demo notice
The content is sample legal text and must be replaced or reviewed by the outlet before production use.

Last updated: {DateTime.UtcNow:dd MMM yyyy}.";

    public static string Cancellation(string outletName) => $@"1. Cancellation
Customers may request cancellation according to the outlet's configured cancellation rules.

2. Refunds
Any refund or credit is subject to the final commercial policy published by {outletName}.

3. Demo notice
This is sample policy content for demonstration purposes and is not a production refund commitment.

Last updated: {DateTime.UtcNow:dd MMM yyyy}.";

    public static string SkipReschedule(string outletName) => $@"1. Meal skips
Customers should request a meal skip before the outlet's configured cutoff time.

2. Rescheduling
Eligible unused meals may be rescheduled within the subscription rules displayed by {outletName}.

3. Late requests
Late changes may incur the configured late-skip fee where applicable.

4. Demo notice
These settings are sample operational rules used to demonstrate the meal calendar workflow.

Last updated: {DateTime.UtcNow:dd MMM yyyy}.";

    public static string Delivery(string outletName) => $@"1. Delivery coverage
Deliveries are available only within the coverage configured by {outletName}.

2. Delivery timing
Published delivery slots and dates are estimates subject to kitchen and driver operations.

3. Address accuracy
Customers must provide a correct delivery pinpoint and editable address details.

4. Demo notice
This policy is preloaded sample content for demonstrating delivery planning and driver workflows.

Last updated: {DateTime.UtcNow:dd MMM yyyy}.";

    public static string Allergen(string outletName) => $@"1. Ingredient and allergen information
{outletName} maintains ingredient and allergen information for the meals configured in the demo.

2. Customer responsibility
Customers should review meal ingredients and allergy warnings before confirming meals.

3. Cross-contact
The outlet should define its production and cross-contact controls before production use.

4. Demo notice
The recipes and allergen information in this workspace are demonstration data.

Last updated: {DateTime.UtcNow:dd MMM yyyy}.";

    public static string Payment(string outletName) => $@"1. Pricing
Meal and subscription prices displayed in the demo are sample INR values.

2. Discounts
Any discount codes or subscription discounts shown in the demo are illustrative.

3. Taxes and delivery
Tax and delivery calculations use the outlet settings configured in the demo workspace.

4. Demo notice
No real customer payment is required to explore this demo workspace. Production outlets must publish their own final commercial terms.

Last updated: {DateTime.UtcNow:dd MMM yyyy}.";
}
