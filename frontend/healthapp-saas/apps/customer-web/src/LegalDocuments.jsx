import React,{useMemo}from'react';

const CUSTOMER_DOCUMENTS=[
 {id:'terms',title:'Customer Terms & Conditions',eyebrow:'CUSTOMER TERMS',intro:'These draft terms govern the purchase of meals and subscription services from the outlet shown on this customer portal.',sections:[
  ['1. About these terms','The meal service is provided by the outlet identified on this website. The outlet is responsible for its menu, pricing, delivery commitments, cancellation rules, refunds, charges and customer support. HealthApp provides the technology platform used to operate the customer portal.'],
  ['2. Account and customer information','You must provide accurate account, contact, delivery and profile information and keep your credentials secure. You are responsible for activity carried out through your account unless caused by the platform or another party acting unlawfully.'],
  ['3. Meal plans and subscriptions','A package becomes an order or subscription only after the applicable confirmation and payment step is completed. Available meals, active delivery days, package duration, delivery mode and pricing are shown before confirmation. The outlet may set minimum package requirements and operational cut-off times.'],
  ['4. Pricing, taxes and fees','Prices, applicable taxes, delivery charges, service charges and any other customer charges are shown during checkout where required. Promotional discounts are subject to their stated eligibility, validity and redemption rules.'],
  ['5. Cancellation, refunds and credits','The outlet must publish its applicable cancellation, refund and credit rules before purchase. Any mandatory consumer rights that cannot lawfully be excluded continue to apply. Where a refund or credit is permitted, the outlet should state the timing, method and any permitted deductions.'],
  ['6. Meal skips and rescheduling','The outlet may define cut-off times for skipping or rescheduling meals and may apply clearly disclosed late charges where permitted. Any rescheduling limits, expiry windows and address requirements are displayed in the applicable policy.'],
  ['7. Delivery','Delivery is made to the address and map location selected by you. You should provide access details and be available during the applicable delivery window. Failed delivery, re-delivery or address-change charges may apply only where disclosed in the outlet policy and permitted by law.'],
  ['8. Allergens and dietary information','Menus may contain allergens or be prepared in facilities where allergens are present. Ingredient and allergen information is provided to support informed choices and does not replace professional medical advice. Customers are responsible for checking current information and telling the outlet about relevant requirements.'],
  ['9. Health information','You may choose to provide health, dietary or allergy information to support meal personalisation. Such information is handled according to the applicable privacy notice and should only be used for the stated service purposes.'],
  ['10. Customer conduct','You must not misuse the service, submit fraudulent payment details, abuse delivery staff, interfere with the platform, or attempt to gain unauthorised access to accounts or systems.'],
  ['11. Service availability and changes','Menus, delivery slots, prices and service areas may change for legitimate operational reasons. The outlet should provide notice where required. Nothing in these terms removes rights provided by applicable law.'],
  ['12. Complaints and support','Questions, complaints about meals or delivery, and requests for refunds should be directed to the outlet using the contact details published on this portal. Platform-specific technical issues may be escalated to HealthApp where the outlet instructs the customer to do so.'],
  ['13. Governing law','The outlet should state its applicable governing law and dispute-resolution process here. Mandatory consumer, privacy and other rights in the customer’s jurisdiction remain unaffected.'],
  ['14. Changes to these terms','Material changes should be versioned and dated. Where renewed consent or notice is legally required, the outlet should obtain or provide it before the changed terms apply.']
 ]},
 {id:'privacy',title:'Customer Privacy Policy',eyebrow:'PRIVACY NOTICE',intro:'This draft explains how an outlet may collect and use customer information when providing meals and delivery services through HealthApp.',sections:[
  ['1. Who is responsible for your data','The outlet named on this portal is the primary business contact for customer data used to provide its meal service. HealthApp operates the software platform and may process personal data on the outlet’s instructions or for its own limited platform purposes. The final controller/processor allocation should be confirmed for the actual operating model and jurisdiction.'],
  ['2. Information we may collect','This may include name, email, mobile number, account credentials, delivery addresses, precise delivery coordinates, order and subscription history, meal preferences, dietary choices, allergy information, communications, device or technical information and payment-related transaction records.'],
  ['3. Why we use it','Purposes may include account creation, authentication, meal personalisation, allergy and dietary warnings, order processing, delivery, customer support, fraud prevention, service communications, billing, legal compliance and improvement of the service.'],
  ['4. Legal basis','Where privacy law requires a legal basis, the applicable notice should identify the basis for each processing purpose, such as performance of a contract, legal obligation, legitimate interests or consent. Sensitive-data processing may require additional conditions and safeguards.'],
  ['5. Health and allergy information','Health, allergy or similar information can require enhanced protection under applicable law. The outlet should collect only what is needed, clearly explain why it is needed, restrict access and avoid using it for unrelated purposes without an appropriate legal basis.'],
  ['6. Sharing','Information may be shared with delivery personnel, payment providers, cloud and hosting providers, customer-support providers, analytics or security providers, professional advisers, and authorities when legally required. Third-party access should be limited to the purposes that apply.'],
  ['7. International transfers','Where personal data is transferred across borders, the outlet and its service providers must use the safeguards required by applicable law. The final notice should identify relevant countries or safeguards where disclosure is required.'],
  ['8. Retention','Data should be retained only for as long as needed for the stated purposes, legal obligations, dispute handling, accounting and security. The final policy should provide specific retention periods or criteria where required.'],
  ['9. Customer rights','Depending on applicable law, you may have rights to access, correction, deletion, restriction, objection, portability, withdrawal of consent and complaint to a supervisory or regulatory authority. Requests should be directed to the outlet contact published in this notice.'],
  ['10. Cookies and technical data','The portal may use essential cookies, local storage and technical logs needed for authentication, security and operation. Non-essential analytics or marketing technologies should be disclosed and handled according to applicable consent requirements.'],
  ['11. Security','Reasonable technical and organisational safeguards should be used to protect customer information, including access controls, secure authentication, least-privilege access, encryption where appropriate, backups and monitoring.'],
  ['12. Children','The service should not knowingly collect children’s information in ways that are prohibited by applicable law. Any age restrictions and parental requirements should be stated here.'],
  ['13. Contact and complaints','Publish the outlet’s legal/business name, privacy contact, email, postal address and any required data-protection contact here. Include the relevant supervisory authority or complaint route where required by law.'],
  ['14. Changes','This notice should show an effective date and version. Material changes should be communicated where required.']
 ]},
 {id:'cancellation',title:'Cancellation & Refund Policy',eyebrow:'COMMERCIAL POLICY',intro:'Template for outlet-specific cancellation, refund and credit rules.',sections:[
  ['1. Before preparation','State the latest time a customer may cancel for a refund or credit before meal preparation or dispatch.'],
  ['2. After preparation','Explain when cancellation is no longer refundable because preparation, packing or delivery has started, subject to mandatory legal rights.'],
  ['3. Refund method','State whether approved refunds return to the original payment method or are issued as customer credit, and the expected processing time.'],
  ['4. Service failures','State the remedy for missing, damaged, materially incorrect or late orders and how customers should report the issue.'],
  ['5. Taxes and fees','Explain whether delivery fees, service fees and other charges are refundable when the underlying order is cancelled.'],
  ['6. Contact','Publish the outlet support contact and any evidence customers should provide for a claim.']
 ]},
 {id:'skip-reschedule',title:'Meal Skip & Rescheduling Policy',eyebrow:'MEAL CHANGES',intro:'Template for the outlet’s operational rules on skipped and rescheduled meals.',sections:[
  ['1. Skip cut-off','State the exact local-time cut-off for a free meal skip.'],
  ['2. Late skip','State any late-skip charge, when it applies, where it is displayed, and whether it is credited to the outlet or platform.'],
  ['3. Rescheduling','State how unused meals can be moved, the latest permitted date, permitted slots and any serviceability restrictions.'],
  ['4. Expiry','State when an unused or skipped meal expires if it is not rescheduled.'],
  ['5. Exceptions','State any operational or legal exceptions, including failures caused by the outlet or delivery system.']
 ]},
 {id:'delivery',title:'Delivery Policy',eyebrow:'DELIVERY',intro:'Template describing delivery windows, addresses, serviceability and failed delivery charges.',sections:[
  ['1. Delivery areas','The outlet serves only locations that are shown as serviceable by its configured city, area or radius rules.'],
  ['2. Exact location','Customers should select the exact delivery point on the map and verify editable address details before saving.'],
  ['3. Delivery window','Publish the normal delivery windows and explain that actual arrival may vary due to traffic, weather, safety or operational conditions.'],
  ['4. Failed delivery','State what happens when the customer is unavailable, the address is inaccessible, or the pin/address is incorrect. Any re-delivery charge must be disclosed before it applies where required.'],
  ['5. Address changes','State the cut-off for changing an address and whether a new delivery fee may apply.']
 ]},
 {id:'allergen',title:'Allergen & Dietary Disclaimer',eyebrow:'FOOD SAFETY',intro:'Template disclaimer to accompany ingredient and allergen information.',sections:[
  ['1. Ingredient information','The outlet should publish current ingredient and allergen information for each meal where required and update it when recipes change.'],
  ['2. Cross-contact','Meals may be prepared in environments where allergens are handled. Exact cross-contact risk depends on the outlet’s food preparation controls.'],
  ['3. Customer responsibility','Customers with severe allergies or medically required diets should review ingredients, contact the outlet before ordering and make their own informed decision.'],
  ['4. Medical advice','Meal information is not medical advice, diagnosis or treatment. Customers should consult a qualified healthcare professional for medical or nutrition decisions.']
 ]},
 {id:'payment-discounts',title:'Payment, Pricing & Promotional Terms',eyebrow:'PAYMENTS',intro:'Template for pricing, taxes, payment failures and promotional discounts.',sections:[
  ['1. Prices','Display meal prices and mandatory charges clearly before the customer confirms payment.'],
  ['2. Discount codes','Discount codes may have outlet-defined eligibility, validity dates, usage limits and maximum discount amounts. The code’s terms should be visible before confirmation.'],
  ['3. Payment authorisation','Orders are confirmed only when the required payment status is received. Failed, reversed or disputed payments may prevent order creation or require follow-up.'],
  ['4. Pricing corrections','If a genuine pricing error is identified before fulfilment, the outlet should communicate the correction and available options as required by applicable law.']
 ]},
];

export default function LegalDocuments({documentId='terms',outletName='Your outlet',onBack}){
 const doc=useMemo(()=>CUSTOMER_DOCUMENTS.find(x=>x.id===documentId)||CUSTOMER_DOCUMENTS[0],[documentId]);
 return <div className="legalPage">
  <header className="legalHeader">
   <div><span className="eyebrow">{doc.eyebrow}</span><h1>{doc.title}</h1><p>{doc.intro}</p></div>
   {onBack&&<button className="secondary" onClick={onBack}>← Back</button>}
  </header>
  <div className="legalMeta"><span><b>Draft</b> · For legal review before publication</span><span>Applies to: <b>{outletName}</b></span></div>
  <div className="legalLayout">
   <aside className="legalIndex">{CUSTOMER_DOCUMENTS.map(x=><button key={x.id} className={x.id===doc.id?'active':''} onClick={()=>{window.history.pushState({},'',\`?legal=\${x.id}\`);window.dispatchEvent(new PopStateEvent('popstate'))}}>{x.title}</button>)}</aside>
   <article className="legalBody"><div className="legalDraftNotice"><b>Draft template</b><span>Replace business/contact placeholders and configure outlet-specific commercial rules before making this document effective.</span></div>{doc.sections.map(([heading,body])=><section key={heading}><h2>{heading}</h2><p>{body}</p></section>)}<section><h2>Document control</h2><p><b>Version:</b> Draft 0.1<br/><b>Effective date:</b> To be set by the outlet<br/><b>Last updated:</b> 7 October 2026<br/><b>Contact:</b> Publish the outlet’s legal/business contact details here.</p></section></article>
  </div>
 </div>;
}

export {CUSTOMER_DOCUMENTS};