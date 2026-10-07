import React,{useMemo}from'react';

const OUTLET_DOCUMENTS=[
 {id:'saas-terms',title:'HealthApp SaaS Terms & Conditions',eyebrow:'OUTLET AGREEMENT',intro:'Draft agreement governing the outlet’s use of HealthApp as a software-as-a-service platform.',sections:[
  ['1. Parties and service','HealthApp provides the software platform and related services. The outlet uses the platform to manage its business, customers, menus, subscriptions, delivery operations and related workflows.'],
  ['2. Account and access','The outlet is responsible for authorised users, credentials, role assignment and secure use of its account.'],
  ['3. Subscription and fees','The applicable SaaS plan, billing cycle, included customer allowance, setup fee and transaction/service charges are shown in the outlet subscription. Taxes and payment obligations apply as stated.'],
  ['4. Outlet responsibilities','The outlet is responsible for the legality, accuracy and quality of its business information, menus, prices, nutrition claims, ingredient information, allergen information, customer-facing policies, delivery promises and customer support.'],
  ['5. Customer relationship','The outlet remains responsible for its commercial relationship with customers, including meal fulfilment, cancellation, refund, skip, rescheduling, delivery and complaint handling.'],
  ['6. Content and intellectual property','The outlet grants HealthApp the rights needed to host, display and process outlet-provided content for operating the service. Each party retains its own pre-existing intellectual property.'],
  ['7. Data protection','The parties should define controller/processor roles and enter any required data processing terms. HealthApp may use approved subprocessors for hosting, storage, communications, analytics, security and other platform functions.'],
  ['8. Security and acceptable use','The outlet must not misuse the service, upload malicious content, circumvent security, or use the platform for unlawful purposes.'],
  ['9. Availability and third-party services','The service may depend on cloud, mapping, payment, communications and other third-party providers. Availability can be affected by maintenance, provider outages or events outside reasonable control.'],
  ['10. Suspension and termination','HealthApp may suspend or terminate access for non-payment, material breach, unlawful use, security threats or other grounds stated in the final agreement. The outlet should be given the notice and cure periods required by law or contract.'],
  ['11. Data export and deletion','On termination, the parties should define export availability, retention periods and deletion timing, subject to legal and security obligations.'],
  ['12. Warranties and liability','The final agreement should define service warranties, exclusions, liability caps, indemnities and any mandatory-law carve-outs after legal review.'],
  ['13. Confidentiality','Each party should protect the other party’s confidential information and use it only for the agreed business relationship.'],
  ['14. Governing law and disputes','State the governing law, venue and dispute-resolution process in the final signed agreement.']
 ]},
 {id:'dpa',title:'HealthApp Data Processing Addendum',eyebrow:'DATA PROCESSING',intro:'Draft addendum for personal-data processing carried out by HealthApp on behalf of an outlet.',sections:[
  ['1. Roles','The outlet should be identified as controller and HealthApp as processor where HealthApp processes customer data solely on the outlet’s documented instructions. Some processing may require a separate controller analysis.'],
  ['2. Processing scope','Document the subject matter, duration, nature, purpose, categories of data and categories of data subjects.'],
  ['3. Instructions','HealthApp processes personal data only on documented instructions, except where applicable law requires processing.'],
  ['4. Confidentiality','Authorised personnel with access to personal data should be bound by confidentiality obligations.'],
  ['5. Security measures','The final DPA should describe appropriate technical and organisational controls, access management, encryption where appropriate, logging, resilience, backups and incident handling.'],
  ['6. Subprocessors','The final DPA should list or provide a mechanism for approved subprocessors and define notice and objection procedures where required.'],
  ['7. Data-subject assistance','HealthApp should provide reasonable assistance with rights requests, security obligations, breach response and other controller duties required by applicable law.'],
  ['8. International transfers','Where personal data leaves the relevant jurisdiction, the parties should use the legally required transfer mechanism and safeguards.'],
  ['9. Deletion or return','After service termination, personal data should be returned or deleted according to the outlet’s instructions and legal retention requirements.'],
  ['10. Audit and compliance','The final DPA should define evidence, audit and compliance assistance arrangements proportionate to the services and legal requirements.']
 ]},
 {id:'acceptable-use',title:'HealthApp Acceptable Use & Platform Rules',eyebrow:'PLATFORM RULES',intro:'Draft rules for safe, lawful and reliable use of the HealthApp SaaS platform.',sections:[
  ['1. Lawful use','Use HealthApp only for lawful business and customer-service activities.'],
  ['2. Account misuse','Do not share privileged credentials improperly, impersonate another user or bypass access controls.'],
  ['3. Content standards','Do not upload unlawful, infringing, malicious or misleading content.'],
  ['4. Customer treatment','Do not use the platform to harass, discriminate against, defraud or otherwise mistreat customers or staff.'],
  ['5. Security','Do not introduce malware, attempt unauthorised access, scan or attack HealthApp infrastructure, or interfere with availability.'],
  ['6. Data use','Do not use customer data for purposes that are inconsistent with the disclosed service, applicable law or the outlet’s documented instructions.'],
  ['7. Enforcement','HealthApp may restrict or suspend access when reasonably necessary to protect the service, users, data or legal compliance.']
 ]},
];

export default function OutletLegalDocuments({documentId='saas-terms',outletName='Your outlet',onBack}){
 const doc=useMemo(()=>OUTLET_DOCUMENTS.find(x=>x.id===documentId)||OUTLET_DOCUMENTS[0],[documentId]);
 return <div className="legalPage outletLegalPage">
  <header className="legalHeader"><div><span className="eyebrow">{doc.eyebrow}</span><h1>{doc.title}</h1><p>{doc.intro}</p></div>{onBack&&<button className="secondary" onClick={onBack}>← Back to settings</button>}</header>
  <div className="legalMeta"><span><b>Draft</b> · Legal review required before acceptance</span><span>Outlet: <b>{outletName}</b></span></div>
  <div className="legalLayout"><aside className="legalIndex">{OUTLET_DOCUMENTS.map(x=><button key={x.id} className={x.id===doc.id?'active':''} onClick={()=>{window.history.pushState({},'',\`?legal=\${x.id}\`);window.dispatchEvent(new PopStateEvent('popstate'))}}>{x.title}</button>)}</aside><article className="legalBody"><div className="legalDraftNotice"><b>Draft template</b><span>These pages are implementation drafts. Business-specific details, commercial terms and applicable law must be reviewed before publication.</span></div>{doc.sections.map(([heading,body])=><section key={heading}><h2>{heading}</h2><p>{body}</p></section>)}<section><h2>Document control</h2><p><b>Version:</b> Draft 0.1<br/><b>Effective date:</b> To be set<br/><b>Last updated:</b> 7 October 2026</p></section></article></div>
 </div>;
}
export {OUTLET_DOCUMENTS};