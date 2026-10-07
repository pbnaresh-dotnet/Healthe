import React,{useEffect,useMemo,useState}from'react';
import{createRoot}from'react-dom/client';
import{locations,money,outletDemo,outletOnboarding,openCashfreeCheckout}from'@healthapp/shared';
import'./styles.css';

const OUTLET_APP_URL=import.meta.env.VITE_OUTLET_APP_URL||'https://outlet.broccoly.in';

const SETUP_FEE=5000;
const EMPTY={businessType:'Individual',outletName:'',city:'',state:'',pincode:'',addressLine1:'',addressLine2:'',ownerName:'',ownerPhone:'',email:'',password:''};

const featureCards=[
 {icon:'↻',title:'Manage subscriptions',text:'Turn recurring customers into predictable pre-orders and plan your week with confidence.',tag:'RECURRING REVENUE',metric:'More predictable demand',image:'https://images.unsplash.com/photo-1683106215993-811ec358430f?auto=format&fit=crop&w=900&q=82'},
 {icon:'◈',title:'Menu & recipes',text:'Keep healthy recipes, nutrition, ingredients and allergens organised in one place.',tag:'HEALTHY MENU',metric:'Fresh meals, clearly managed',image:'https://images.unsplash.com/photo-1662714208483-3480ccd2de39?auto=format&fit=crop&w=900&q=82'},
 {icon:'▦',title:'Kitchen orders',text:'Know what needs cooking before the rush. Plan ingredients and portions from real orders.',tag:'SMART KITCHEN',metric:'Prep from real demand',image:'https://images.unsplash.com/photo-1683105880651-206bbedd7e2e?auto=format&fit=crop&w=900&q=82'},
 {icon:'⌖',title:'Delivery management',text:'Coordinate delivery areas, routes and drivers without juggling separate spreadsheets.',tag:'LAST-MILE',metric:'Routes, drivers & slots',image:'https://images.unsplash.com/photo-1778825628168-31dc884db219?auto=format&fit=crop&w=900&q=82'},
 {icon:'♙',title:'Customer management',text:'Keep profiles, preferences, allergies, addresses and subscription history together.',tag:'CUSTOMER CARE',metric:'One view of every customer',image:'https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=900&q=82'},
 {icon:'↗',title:'Reports & analytics',text:'See sales, subscriptions and customer growth so you can make smarter decisions.',tag:'GROWTH',metric:'Decisions backed by data',image:'https://images.unsplash.com/photo-1784729553968-07da5d7b7c99?auto=format&fit=crop&w=900&q=82'}
];

const benefits=[
 ['▣','Branded storefront','Your own domain'],
 ['↻','Subscription ready','Recurring revenue'],
 ['▦','End-to-end operations','Kitchen to delivery'],
 ['♙','Customer management','Grow and retain'],
 ['↗','Reports & insights','Make better decisions']
];

function Brand(){return <a className="brand" href="#top" aria-label="Broccoly home"><span className="brandMark">B</span><span><b>Broccoly</b><small>Healthy food business SaaS</small></span></a>}

function DemoRequestModal({onClose}){
 const[email,setEmail]=useState(''),[businessName,setBusinessName]=useState(''),[busy,setBusy]=useState(false),[message,setMessage]=useState(''),[error,setError]=useState('');
 const submit=async e=>{e.preventDefault();try{setBusy(true);setError('');const x=await outletDemo.request({email,businessName:businessName||null});setMessage(x?.message||'Your 7-day demo account has been created. Check your email for login details.')}catch(x){setError(x.message||'Unable to create demo account.')}finally{setBusy(false)}};
 return <div className="modalBackdrop" role="dialog" aria-modal="true" aria-labelledby="demoTitle" onMouseDown={e=>e.target===e.currentTarget&&!busy&&onClose()}><div className="demoModal"><div className="modalHead"><div><span className="eyebrow">FREE 7-DAY DEMO</span><h2 id="demoTitle">See Broccoly from the inside.</h2><p>Explore subscriptions, kitchen operations, customers and delivery workflows with sample data.</p></div><button className="iconButton" onClick={onClose} disabled={busy}>×</button></div>{message?<div className="successBox"><strong>✓ Demo account ready</strong><span>{message}</span><small>Demo access expires automatically after 7 days.</small><button className="primary" onClick={onClose}>Done</button></div>:<form onSubmit={submit} className="stackForm"><label><span>Business / outlet name</span><input value={businessName} onChange={e=>setBusinessName(e.target.value)} placeholder="Example: Fit Food Kitchen"/></label><label><span>Email *</span><input type="email" value={email} onChange={e=>setEmail(e.target.value)} placeholder="you@yourbusiness.com" required/></label>{error&&<div className="errorBox">{error}</div>}<div className="includeBox"><b>What you can test</b><span>✓ Customer profiles & addresses</span><span>✓ Meal subscriptions & packages</span><span>✓ Recipes, menus & kitchen orders</span><span>✓ Drivers, routes & deliveries</span></div><div className="modalActions"><button type="button" className="secondary" onClick={onClose} disabled={busy}>Cancel</button><button className="primary" disabled={busy}>{busy?'Creating demo…':'Request demo account →'}</button></div></form>}</div></div>
}

function Hero({onGetStarted,onDemo}){return <section className="hero" id="top"><div className="heroCopy"><span className="eyebrow">THE COMPLETE MEAL BUSINESS PLATFORM</span><h1>Run your meal business with <em>Broccoly</em></h1><p>Your own branded website, subscriptions, meal plans, kitchen operations, delivery management and customer management — all in one platform.</p><div className="heroActions"><button className="primary heroPrimary" onClick={onGetStarted}>Start Your Business →</button><button className="secondary" onClick={onDemo}>Request a Free Demo</button></div><div className="heroProof"><span>✓ 7-day demo</span><span>✓ No credit card required</span><span>✓ Test with sample data</span></div></div><div className="heroVisual"><div className="heroImageCard"><img src={featureCards[1].image} alt="Healthy meals managed in Broccoly" loading="eager"/><div className="heroDevice"><div className="deviceTop"><span>FitFood</span><b>Healthy Meals</b></div><div className="deviceImage"><img src={featureCards[4].image} alt="" loading="eager"/></div><button>Order now</button></div></div><div className="heroFloat heroFloatOrders"><b>42</b><span>orders planned</span><i>↑ 18%</i></div><div className="heroFloat heroFloatPrep"><b>Better prep</b><span>Plan ingredients from pre-orders</span></div></div></section>}

function BenefitStrip(){return <section className="benefitStrip">{benefits.map(([icon,title,text])=><article key={title}><span>{icon}</span><b>{title}</b><small>{text}</small></article>)}</section>}

function HowItWorks({onGetStarted,onDemo}){return <section className="section how" id="how"><div className="sectionIntro"><span className="eyebrow">HOW IT WORKS</span><h2>Start your meal business in easy steps</h2><p>Choose a plan, complete onboarding, configure your brand and start accepting customers.</p></div><div className="steps">{[['1','Choose a Plan','Select the plan that fits your business needs.'],['2','Create Account','Register your outlet and provide business details.'],['3','Setup & Verify','Complete setup and submit required documents.'],['4','Customize','Configure your brand, menu, delivery areas and settings.'],['5','Go Live','Start accepting customers and subscriptions.']].map(([n,t,d],i)=><React.Fragment key={t}><article><strong>{n}</strong><b>{t}</b><span>{d}</span></article>{i<4&&<i>→</i>}</React.Fragment>)}</div><div className="howDemo"><div><span className="eyebrow">NOT READY TO SUBSCRIBE?</span><h3>Try a free demo for 7 days.</h3><p>Explore the outlet dashboard with sample data, no credit card required.</p></div><button className="primary" onClick={onDemo}>Request Demo Account →</button></div><div className="howCta"><button className="primary" onClick={onGetStarted}>Start Now →</button><span>Your brand · Your domain · Your customers</span></div></section>}

function Features(){return <section className="section features" id="features"><div className="sectionIntro left"><span className="eyebrow">ONE PLATFORM FOR YOUR DAILY WORK</span><h2>Everything you need to run and grow your outlet</h2><p>Less switching between tools. More time improving your food and serving customers.</p></div><div className="featureGrid">{featureCards.map((f,i)=><article className="featureCard" key={f.title}><div className="featureImage"><img src={f.image} alt="" loading="lazy"/><span>{f.tag}</span><i>{f.icon}</i></div><div className="featureBody"><div className="featureMetric">✦ {f.metric}</div><h3>{f.title}</h3><p>{f.text}</p><b>Explore →</b></div></article>)}</div></section>}

function BrandControl({onGetStarted}){return <section className="section brandControl"><div className="controlVisual"><div className="siteFrame"><div className="siteTop"><span className="siteLogo">F</span><b>FitFood</b><span>Menu</span><span>Plans</span><span>About</span><button>Subscribe</button></div><div className="siteHero"><small>HEALTHY MEALS · YOUR BRAND</small><h3>Healthy meals.<br/><em>Made simple.</em></h3><p>Fresh meals, delivered to your schedule.</p></div><div className="siteCards"><span>Breakfast plans</span><span>Weekly subscriptions</span><span>Delivery areas</span></div></div></div><div className="controlCopy"><span className="eyebrow">YOUR BUSINESS, YOUR BRAND</span><h2>Broccoly powers the platform. Customers see your brand.</h2><p>Use your own logo, colours, customer-facing policies, domain and menu. Broccoly stays behind the scenes as your SaaS infrastructure.</p><div className="checkList"><span>✓ Custom domain or Broccoly subdomain</span><span>✓ Outlet-controlled customer terms & policies</span><span>✓ Outlet-specific pricing and delivery rules</span><span>✓ Separate customers and data per outlet</span></div><button className="secondary" onClick={onGetStarted}>View plans & get started →</button></div></section>}

function Pricing({plans,loading,onChoose}){const[cycle,setCycle]=useState('Monthly');return <section className="section pricing" id="pricing"><div className="sectionIntro"><span className="eyebrow">PRICING</span><h2>Simple, transparent pricing</h2><p>Start with a plan and scale as your business grows. Your customer meal revenue remains your outlet's business.</p></div>{loading?<div className="loadingCard"><div className="spinner"/><b>Loading current plans…</b><span>Fetching the latest Broccoly subscription options.</span></div>:<><div className="billingToggle"><button className={cycle==='Monthly'?'active':''} onClick={()=>setCycle('Monthly')}>Monthly</button><button className={cycle==='Annual'?'active':''} onClick={()=>setCycle('Annual')}>Annual</button><span>{cycle==='Annual'?'Annual billing selected':'Save on annual billing'}</span></div><div className="plans">{plans.map((p,i)=>{const fee=cycle==='Annual'?p.annualFee:p.monthlyFee;return <article className={'plan '+(i===1?'featured':'')} key={p.id}>{i===1&&<span className="popular">MOST POPULAR</span>}<div className="planIcon">{i===0?'🌱':i===1?'📈':'⭐'}</div><h3>{p.name}</h3><p>{p.description||'Tools and capacity for your outlet.'}</p><div className="planPrice">{money(fee)}<small>/{cycle.toLowerCase()}</small></div>{cycle==='Monthly'?<div className="annual">{money(p.annualFee)} / year</div>:<div className="annual">Pay annually · save where offered</div>}<div className="planFacts"><span>✓ {p.includedActiveCustomers} included active customers</span><span>✓ {Number(p.customerTransactionFeePercent||0)}% customer transaction fee</span><span>✓ {money(p.additionalCustomerFee)} per additional customer</span></div><button className={i===1?'primary':'secondary'} onClick={()=>onChoose(p.id)}>Choose {p.name} →</button></article>})}</div></>}<div className="pricingNote"><b>One-time onboarding</b><span>₹{SETUP_FEE.toLocaleString('en-IN')} setup fee · Recurring SaaS billing starts after verification and activation.</span></div></section>}

function WhyBroccoly(){return <section className="section why"><div className="whyVisual"><div className="whyCard"><span>PRE-ORDERS</span><b>42 meals</b><small>planned before prep starts</small><div className="whyBars"><i/><i/><i/><i/></div></div><div className="whyOrb">B</div></div><div className="whyCopy"><span className="eyebrow">WHY CHOOSE BROCCOLY</span><h2>Built for meal businesses</h2><p>We understand the unique needs of meal-delivery and subscription businesses.</p><div className="whyGrid">{[['🚀','Launch Faster','Get your business online quickly without technical expertise.'],['▥','Focus on Your Food','We handle the technology so you can focus on great food and customer experience.'],['◆','Secure & Reliable','Enterprise-grade security and reliable infrastructure.'],['◉','Ongoing Support','Our team is here to help you succeed at every step.']].map(([icon,t,d])=><article key={t}><span>{icon}</span><b>{t}</b><small>{d}</small></article>)}</div></div></section>}

function Testimonials(){const rows=[['FitFood','Bangalore','“Broccoly helped us launch our branded website and subscription service quickly.”'],['HealthyBites','Chennai','“The platform is easy to use and the support team is always helpful.”'],['GreenBowl','Hyderabad','“From kitchen operations to delivery management, everything is in one place.”']];return <section className="section testimonials"><div className="sectionIntro"><span className="eyebrow">TESTIMONIALS</span><h2>Trusted by meal businesses like yours</h2><p>See how a single workspace can simplify the whole operation.</p></div><div className="testimonialGrid">{rows.map(([n,c,q])=><article key={n}><div className="testimonialAvatar">{n[0]}</div><p>{q}</p><div className="testimonialPerson"><div><b>{n}</b><span>{c}</span></div><strong>★★★★★</strong></div></article>)}</div></section>}

function StartOptions({onSubscribe,onDemo,onBack}){return <div className="registerShell"><div className="registerTop"><Brand/><button className="linkButton" onClick={onBack}>← Back to Broccoly</button></div><main className="startMain"><div className="startIntro"><span className="eyebrow">GET STARTED</span><h1>Choose how you want to begin with Broccoly</h1><p>Subscribe now to create your outlet, or explore the real workflow with a free 7-day demo.</p></div><div className="startChoices"><article className="startChoice"><div className="startChoiceIcon">▣</div><span className="eyebrow">SUBSCRIBE NOW</span><h2>Start your outlet</h2><p>Choose a SaaS plan, create your business account and continue through onboarding and payment.</p><div className="startFacts"><span>✓ Choose monthly or annual plan</span><span>✓ Create your outlet account</span><span>✓ Complete verification after signup</span><span>✓ Configure your own brand and domain</span></div><button className="primary large" onClick={()=>onSubscribe()}>Choose a Plan →</button></article><article className="startChoice recommended"><span className="popular">RECOMMENDED</span><div className="startChoiceIcon">◌</div><span className="eyebrow">TRY FREE DEMO</span><h2>Test Broccoly for 7 days</h2><p>Get a temporary outlet account with sample data and explore the full workflow before subscribing.</p><div className="startFacts"><span>✓ Full access to the demo outlet dashboard</span><span>✓ Test customers, subscriptions and orders</span><span>✓ Test kitchen and delivery workflows</span><span>✓ No credit card required</span></div><button className="primary large" onClick={onDemo}>Request Demo Account →</button></article></div></main></div>}

function Registration({plans,initialPlan,onBack,onLogin}) {
 const [selectedPlan,setSelectedPlan]=useState(initialPlan||null);
 const [cycle,setCycle]=useState('Monthly');
 const [step,setStep]=useState(1);
 const [details,setDetails]=useState(EMPTY);
 const [cities,setCities]=useState([]);
 const [busy,setBusy]=useState(false);
 const [error,setError]=useState('');
 const [success,setSuccess]=useState(false);

 useEffect(() => {
  locations.cities().then(x=>setCities(x||[])).catch(()=>setCities([]));
 },[]);

 const cityOptions=useMemo(()=>cities||[],[cities]);
 const selected=selectedPlan;
 const fee=selected ? (cycle==='Annual' ? selected.annualFee : selected.monthlyFee) : 0;

 const saveBusiness=e => {
  e.preventDefault();
  setError('');
  if(details.password.length<8) {
   setError('Password must be at least 8 characters.');
   return;
  }
  setStep(3);
 };

 const waitForOnboardingPayment=async session => {
  const deadline=Date.now()+60000;
  while(Date.now()<deadline) {
   const current=await outletOnboarding.get(session.id,session.accessKey);
   const status=String(current?.paymentStatus||'').toLowerCase();
   if(status==='paid') return current;
   if(status==='failed') throw new Error('Cashfree payment failed. Please retry.');
   await new Promise(resolve=>setTimeout(resolve,2000));
  }
  throw new Error('Payment confirmation is taking longer than expected. Please check your payment status and try again.');
 };

 const pay=async() => {
  if(!selected) return;
  try {
   setBusy(true);
   setError('');
   const session=await outletOnboarding.pay({
    saasPlanId:selected.id,
    billingCycle:cycle,
    businessType:details.businessType,
    outletName:details.outletName,
    city:details.city,
    state:details.state,
    pincode:details.pincode,
    addressLine1:details.addressLine1,
    addressLine2:details.addressLine2,
    ownerName:details.ownerName,
    ownerPhone:details.ownerPhone,
    email:details.email,
    password:details.password
   });
   if(String(session?.paymentStatus||'').toLowerCase()==='paid') {
    setSuccess(true);
    return;
   }
   if(!session?.paymentSessionId) throw new Error('Cashfree payment session was not created.');
   await openCashfreeCheckout(session.paymentSessionId,{
    mode:String(import.meta.env.VITE_CASHFREE_MODE||'sandbox').toLowerCase(),
    redirectTarget:'_modal'
   });
   await waitForOnboardingPayment(session);
   setSuccess(true);
  } catch(e) {
   setError(e.message||'Unable to complete outlet payment.');
  } finally {
   setBusy(false);
  }
 };

 const renderPlanStep=() => (
  <section className="registerCard">
   <div className="registerIntro">
    <span className="eyebrow">STEP 1 OF 4 · PLAN</span>
    <h1>Choose your plan</h1>
    <p>You can change your SaaS plan later as your business grows.</p>
   </div>
   <div className="cycleToggle">
    <button className={cycle==='Monthly'?'active':''} onClick={()=>setCycle('Monthly')}>Monthly</button>
    <button className={cycle==='Annual'?'active':''} onClick={()=>setCycle('Annual')}>Annual <small>Save with annual billing</small></button>
   </div>
   <div className="registerPlans">
    {plans.map((p,i)=>(
     <article className={'registerPlan '+(selected?.id===p.id?'selected':'')} key={p.id} onClick={()=>setSelectedPlan(p)}>
      <div className="planIcon">{i===0?'🌱':i===1?'📈':'⭐'}</div>
      <h3>{p.name}</h3>
      <p>{p.description||'Tools and capacity for your outlet.'}</p>
      <strong>{money(cycle==='Annual'?p.annualFee:p.monthlyFee)}<small>/{cycle.toLowerCase()}</small></strong>
      <span>✓ {p.includedActiveCustomers} included customers</span>
      <span>✓ {Number(p.customerTransactionFeePercent||0)}% customer transaction fee</span>
      <span>✓ {money(p.additionalCustomerFee)} per additional customer</span>
      <button type="button" className={selected?.id===p.id?'primary':'secondary'} onClick={e=>{e.stopPropagation();setSelectedPlan(p)}}>{selected?.id===p.id?'Selected':'Choose '+p.name}</button>
     </article>
    ))}
   </div>
   <div className="registerBottom">
    <div><b>One-time setup fee</b><span>{money(SETUP_FEE)} at the payment step</span></div>
    <button className="primary large" disabled={!selected} onClick={()=>setStep(2)}>Continue to business details →</button>
   </div>
  </section>
 );

 const renderDetailsStep=() => (
  <section className="registerCard">
   <div className="registerIntro">
    <span className="eyebrow">STEP 2 OF 4 · BUSINESS DETAILS</span>
    <h1>Business details</h1>
    <p>Tell us about your outlet.</p>
   </div>
   <form onSubmit={saveBusiness}>
    <div className="formSection"><b>Outlet details</b><span>Name and address for your business.</span></div>
    <div className="formGrid">
     <label><span>Outlet name *</span><input value={details.outletName} onChange={e=>setDetails({...details,outletName:e.target.value})} required /></label>
     <label><span>Business type *</span><select value={details.businessType} onChange={e=>setDetails({...details,businessType:e.target.value})}><option value="Individual">Individual / Unregistered</option><option value="RegisteredBusiness">Registered business</option></select></label>
     <label>
      <span>City *</span>
      {cityOptions.length ? (
       <select value={details.city} onChange={e=>{const v=e.target.value;const row=cityOptions.find(x=>x.city===v);setDetails({...details,city:v,state:row?.state||details.state})}} required>
        <option value="">Select city</option>
        {cityOptions.map(x=><option key={x.city+'|'+x.state} value={x.city}>{x.city} · {x.state}</option>)}
       </select>
      ) : (
       <input value={details.city} onChange={e=>setDetails({...details,city:e.target.value})} placeholder="City" required />
      )}
     </label>
     <label><span>State *</span><input value={details.state} onChange={e=>setDetails({...details,state:e.target.value})} required /></label>
     <label><span>Pincode *</span><input value={details.pincode} onChange={e=>setDetails({...details,pincode:e.target.value})} required /></label>
     <label className="span2"><span>Address line 1 *</span><input value={details.addressLine1} onChange={e=>setDetails({...details,addressLine1:e.target.value})} placeholder="Building / flat / street" required /></label>
     <label className="span2"><span>Address line 2</span><input value={details.addressLine2} onChange={e=>setDetails({...details,addressLine2:e.target.value})} placeholder="Area / landmark" /></label>
    </div>
    <div className="formSection"><b>Owner & account</b><span>These credentials will be used to access the outlet workspace.</span></div>
    <div className="formGrid">
     <label><span>Owner name *</span><input value={details.ownerName} onChange={e=>setDetails({...details,ownerName:e.target.value})} required /></label>
     <label><span>Email *</span><input type="email" value={details.email} onChange={e=>setDetails({...details,email:e.target.value.toLowerCase()})} required /></label>
     <label><span>Contact number *</span><input value={details.ownerPhone} onChange={e=>setDetails({...details,ownerPhone:e.target.value})} required /></label>
     <label><span>Password *</span><input type="password" minLength="8" value={details.password} onChange={e=>setDetails({...details,password:e.target.value})} placeholder="At least 8 characters" required /></label>
    </div>
    <div className="infoBox"><b>What happens next?</b><span>The setup payment creates your outlet account. Then sign in to complete verification documents and submit the outlet for Broccoly review.</span></div>
    <div className="registerBottom">
     <button type="button" className="secondary" onClick={()=>setStep(1)}>← Change plan</button>
     <button className="primary large">Continue to payment →</button>
    </div>
   </form>
  </section>
 );

 const renderPaymentStep=() => (
  <section className="registerCard">
   <div className="registerIntro">
    <span className="eyebrow">STEP 3 OF 4 · PAYMENT</span>
    <h1>Payment Summary</h1>
    <p>Review your plan and complete secure payment.</p>
   </div>
   <div className="paymentGrid">
    <section className="summaryCard">
     <span className="eyebrow">SELECTED PLAN</span>
     <h3>{selected?.name}</h3>
     <div className="line"><span>{cycle} subscription</span><b>{money(fee)}</b></div>
     <div className="line"><span>One-time setup fee</span><b>{money(SETUP_FEE)}</b></div>
     <div className="line"><span>Taxes</span><b>As applicable</b></div>
     <div className="total"><span>Total today</span><strong>{money(SETUP_FEE)}</strong></div>
    </section>
    <section className="summaryCard">
     <div className="secure">SECURE CHECKOUT · {String(import.meta.env.VITE_CASHFREE_MODE||'sandbox').toUpperCase()}</div>
     <div className="accountPreview">
      <span>Account email</span>
      <b>{details.email}</b>
      <small>{details.ownerName} · {details.ownerPhone}</small>
      <small>{details.outletName} · {details.city}</small>
     </div>
     <div className="infoBox"><b>Secure Cashfree checkout</b><span>Card, UPI and other supported payment methods are handled by Cashfree. Broccoly never receives or stores your card credentials.</span></div>
     <div className="registerBottom">
      <button className="secondary" onClick={()=>setStep(2)} disabled={busy}>← Edit details</button>
      <button className="primary large" disabled={busy} onClick={pay}>{busy?'Opening secure checkout…':'Pay '+money(SETUP_FEE)+' securely & continue'}</button>
     </div>
    </section>
   </div>
  </section>
 );

 if(success) {
  return (
   <div className="registerShell">
    <div className="registerTop">
     <Brand/>
     <button className="linkButton" onClick={onLogin}>Go to outlet login →</button>
    </div>
    <div className="successPage">
     <div className="successIcon">✓</div>
     <span className="eyebrow">PAYMENT CONFIRMED</span>
     <h1>Welcome to Broccoly!</h1>
     <p>Your <b>{selected?.name}</b> setup payment has been confirmed. Sign in to complete verification, upload documents and configure your storefront.</p>
     <div className="successSteps">
      <span><b>1</b>We will verify your business details and documents</span>
      <span><b>2</b>You will receive email with further instructions</span>
      <span><b>3</b>Once verified, sign in to your outlet dashboard and start configuring</span>
     </div>
     <button className="primary large" onClick={onLogin}>Go to Dashboard →</button>
    </div>
   </div>
  );
 }

 const stepContent = step===1 ? renderPlanStep() : step===2 ? renderDetailsStep() : renderPaymentStep();

 return (
  <div className="registerShell">
   <div className="registerTop">
    <Brand/>
    <button className="linkButton" onClick={onBack}>← Back to Broccoly</button>
   </div>
   <div className="progress">
    {['Plan','Business Details','Payment','Confirmation'].map((label,i)=>(
     <React.Fragment key={label}>
      <div className={(i+1<step?'done ':'')+(i+1===step?'current':'')}>
       <span>{i+1<step?'✓':i+1}</span>
       <small>{label}</small>
      </div>
      {i<3 && <i className={i+1<step?'filled':''}/>}
     </React.Fragment>
    ))}
   </div>
   <main className="registerMain">
    {error && (
     <div className="errorBanner">
      <b>Something needs attention</b>
      <span>{error}</span>
      <button onClick={()=>setError('')}>×</button>
     </div>
    )}
    {stepContent}
   </main>
  </div>
 );
}
function App(){const[plans,setPlans]=useState([]),[planLoading,setPlanLoading]=useState(true),[demoOpen,setDemoOpen]=useState(false),[register,setRegister]=useState(false),[startOpen,setStartOpen]=useState(false),[initialPlan,setInitialPlan]=useState(null);
 useEffect(()=>{outletOnboarding.plans().then(x=>setPlans(x||[])).catch(()=>setPlans([])).finally(()=>setPlanLoading(false))},[]);
 useEffect(()=>{try{const params=new URLSearchParams(window.location.search);if(params.get('register')==='1'){setStartOpen(true)}const planId=params.get('plan');if(planId&&plans.length){setInitialPlan(plans.find(x=>x.id===planId)||null);setRegister(true);setStartOpen(false)}}catch{}},[plans]);
 const openStart=()=>{setStartOpen(true);window.scrollTo({top:0,behavior:'smooth'})};
 const choosePlan=id=>{setInitialPlan(plans.find(x=>x.id===id)||null);setRegister(true);setStartOpen(false);window.scrollTo({top:0,behavior:'smooth'})};
 if(register)return <Registration plans={plans} initialPlan={initialPlan} onBack={()=>setRegister(false)} onLogin={()=>window.location.href=OUTLET_APP_URL}/>;
 if(startOpen)return <StartOptions onSubscribe={()=>{setInitialPlan(null);setRegister(true)}} onDemo={()=>setDemoOpen(true)} onBack={()=>setStartOpen(false)}/>;
 return <><header className="nav"><Brand/><nav><a href="#how">How it works</a><a href="#features">Features</a><a href="#pricing">Pricing</a><a href="#why">Why Broccoly</a><a href="#testimonials">Customers</a></nav><div className="navActions"><button className="linkButton" onClick={()=>window.location.href=OUTLET_APP_URL}>Login</button><button className="secondary demoButton" onClick={()=>setDemoOpen(true)}>Request a demo</button><button className="primary" onClick={openStart}>Start Your Business →</button></div></header><Hero onGetStarted={openStart} onDemo={()=>setDemoOpen(true)}/><BenefitStrip/><HowItWorks onGetStarted={openStart} onDemo={()=>setDemoOpen(true)}/><Features/><BrandControl onGetStarted={openStart}/><Pricing plans={plans} loading={planLoading} onChoose={choosePlan}/><WhyBroccoly/><Testimonials/><section className="finalCta"><div><span className="eyebrow">READY TO BUILD YOUR BUSINESS</span><h2>Give your outlet a platform built for subscriptions.</h2><p>Start with the plan that fits today. Build your customer storefront, configure your operations and scale from there.</p></div><div className="finalCtaActions"><button className="primary large" onClick={openStart}>Choose a plan & register →</button><button className="secondary" onClick={()=>setDemoOpen(true)}>Try the 7-day demo</button></div></section><footer className="footer"><Brand/><div><span>© {new Date().getFullYear()} Broccoly</span><span>Healthy food business SaaS</span></div><div><a href="/terms">SaaS Terms</a><a href="/privacy">Privacy</a><a href="mailto:support@broccoly.in">Support</a></div></footer>{demoOpen&&<DemoRequestModal onClose={()=>setDemoOpen(false)}/>}</>
}
createRoot(document.getElementById('root')).render(<App/>);
