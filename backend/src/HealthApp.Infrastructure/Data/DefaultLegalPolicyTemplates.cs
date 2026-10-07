using System.Text;

namespace HealthApp.Infrastructure.Data;

internal static class DefaultLegalPolicyTemplates
{
    public static string Terms(string outletName) => $@"1. About these Terms
These Customer Terms & Conditions govern your use of {outletName}'s meal, package and subscription services. The outlet is responsible for the food, menu, pricing, preparation, delivery, customer support and other commercial services described on this website. Broccoly provides the software platform used to operate the customer portal and does not replace the outlet as the seller of meals.

By creating an account, placing an order, accepting a package or using the outlet's services, you agree to these Terms and the other customer policies published by the outlet. Where applicable law gives you rights that cannot be excluded or limited, those rights continue to apply.

2. Customer Account
You must provide accurate and current information when creating an account, including your name, email, mobile number and delivery information. Keep your password and account credentials secure and notify the outlet if you believe your account has been used without permission.

You are responsible for activity carried out through your account except where liability arises from the outlet's or platform's unlawful conduct or failure to apply required security measures.

3. Meal Plans, Packages and Subscriptions
The outlet may offer one-time meals, pre-planned packages and recurring subscriptions. The applicable plan, meal count, delivery frequency, duration, selected meals, delivery mode, taxes, fees and discounts will be presented before confirmation.

A package or subscription becomes confirmed only after the required customer confirmation and payment or other stated acceptance process is completed. Availability is subject to the outlet's menu, service area, kitchen capacity and configured delivery schedule.

4. Prices, Taxes, Fees and Discounts
The prices and mandatory charges applicable to your purchase will be displayed before confirmation where required. Charges can include meal prices, delivery fees, service fees, taxes and other clearly disclosed amounts.

Promotional codes and discounts are subject to their stated eligibility, validity dates, usage limits and other conditions. A discount cannot normally be combined with another offer unless the outlet expressly permits it.

If a genuine pricing or calculation error is identified, the outlet may correct it and will provide the remedy required by applicable law.

5. Payments
Payments are processed through the payment method made available at checkout. An order may remain pending or fail if payment is declined, reversed, cancelled, disputed or not confirmed.

The outlet may use third-party payment providers and is not responsible for outages or processing delays that are outside its reasonable control, subject to applicable consumer rights.

6. Cancellation, Refunds and Credits
Your cancellation and refund rights are governed by the outlet's published Cancellation & Refund Policy, together with any mandatory rights under applicable law.

Where a refund or customer credit is approved, the outlet will use the method and processing period stated in the applicable policy. Certain charges may be non-refundable where clearly disclosed and permitted by law.

7. Meal Skips and Rescheduling
Meal skips and rescheduling are governed by the outlet's published Meal Skip & Rescheduling Policy. Cut-off times, late charges, permitted dates, address changes and expiry rules will be applied as stated there.

8. Delivery
The outlet delivers only to serviceable locations and delivery windows configured for the service. You are responsible for providing an accurate address and selecting the intended delivery point on the map where the customer portal supports map selection.

You should be available during the delivery window and provide reasonable access instructions. Failed delivery, re-delivery or address-change charges may apply only where disclosed in the outlet's Delivery Policy and permitted by law.

9. Ingredients, Allergens and Dietary Choices
Ingredient, nutrition and allergen information is provided to help you make informed choices. Recipes and ingredients can change, and customers should review the current information before each order.

Meals may be prepared in environments where allergens are handled. Customers with severe allergies, medical dietary requirements or other safety concerns should contact the outlet before ordering and should not rely solely on information displayed by the platform.

10. Health Information
You may choose to provide dietary, allergy or similar information to support meal personalisation. Such information must be used only for purposes described in the outlet's Privacy Policy and other applicable notices.

The service does not provide medical diagnosis or treatment. Meal information is not a substitute for advice from a qualified healthcare professional.

11. Customer Conduct
You must not misuse the customer portal, submit fraudulent payment information, abuse delivery or support staff, attempt unauthorised access, interfere with platform security, upload unlawful material or use the service for fraudulent or harmful activity.

12. Service Changes and Availability
The outlet may change menus, recipes, delivery areas, schedules, prices, promotions or other operational details for legitimate business or safety reasons. Where notice or other customer rights are required, the outlet will provide them.

Neither the outlet nor the platform promises uninterrupted availability where service disruption results from events outside reasonable control, subject to mandatory legal obligations.

13. Complaints and Customer Support
Questions relating to meals, delivery, subscriptions, refunds or outlet services should be raised with {outletName} using the customer-support contact details published on the website.

Technical issues relating to the software platform may be escalated to Broccoly where the outlet directs you to do so.

14. Suspension or Closure of Accounts
The outlet may suspend or close an account where there is fraud, abuse, a material breach of these Terms, non-payment or another legitimate operational or legal reason. Outstanding customer rights, refunds or credits will be handled according to the applicable policies and law.

15. Changes to these Terms
The outlet may update these Terms from time to time. Changes will be versioned and dated. Where renewed consent or advance notice is required by law, the outlet will provide it before the changed Terms apply.

16. Governing Law and Disputes
These Terms are governed by the laws of India, subject to mandatory consumer and other rights that apply to you. Disputes should first be raised with {outletName} so the parties can try to resolve the matter informally. Any court or dispute forum must be determined subject to applicable law and mandatory consumer protections.

Version 1.0
Effective date: {DateTime.UtcNow:yyyy-MM-dd}
Outlet: {outletName}
";

    public static string Privacy(string outletName) => $@"1. Who is Responsible for Your Information
{outletName} is the primary business contact for personal information processed to provide its meals, packages and delivery services. Broccoly provides the software platform and may process information on the outlet's instructions and for limited platform, security and infrastructure purposes.

2. Information We Collect
Depending on how you use the service, we may collect your name, email address, mobile number, account credentials, delivery addresses, precise delivery coordinates, order and subscription history, meal preferences, dietary choices, allergy information, communications, device or browser information, security logs and payment transaction references.

3. Information You Choose to Provide
You may choose to provide information about food preferences, goals, allergies or other dietary needs. You should provide only information that is relevant to the meal service. Information that may be sensitive under applicable law will be handled with appropriate safeguards and only for legitimate stated purposes.

4. Why We Use Information
Information may be used to:
- create and secure your account;
- process meals, packages and subscriptions;
- calculate prices, discounts, delivery fees and taxes;
- prepare and deliver meals;
- provide dietary and allergen warnings;
- communicate service, order and account updates;
- provide customer support;
- prevent fraud, abuse and security incidents;
- maintain records required by law;
- improve the reliability and performance of the platform; and
- send promotional communications where permitted and where you have the required choice or consent.

5. Legal Basis
Where applicable privacy law requires a legal basis, processing may rely on performance of a contract, legal obligation, legitimate interests or consent, depending on the purpose and circumstances. Sensitive or special-category information may require additional legal conditions.

6. Sharing of Information
Information may be shared with personnel and service providers who need it to operate the service, including delivery personnel, payment providers, cloud hosting and storage providers, security and support providers, professional advisers and public authorities where legally required.

Service providers receive access only to the information reasonably required for their role and should be subject to appropriate confidentiality and security obligations.

7. Precise Location
Where you select a delivery point on a map, the associated coordinates may be stored and used to determine serviceability, calculate delivery distance and support accurate delivery. You can edit the address fields before confirming an address.

8. Payment Information
Payment credentials are normally processed by the selected payment provider. {outletName} and Broccoly may receive transaction identifiers, status and other limited payment information needed to reconcile orders and provide customer support.

9. Retention
Information will be retained for as long as reasonably necessary for the stated service purposes, security, accounting, dispute handling and legal obligations. Specific retention periods may vary by data type and applicable law.

10. Security
Reasonable technical and organisational measures are used to protect information, including access controls, authenticated access, least-privilege permissions, secure transport, logging, backups and monitoring. No internet service can guarantee absolute security.

11. Your Rights
Depending on applicable law, you may have rights to access, correct, delete, restrict or object to certain processing, request portability, withdraw consent where processing is consent-based, and complain to a relevant regulator or supervisory authority.

Requests should be made to {outletName} using the privacy or support contact published on the website.

12. Cookies and Technical Storage
The customer portal may use essential cookies, local storage and technical logs required for authentication, security and normal operation. Non-essential analytics or marketing technologies should be used only in accordance with applicable notice and consent requirements.

13. Children
The service is not intended to knowingly collect children's information in ways prohibited by applicable law. Where age restrictions or parental requirements apply, those requirements must be followed.

14. International Processing
Broccoly or service providers may process information in countries outside your location. Where applicable law requires safeguards for international transfers, appropriate contractual, technical or organisational measures should be used.

15. Changes to this Privacy Policy
This Privacy Policy is versioned and dated. Material changes will be communicated where required by law. Continued use after an effective change may be subject to the updated policy only where legally permitted.

Version 1.0
Effective date: {DateTime.UtcNow:yyyy-MM-dd}
Outlet: {outletName}
";

    public static string CancellationRefund(string outletName) => $@"1. Cancellation Before Preparation
You may cancel a meal, package or scheduled delivery without a cancellation charge when the cancellation is received before the cut-off time stated by {outletName} and where no preparation, packing or dispatch has started.

2. Cancellation After Preparation
Once preparation, packing or dispatch has started, a cancellation may be non-refundable or may qualify only for a partial refund or customer credit, subject to the rights required by applicable law.

3. Subscription Cancellation
Recurring subscriptions can be cancelled through the available customer account controls or by contacting {outletName}. Cancellation affects future billing and deliveries according to the subscription terms and the applicable cut-off time. Already prepared or dispatched meals may remain payable where permitted.

4. Refunds
Approved refunds will normally be returned to the original payment method unless the customer agrees to another permitted form of credit or refund. Processing times depend on the payment provider and banking system.

5. Delivery Fees and Other Charges
Whether delivery fees, service charges or other amounts are refundable depends on the reason for cancellation and the applicable legal requirements. Any non-refundable amount must be clearly disclosed before purchase where required.

6. Service Failure
If a meal is materially missing, damaged, incorrectly supplied or not delivered as promised, contact {outletName} promptly. The outlet will assess the issue and provide the remedy required by its operational policy and applicable law, which may include replacement, refund or customer credit.

7. Payment Failure and Duplicate Charges
If a payment is reversed, duplicated or otherwise processed incorrectly, the outlet will investigate and correct the transaction as appropriate.

8. How to Request a Refund
Provide the order or subscription reference, your account contact information, the reason for the request and any relevant evidence. Requests should be made using the support contact published on the website.

Version 1.0
Effective date: {DateTime.UtcNow:yyyy-MM-dd}
Outlet: {outletName}
";

    public static string SkipReschedule(string outletName) => $@"1. Free Meal Skip Cut-off
A scheduled meal may be skipped without a late charge when the skip request is successfully submitted before 12:00 midnight in the local time applicable to the delivery date, unless {outletName} publishes a different operational cut-off for a specific service.

2. Late Skip
A skip request received after the free cut-off may incur a late skip fee where that fee was disclosed before the customer accepted the applicable package or subscription. A late fee does not remove any mandatory rights where the failure is caused by the outlet or delivery service.

3. Rescheduling
An unused meal may be rescheduled to another eligible date and meal slot when the subscription, service area, kitchen capacity and other operational conditions permit. The new date must remain within the rescheduling window configured by the outlet.

4. Address Changes
When rescheduling a meal, the customer may select another saved delivery address where the outlet serves that location. A new delivery fee may apply when the delivery distance changes and the applicable pricing has been disclosed.

5. Expiry of Unused Meals
Unless the outlet states otherwise for a specific plan, an unused skipped meal should be rescheduled within 7 calendar days of the original scheduled date. After the stated expiry window, the meal may expire without refund or replacement where permitted by law.

6. Outlet or Delivery Failure
A meal should not be treated as a customer-initiated skip when the delivery cannot be completed because of an outlet or delivery-system failure. The outlet will provide the remedy required by its service policy and applicable law.

7. Confirmation
A skip or rescheduling request is effective only after the customer portal confirms that the change has been successfully recorded.

Version 1.0
Effective date: {DateTime.UtcNow:yyyy-MM-dd}
Outlet: {outletName}
";

    public static string Delivery(string outletName) => $@"1. Service Areas
{outletName} delivers only to areas, postcodes or radius coverage shown as serviceable in the customer portal. Serviceability may change as the outlet updates its operational coverage.

2. Exact Delivery Location
Where map selection is available, customers should select the exact point where the meal should be delivered. The customer may edit the address lines before confirming the address.

3. Delivery Address Responsibility
Customers are responsible for providing a correct address, postcode, contact number, building or flat information, access instructions and map location. Incorrect or incomplete information can cause delay or failed delivery.

4. Delivery Windows
The outlet will publish available delivery days, meal slots and delivery windows. Actual arrival may vary because of traffic, weather, vehicle issues, route changes, safety conditions, demand or other reasonable operational circumstances.

5. Failed Delivery
If the customer is unavailable, the address is inaccessible, the location is unsafe or the address/pin is materially incorrect, the outlet may mark the delivery as failed. Any re-delivery or failed-delivery charge will apply only where disclosed and permitted by law.

6. Address Changes
Customers should change an address before the cut-off communicated by the outlet. A late address change may not be possible for the same delivery and a different delivery fee may apply if the destination changes.

7. Delivery Charges
Delivery fees may be calculated using distance or other configured service rules. The applicable fee should be displayed before order confirmation where required.

8. Weather, Safety and Exceptional Events
The outlet may delay, reschedule or cancel a delivery when reasonably necessary for food safety, road safety, severe weather, emergency conditions or other events outside reasonable control. The outlet will provide an appropriate remedy where required by law.

9. Contact
Delivery questions, missed deliveries and address issues should be reported to {outletName} using the support contact published on the website.

Version 1.0
Effective date: {DateTime.UtcNow:yyyy-MM-dd}
Outlet: {outletName}
";

    public static string AllergenDietary(string outletName) => $@"1. Ingredient Information
{outletName} provides ingredient and allergen information for meals to support informed customer choices. Recipe information can change, so customers should review the current meal information before each purchase.

2. Common Allergens
A meal may contain or come into contact with common allergens including cereals containing gluten, milk, eggs, peanuts, tree nuts, soy, sesame, fish, crustaceans, molluscs, celery, mustard, lupin and sulphites, depending on the ingredients and preparation environment.

3. Cross-contact
Meals may be prepared in kitchens or facilities where allergens are handled. Even where an allergen is not an intentional ingredient, cross-contact can occur despite reasonable food-safety controls.

4. Customer Responsibility
Customers with severe allergies, intolerances, medically required diets or other safety concerns should review the ingredient information and contact {outletName} before ordering. Customers should not rely solely on a general category, dietary label or previous order history.

5. Dietary Preferences
Dietary labels such as vegetarian, vegan or other preferences describe the outlet's configured meal classification and should be checked against the current ingredients where the distinction is important to the customer.

6. Changes to Recipes
Ingredients, suppliers, portion sizes and preparation methods can change. The current recipe information shown on the platform should be treated as the most recent information available.

7. Medical Advice
Food, nutrition and allergen information is not medical advice, diagnosis or treatment. Customers with medical conditions should consult an appropriately qualified healthcare professional.

8. Limitation
The outlet takes reasonable steps to maintain accurate ingredient information but cannot guarantee that a meal is completely free from an allergen or suitable for a specific medical condition unless expressly stated and legally supported.

Version 1.0
Effective date: {DateTime.UtcNow:yyyy-MM-dd}
Outlet: {outletName}
";

    public static string PaymentPricingPromotional(string outletName) => $@"1. Prices
Meal, package, subscription and delivery prices are displayed by {outletName} through the customer portal. Prices may vary by portion, menu selection, duration, delivery distance, promotion or other clearly disclosed condition.

2. Taxes and Charges
Applicable taxes and mandatory fees will be included or displayed before confirmation as required. Delivery charges may be calculated separately from meal charges.

3. Promotional Codes
Promotional codes are valid only during the stated promotion period and subject to the stated eligibility criteria. A code may be limited by customer, order, meal count, outlet, maximum discount amount or number of uses.

4. Multiple Promotions
Unless specifically stated otherwise, only one promotional discount may be applied to a transaction. Promotions cannot normally be exchanged for cash.

5. Expired or Invalid Codes
A promotion may be rejected if it is expired, suspended, already fully redeemed, incorrectly entered, not applicable to the selected meal or otherwise outside its stated conditions.

6. Pricing Corrections
If {outletName} identifies a genuine pricing or promotional calculation error, the outlet may correct the error and will provide the remedy required by applicable law.

7. Payment Confirmation
An order, package or subscription is considered paid only when the required payment status has been successfully confirmed. A payment attempt that is pending, declined, reversed or disputed does not by itself guarantee fulfilment.

8. Refunds and Promotions
Where an order containing a discount is cancelled or refunded, the discount amount and other charges will be treated according to the applicable Cancellation & Refund Policy and the terms of the promotion.

9. Recurring Subscription Charges
For recurring subscriptions, the applicable subscription fee and billing frequency will be presented before acceptance. Future charges may change only where the outlet provides the notice required by law and the subscription terms.

Version 1.0
Effective date: {DateTime.UtcNow:yyyy-MM-dd}
Outlet: {outletName}
";
}
