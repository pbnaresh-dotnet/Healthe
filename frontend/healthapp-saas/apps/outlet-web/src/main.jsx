
import React,{useEffect,useMemo,useRef,useState}from'react';
import{createRoot}from'react-dom/client';
import{auth,outletAdmin,outletStaff,catalog,locations,currentUser,money,API_URL,outletDemo}from'@healthapp/shared';
import{MapContainer,TileLayer,CircleMarker,Popup,Polyline,useMap,useMapEvents}from'react-leaflet';
import'leaflet/dist/leaflet.css';
import'./styles.css';
import OutletVerificationCenter from'./OutletVerificationCenter.jsx';
import OutletSettings from'./OutletSettings.jsx';
import OutletLegalDocuments from'./OutletLegalDocuments.jsx';

const ORIGIN=API_URL.replace(/\/api\/?$/,'');
const img=u=>u?(u.startsWith('http')?u:ORIGIN+u):'';
const DAYS=['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'];
const SLOTS=[['Morning',1],['Afternoon',2],['Evening',3],['Night',4]];
const MEAL_TYPES=[['Meal','Meal'],['Juice','Juice'],['Snack','Snack'],['Curd','Curd'],['Starter','Starter'],['Side','Side'],['Add-on','Add-on'],['Soup','Soup'],['Salad','Salad'],['Dessert','Dessert'],['Drink','Drink'],['Other','Other']];
const emptyRecipe={name:'',category:'Veg',mealType:'Meal',calories:0,proteinGrams:0,carbsGrams:0,fatGrams:0,fiberGrams:0,sugarGrams:0,largeCalories:0,largeProteinGrams:0,largeCarbsGrams:0,largeFatGrams:0,largeFiberGrams:0,largeSugarGrams:0,pricePerMeal:0,largePricePerMeal:0,description:'',ingredients:[],allergenIds:[],tags:'',imageUrl:'',isActive:true};
const todayISO=()=>new Date().toISOString().slice(0,10);
const addDays=(iso,n)=>{const d=new Date(iso+'T00:00:00Z');d.setUTCDate(d.getUTCDate()+Number(n||0));return d.toISOString().slice(0,10)};
const dayId=iso=>new Date(iso+'T00:00:00Z').getUTCDay();
const dayName=id=>DAYS[id]||'Day';
const packageKey=(date,slot)=>date+'_'+slot;
const packageItemKey=(date,slot,recipeId)=>date+'_'+slot+'_'+recipeId;
const packageDurationDays={ThreeDays:3,FiveDays:5,OneWeek:7,TwoWeeks:14,OneMonth:28};

function calculateRecipeNutrition(form, ingredients){
 const byId=new Map((ingredients||[]).map(x=>[x.id,x]));
 const makeTotals=()=>({calories:0,proteinGrams:0,carbsGrams:0,fatGrams:0,fiberGrams:0,sugarGrams:0});
 const regular=makeTotals(),large=makeTotals(),missing=[];
 const add=(totals,item,grams)=>{
  const factor=grams/100;
  totals.calories+=Number(item.caloriesPer100g||0)*factor;
  totals.proteinGrams+=Number(item.proteinGramsPer100g||0)*factor;
  totals.carbsGrams+=Number(item.carbsGramsPer100g||0)*factor;
  totals.fatGrams+=Number(item.fatGramsPer100g||0)*factor;
  totals.fiberGrams+=Number(item.fiberGramsPer100g||0)*factor;
  totals.sugarGrams+=Number(item.sugarGramsPer100g||0)*factor;
 };
 for(const row of(form?.ingredients||[])){
  const item=byId.get(row.ingredientId);
  const qty=Number(row.quantity),largeQty=Number(row.largeQuantity??row.quantity);
  if(!item||!Number.isFinite(qty)||qty<=0)continue;
  const unit=String(row.unit||item.defaultUnit||'g').trim().toLowerCase();
  const toGrams=value=>unit==='kg'?value*1000:['g','gram','grams'].includes(unit)?value:null;
  const grams=toGrams(qty),largeGrams=toGrams(Number.isFinite(largeQty)&&largeQty>0?largeQty:qty);
  if(grams===null||largeGrams===null){missing.push(`${item.name} (${row.unit||unit})`);continue;}
  if(!item.nutritionSource){missing.push(`${item.name} (nutrition reference not configured)`);continue;}
  add(regular,item,grams);add(large,item,largeGrams);
 }
 for(const totals of[regular,large])for(const key of Object.keys(totals))totals[key]=Math.round(totals[key]*10)/10;
 return{regular,large,missing:[...new Set(missing)]};
}
const OUTLET_ROLE_NAVS={
 OutletAdmin:['dashboard','kitchen','recipes','menu','ingredient-usage','customers','packages','subscriptions','orders','deliveries','routes','team','settings'],
 OutletManager:['dashboard','kitchen','recipes','menu','ingredient-usage','customers','packages','subscriptions','orders','deliveries','routes','settings'],
 KitchenStaff:['kitchen','recipes','menu','ingredient-usage'],
 Driver:['driver']
};
const OUTLET_ROLE_LABELS={OutletAdmin:'Outlet Admin',OutletManager:'Outlet Manager',KitchenStaff:'Kitchen Staff',Driver:'Driver'};
const OUTLET_NAV_GROUPS=[
 {key:'workspace',label:'Workspace',items:['dashboard']},
 {key:'kitchen',label:'Kitchen',items:['kitchen','recipes','menu','ingredient-usage']},
 {key:'management',label:'Management',items:['customers','packages','subscriptions','orders']},
 {key:'delivery',label:'Delivery Operations',items:['deliveries','routes']},
 {key:'admin',label:'Owner / Admin',roles:['OutletAdmin'],items:['team','settings']},
 {key:'manager-settings',label:'Manager',roles:['OutletManager'],items:['settings']},
 {key:'driver',label:'Driver',roles:['Driver'],items:['driver']}
];


function StandardConfirmModal({request,onClose,onConfirm}){return <div className="standardConfirmBackdrop" role="dialog" aria-modal="true" aria-labelledby="standard-confirm-title" onMouseDown={e=>e.target===e.currentTarget&&onClose()}><div className={'standardConfirmModal '+(request.variant==='danger'?'danger':'')}><div className={'standardConfirmIcon '+(request.variant||'warning')}>{request.variant==='danger'?'×':'!'}</div><div className="standardConfirmContent"><span className={'standardConfirmEyebrow '+(request.variant||'warning')}>{request.variant==='danger'?'CONFIRM ACTION':'PLEASE CONFIRM'}</span><h3 id="standard-confirm-title">{request.title}</h3><p>{request.message}</p><div className="standardConfirmActions"><button type="button" className="secondary" onClick={onClose}>Cancel</button><button type="button" className={request.variant==='danger'?'dangerAction':'primary'} onClick={onConfirm}>{request.confirmLabel||'Continue'}</button></div></div></div></div>}

function Modal({title,onClose,children,wide}){return <div className="overlay" onMouseDown={e=>e.target===e.currentTarget&&onClose()}><div className={wide?'modal wide':'modal'}><div className="modalHead"><h3>{title}</h3><button className="iconBtn" onClick={onClose}>×</button></div>{children}</div></div>}
function Field({label,children,help}){return <label className="field"><span>{label}</span>{children}{help&&<small>{help}</small>}</label>}
function Empty({title,text}){return <div className="empty"><div className="emptyIcon">＋</div><h3>{title}</h3><p>{text}</p></div>}
function Table({columns,rows,empty}){return rows.length?<div className="dataTable"><div className="dataRow header">{columns.map(c=><span key={c}>{c}</span>)}</div>{rows.map((r,i)=><div className="dataRow" key={i}>{r.map((v,j)=><span key={j}>{v}</span>)}</div>)}</div>:<Empty title={empty} text="There is no activity to show yet."/>}


const featureCards=[
 {icon:'↻',title:'Manage subscriptions',text:'Turn recurring customers into predictable pre-orders and plan your week with confidence.',tag:'RECURRING REVENUE',metric:'More predictable demand',image:'https://images.unsplash.com/photo-1683106215993-811ec358430f?auto=format&fit=crop&w=900&q=82'},
 {icon:'◈',title:'Menu & recipes',text:'Keep healthy recipes, nutrition, ingredients and allergens organised in one place.',tag:'HEALTHY MENU',metric:'Fresh meals, clearly managed',image:'https://images.unsplash.com/photo-1662714208483-3480ccd2de39?auto=format&fit=crop&w=900&q=82'},
 {icon:'▦',title:'Kitchen orders',text:'Know what needs cooking before the rush. Plan ingredients and portions from real orders.',tag:'SMART KITCHEN',metric:'Prep from real demand',image:'https://images.unsplash.com/photo-1683105880651-206bbedd7e2e?auto=format&fit=crop&w=900&q=82'},
 {icon:'⌖',title:'Delivery management',text:'Coordinate delivery areas, routes and drivers without juggling separate spreadsheets.',tag:'LAST-MILE',metric:'Routes, drivers & slots',image:'https://images.unsplash.com/photo-1778825628168-31dc884db219?auto=format&fit=crop&w=900&q=82'},
 {icon:'♙',title:'Customer management',text:'Keep profiles, preferences, allergies, addresses and subscription history together.',tag:'CUSTOMER CARE',metric:'One view of every customer',image:'https://images.unsplash.com/photo-1512621776951-a57141f2eefd?auto=format&fit=crop&w=900&q=82'},
 {icon:'↗',title:'Reports & analytics',text:'See sales, subscriptions and customer growth so you can make smarter decisions.',tag:'GROWTH',metric:'Decisions backed by data',image:'https://images.unsplash.com/photo-1784729553968-07da5d7b7c99?auto=format&fit=crop&w=900&q=82'}
];
const benefitItems=[
 ['↻','Predictable orders','Grow through recurring subscriptions'],
 ['✦','Plan ingredients','Prepare closer to what customers actually ordered'],
 ['◷','Reduce food waste','Cook with a clearer daily demand signal'],
 ['⌖','Manage delivery','Organise drivers, routes and delivery slots'],
 ['♙','Build loyalty','Give healthy customers a reason to come back'],
 ['↗','Grow your way','Start small and scale as your outlet grows']
];

function MiniDashboard(){
 return <div className="landingDashboard">
  <div className="landingWindowTop"><div className="landingWindowBrand"><span className="brandMark">H</span><b>HealthApp</b></div><span className="landingWindowUser">Fit Food Kitchen ▾</span></div>
  <div className="landingWindowBody">
   <aside className="landingMiniNav"><b>⌂ Dashboard</b><span>◫ Orders</span><span>↻ Subscriptions</span><span>▦ Kitchen</span><span>⌖ Delivery</span><span>♙ Customers</span><span>◈ Menu & Recipes</span></aside>
   <div className="landingDashMain">
    <div className="landingDashTitle"><div><span>Today at a glance</span><b>Know your day before it starts</b></div><span className="landingLiveBadge">● Live</span></div>
    <div className="landingStatRow"><div><span>Total orders</span><b>42</b><small>+18% this week</small></div><div><span>Subscriptions</span><b>38</b><small>Recurring customers</small></div><div><span>Kitchen</span><b>56</b><small>Meals to prepare</small></div><div><span>Delivery</span><b>12</b><small>Stops to plan</small></div></div>
    <div className="landingDashGrid">
      <div className="landingPanel"><div className="landingPanelHead"><b>Today’s meal plan</b><span>View all →</span></div><div className="landingMeal"><i>☀</i><div><b>Breakfast</b><span>24 meals · 6 subscriptions</span></div><strong>24</strong></div><div className="landingMeal"><i>◉</i><div><b>Lunch</b><span>56 meals · 18 subscriptions</span></div><strong>56</strong></div><div className="landingMeal"><i>◒</i><div><b>Evening</b><span>42 meals · 14 subscriptions</span></div><strong>42</strong></div></div>
      <div className="landingPanel"><div className="landingPanelHead"><b>Order status</b><span>Today</span></div><div className="landingDonut"><div><b>140</b><span>Total meals</span></div></div><div className="landingLegend"><span><i></i>Prepared <b>38</b></span><span><i></i>Out for delivery <b>12</b></span><span><i></i>Delivered <b>80</b></span><span><i></i>Pending <b>10</b></span></div></div>
    </div>
   </div>
  </div>
 </div>
}


function DemoRequestModal({onClose}){
 const[email,setEmail]=useState(''),[businessName,setBusinessName]=useState(''),[busy,setBusy]=useState(false),[message,setMessage]=useState(''),[error,setError]=useState('');
 const submit=async e=>{e.preventDefault();try{setBusy(true);setError('');setMessage('');await outletDemo.request({email,businessName:businessName||null});setMessage('Your 7-day demo account has been created. Login details have been sent to your email.');}catch(x){setError(x.message||'Unable to create demo account.')}finally{setBusy(false)}};
 return <div className="demoModalBackdrop" role="dialog" aria-modal="true" onMouseDown={e=>e.target===e.currentTarget&&!busy&&onClose()}><div className="demoModal"><div className="demoModalHead"><div><span className="landingKicker">FREE 7-DAY DEMO</span><h2>See HealthApp from the inside.</h2><p>We create a temporary outlet account and email the login details so you can explore the real workflow.</p></div><button type="button" className="iconBtn" onClick={onClose} disabled={busy}>×</button></div>{message?<div className="demoSuccess"><b>✓ Demo account ready</b><span>{message}</span><small>Use the email address you entered to sign in. Demo access automatically expires after 7 days.</small><button type="button" className="primary" onClick={onClose}>Done</button></div>:<form onSubmit={submit}><label className="field"><span>Business / outlet name</span><input value={businessName} onChange={e=>setBusinessName(e.target.value)} placeholder="Example: Fit Food Kitchen"/></label><label className="field"><span>Email address *</span><input type="email" value={email} onChange={e=>setEmail(e.target.value)} placeholder="you@yourbusiness.com" required/></label>{error&&<div className="error">{error}</div>}<div className="demoIncludes"><b>What you can explore</b><span>✓ Add customers & delivery addresses</span><span>✓ Create meal subscriptions and packages</span><span>✓ Add drivers and plan deliveries</span><span>✓ Preloaded legal policies and a realistic demo workspace</span><span>✓ Manage recipes, menus and kitchen orders</span></div><div className="demoModalActions"><button type="button" className="secondary" onClick={onClose} disabled={busy}>Cancel</button><button className="primary big" disabled={busy||!email}>{busy?'Creating demo…':'Send me the demo login →'}</button></div></form>}</div></div>;
}

function LandingPage({onLogin,onRegister,onDemo}){
 return <div className="landingPage">
  <header className="landingNav"><div className="landingNavBrand"><span className="brandMark">H</span><div><b>HealthApp</b><small>For healthy food outlets</small></div></div><nav><a href="#how">How It Works</a><a href="#features">Features</a><a href="#why">Why HealthApp</a><a href="#success">Success Stories</a><a href="#pricing">Pricing</a></nav><div className="landingNavActions"><button className="secondary" type="button" onClick={onLogin}>Login</button><button className="secondary demoNavButton" type="button" onClick={onDemo}>Request a Demo</button><button className="primary" type="button" onClick={onRegister}>Register Your Outlet →</button></div></header>
  <section className="landingHero">
   <div className="landingHeroCopy">
    <span className="landingKicker">🌱 BUILT FOR HEALTHY FOOD BUSINESSES</span>
    <h1>More orders.<br/>Less waste.<br/><em>A healthier business.</em></h1>
    <p>HealthApp helps outlets grow with subscription-based orders, smarter kitchen planning, organised deliveries and loyal health-conscious customers — all in one simple platform.</p>
    <div className="landingHeroActions"><button className="primary landingPrimaryCta" type="button" onClick={onRegister}>Register Your Outlet →</button><button className="secondary landingDemoCta" type="button" onClick={onDemo}>Request a 7-day Demo ▷</button></div>
    <div className="landingChecks"><span>✓ Subscription-based orders</span><span>✓ Reduce food waste</span><span>✓ Grow repeat customers</span></div>
   </div>
   <div className="landingHeroVisual"><div className="landingGlow"></div><MiniDashboard/><div className="landingFloat landingFloatOrders"><b>42</b><span>orders planned</span><i>↑ 18%</i></div><div className="landingFloat landingFloatWaste"><b>Better prep</b><span>Plan ingredients from pre-orders</span></div></div>
  </section>
  <section className="landingBenefitStrip">{benefitItems.map(([icon,title,text])=><div className="landingBenefit" key={title}><span>{icon}</span><b>{title}</b><small>{text}</small></div>)}</section>
  <section className="landingHow" id="how"><div className="landingSectionIntro"><span className="landingKicker">HOW IT WORKS</span><h2>From signup to smooth operations</h2><p>HealthApp keeps the whole outlet journey in one predictable flow.</p></div><div className="landingSteps"><div><strong>1</strong><b>Register & choose a plan</b><span>Pick the operating model that fits your outlet.</span></div><i>→</i><div><strong>2</strong><b>Complete onboarding</b><span>Add your business, owner details and documents.</span></div><i>→</i><div><strong>3</strong><b>Get verified</b><span>HealthApp reviews the application and activates your outlet.</span></div><i>→</i><div><strong>4</strong><b>Open your workspace</b><span>Manage customers, subscriptions, kitchen and deliveries.</span></div><i>→</i><div><strong>5</strong><b>Grow with confidence</b><span>Use recurring demand to plan better and scale.</span></div></div></section>
  <section className="landingFeatures" id="features"><div className="landingSectionIntro landingSectionIntroLeft"><span className="landingKicker">ONE PLATFORM FOR YOUR DAILY WORK</span><h2>Everything you need to run and grow your outlet</h2><p>Less switching between tools. More time improving your food and serving customers.</p></div><div className="landingFeatureGrid">{featureCards.map(f=><article className="landingFeatureCard" key={f.title}><div className="landingFeatureImage"><img src={f.image} alt="" loading="lazy"/><span>{f.tag}</span><i>{f.icon}</i></div><div className="landingFeatureBody"><div className="landingFeatureMetric">✦ {f.metric}</div><h3>{f.title}</h3><p>{f.text}</p><a href="#how">See how it helps →</a></div></article>)}</div></section>
  <section className="landingWhy" id="why"><div className="landingWhyVisual"><div className="landingPrepCard"><span>PRE-ORDERS</span><b>42 meals</b><small>planned before prep starts</small><div className="landingPrepBars"><i style={{width:'82%'}}></i><i style={{width:'63%'}}></i><i style={{width:'91%'}}></i><i style={{width:'47%'}}></i></div></div><div className="landingLeaf">🌿</div></div><div className="landingWhyCopy"><span className="landingKicker">WHY OUTLETS LOVE HEALTHAPP</span><h2>Run a healthier operation, not a more complicated one.</h2><p>Recurring subscriptions and pre-orders give you a clearer picture of demand. That means fewer last-minute surprises, better ingredient planning and less food prepared “just in case”.</p><div className="landingWhyList"><div><span>✓</span><div><b>Cook closer to demand</b><small>Use scheduled orders to estimate meals, portions and ingredients earlier.</small></div></div><div><span>✓</span><div><b>Keep the kitchen calm</b><small>Give the team a daily production view instead of chasing messages and spreadsheets.</small></div></div><div><span>✓</span><div><b>Deliver with a plan</b><small>Coordinate addresses, routes, drivers and slots from the same workspace.</small></div></div></div></div></section>
  <section className="landingSuccess" id="success"><div className="landingSectionIntro"><span className="landingKicker">A BUSINESS MODEL THAT BUILDS LOYALTY</span><h2>Healthy customers become repeat customers</h2><p>Subscriptions help outlets create a consistent relationship with customers while keeping service simple.</p></div><div className="landingQuote"><div className="landingQuoteMark">“</div><blockquote>HealthApp helps us see what needs to be prepared, delivered and followed up — before the day gets busy.</blockquote><div className="landingQuotePerson"><div className="avatar">R</div><div><b>Healthy meal outlet</b><span>Subscription-based food business</span></div><strong>+32%<small>growth opportunity</small></strong></div></div></section>
  <section className="landingPricing" id="pricing"><div><span className="landingKicker">FLEXIBLE OPERATING MODEL</span><h2>Start with what your outlet needs today.</h2><p>Choose a plan during onboarding, then scale as your customer base and operations grow.</p></div><div className="landingPricingCard"><span>One platform</span><b>Subscriptions + Kitchen + Delivery</b><small>One workspace for your outlet team</small><button className="primary" type="button" onClick={onRegister}>See plans & register →</button></div></section>
  <section className="landingCta"><div><span className="landingKicker">READY WHEN YOU ARE</span><h2>Ready to grow your healthy food business?</h2><p>Start receiving subscription orders, plan your kitchen with confidence and keep your delivery operation organised.</p></div><button className="landingCtaButton" type="button" onClick={onRegister}>Register Your Outlet →<small>Quick onboarding · Verified before activation</small></button></section>
  <footer className="landingFooter"><span>© HealthApp</span><span>Built for healthy food businesses · Subscriptions · Kitchen · Delivery · Customers</span></footer>
 </div>
}


function App(){
 const legalDoc=(()=>{try{return new URLSearchParams(window.location.search).get('legal')||''}catch{return ''}})();
 const[user,setUser]=useState(currentUser()),[demoOpen,setDemoOpen]=useState(false),[login,setLogin]=useState({email:'',password:''}),[active,setActive]=useState('dashboard'),[dash,setDash]=useState(null),[deliveryFilter,setDeliveryFilter]=useState('all'),[showLogin,setShowLogin]=useState(false),[verificationApp,setVerificationApp]=useState(null),[verificationLoading,setVerificationLoading]=useState(false);
 const[recipes,setRecipes]=useState([]),[ingredients,setIngredients]=useState([]),[allergens,setAllergens]=useState([]),[pricing,setPricing]=useState([]),[areas,setAreas]=useState([]),[selectedAreas,setSelectedAreas]=useState([]),[tiers,setTiers]=useState([]);
 const[customers,setCustomers]=useState([]),[subs,setSubs]=useState([]),[orders,setOrders]=useState([]),[deliveries,setDeliveries]=useState([]),[menu,setMenu]=useState([]),[billing,setBilling]=useState(null),[selectedSub,setSelectedSub]=useState(null),[kitchen,setKitchen]=useState(null),[kitchenDate,setKitchenDate]=useState(new Date().toISOString().slice(0,10)),[ingredientConsumption,setIngredientConsumption]=useState(null),[ingredientConsumptionDate,setIngredientConsumptionDate]=useState(todayISO());
 const[customerEditorOpen,setCustomerEditorOpen]=useState(false),[customerEditorSaving,setCustomerEditorSaving]=useState(false),[customerEditorForm,setCustomerEditorForm]=useState({firstName:'',lastName:'',email:'',password:'',weightKg:'',heightCm:'',dateOfBirth:'',goal:'WeightLoss',activityLevel:'Moderate',diet:'',allergyIds:[]});
 const[customerProfile,setCustomerProfile]=useState(null),[customerProfileLoading,setCustomerProfileLoading]=useState(false),[customerProfileOpen,setCustomerProfileOpen]=useState(false);
 const[drivers,setDrivers]=useState([]),[routePlan,setRoutePlan]=useState(null),[deliveryRouteDate,setDeliveryRouteDate]=useState(new Date().toISOString().slice(0,10)),[deliveryRouteMealSlot,setDeliveryRouteMealSlot]=useState(2),[selectedDriverIds,setSelectedDriverIds]=useState([]),[driverOpen,setDriverOpen]=useState(false),[driverForm,setDriverForm]=useState({firstName:'',lastName:'',email:'',password:''});
 const[staff,setStaff]=useState([]),[staffLoading,setStaffLoading]=useState(false),[staffOpen,setStaffOpen]=useState(false),[staffEditing,setStaffEditing]=useState(null),[staffSaving,setStaffSaving]=useState(false),[staffError,setStaffError]=useState(''),[staffForm,setStaffForm]=useState({firstName:'',lastName:'',email:'',password:'',role:'OutletManager'}),[staffIsActive,setStaffIsActive]=useState(true),[staffSearch,setStaffSearch]=useState(''),[staffRoleFilter,setStaffRoleFilter]=useState('All');
 const[driverRun,setDriverRun]=useState(null),[driverRunLoading,setDriverRunLoading]=useState(false),[driverRunDate,setDriverRunDate]=useState(todayISO()),[driverRunSlot,setDriverRunSlot]=useState(2);

 const[error,setError]=useState(''),[busy,setBusy]=useState(false),[toast,setToast]=useState(''),[toastType,setToastType]=useState('success'),[search,setSearch]=useState(''),[category,setCategory]=useState('All'),[city,setCity]=useState('Bengaluru');
 const[confirmDialog,setConfirmDialog]=useState(null);
 const[recipeOpen,setRecipeOpen]=useState(false),[editRecipe,setEditRecipe]=useState(null),[recipeForm,setRecipeForm]=useState(emptyRecipe),[uploading,setUploading]=useState(false);
 const[pricingOpen,setPricingOpen]=useState(false),[priceForm,setPriceForm]=useState({maxDistanceKm:'',fee:''});
 const[tierOpen,setTierOpen]=useState(false),[editTier,setEditTier]=useState(null),[tierForm,setTierForm]=useState({minMeals:1,maxMeals:9,oneWeekPercent:0,twoWeeksPercent:0,oneMonthPercent:0});
 const[taxSettings,setTaxSettings]=useState(null),[taxForm,setTaxForm]=useState({restaurantGstRate:5,restaurantGstMode:'Exclusive',isGstRegistered:false,isComposition:false,taxOperatingMode:'DirectOutletSupplier',gstin:'',pan:'',effectiveFromUtc:''});
 const[pkgCustomers,setPkgCustomers]=useState([]),[pkgRecipes,setPkgRecipes]=useState([]),[pkgMenu,setPkgMenu]=useState([]);
 const[manualPaymentPackageId,setManualPaymentPackageId]=useState(''),[manualPaymentMethod,setManualPaymentMethod]=useState('Cash'),[reviewPackageId,setReviewPackageId]=useState(''),[reviewPackageForm,setReviewPackageForm]=useState({finalMealAmount:'',discountReason:''});
 const[pkgCustomerId,setPkgCustomerId]=useState(''),[pkgAddresses,setPkgAddresses]=useState([]),[pkgDuration,setPkgDuration]=useState('OneWeek'),[pkgDeliveryMode,setPkgDeliveryMode]=useState('OneDeliveryPerDay'),[pkgStartDate,setPkgStartDate]=useState(todayISO()),[pkgSelections,setPkgSelections]=useState({}),[pkgDayAddresses,setPkgDayAddresses]=useState({}),[pkgMealAddresses,setPkgMealAddresses]=useState({}),[pkgPortions,setPkgPortions]=useState({}),[pkgDiscountType,setPkgDiscountType]=useState('None'),[pkgDiscountValue,setPkgDiscountValue]=useState(''),[pkgDiscountReason,setPkgDiscountReason]=useState(''),[pkgQuote,setPkgQuote]=useState(null),[pkgConfirmedAllergies,setPkgConfirmedAllergies]=useState([]),[pkgAddressOpen,setPkgAddressOpen]=useState(false),[pkgAddressTarget,setPkgAddressTarget]=useState(null),[pkgAddressForm,setPkgAddressForm]=useState({city:'',pincode:'',locality:'',label:'Home',addressLine1:'',addressLine2:'',contactName:'',contactPhone:'',latitude:'',longitude:'',cityAreaId:null,isDefault:false}),[pkgNewCustomerOpen,setPkgNewCustomerOpen]=useState(false),[pkgNewCustomerForm,setPkgNewCustomerForm]=useState({firstName:'',lastName:'',email:'',password:''});
 const notify=(m,type='success')=>{setToast(m);setToastType(type);setTimeout(()=>setToast(''),2500)},fail=e=>setError(e?.message||'Unexpected error.');
 const load=async p=>{setBusy(true);setError('');try{
  if(p==='dashboard'){setDash(await outletAdmin.dashboard());setRecipes(await outletAdmin.recipes());setPricing(await outletAdmin.pricingRules());}
  if(p==='kitchen')setKitchen(await outletAdmin.kitchen(kitchenDate));
  if(p==='ingredient-usage')setIngredientConsumption(await outletAdmin.ingredientConsumption(ingredientConsumptionDate));
  if(p==='recipes'){const x=await Promise.all([outletAdmin.recipes(),catalog.ingredients(),catalog.allergens()]);setRecipes(x[0]);setIngredients(x[1]);setAllergens(x[2]);}
  if(p==='customers'){const x=await Promise.all([outletAdmin.customers(),catalog.allergens()]);setCustomers(x[0]||[]);setAllergens(x[1]||[]);}
  if(p==='subscriptions'){const x=await Promise.all([outletAdmin.subscriptions(),outletAdmin.customers()]);setSubs(x[0]);setCustomers(x[1])}
  if(p==='orders')setOrders(await outletAdmin.orders());
  if(p==='deliveries')setDeliveries(await outletAdmin.deliveries());
  if(p==='routes'){const x=await Promise.all([outletAdmin.deliveryRoutes(deliveryRouteDate,deliveryRouteMealSlot),outletAdmin.drivers()]);setRoutePlan(x[0]);setDrivers(x[1]);setSelectedDriverIds(ids=>ids.length?ids:x[1].map(d=>d.id));}
  if(p==='team'){setStaffLoading(true);try{setStaff(await outletStaff.list())}finally{setStaffLoading(false)}}
  if(p==='driver')refreshDriverRun(driverRunDate,driverRunSlot);
  if(p==='menu'){const x=await Promise.all([outletAdmin.menu(),outletAdmin.recipes()]);setMenu(x[0]);setRecipes(x[1])}
  if(p==='delivery-areas'){const x=await Promise.all([outletAdmin.availableDeliveryAreas(city),outletAdmin.selectedDeliveryAreas()]);setAreas(x[0]);setSelectedAreas(x[1].map(a=>a.cityAreaId))}
  if(p==='pricing')setPricing(await outletAdmin.pricingRules());
  if(p==='discounts')setTiers(await outletAdmin.discountTiers());
  if(p==='tax'){const x=await outletAdmin.taxSettings();setTaxSettings(x);setTaxForm({restaurantGstRate:x?.restaurantGstRate??5,restaurantGstMode:x?.restaurantGstMode||'Exclusive',isGstRegistered:Boolean(x?.isGstRegistered),isComposition:Boolean(x?.isComposition),taxOperatingMode:x?.taxOperatingMode||'DirectOutletSupplier',gstin:x?.gstin||'',pan:x?.pan||'',effectiveFromUtc:x?.effectiveFromUtc?String(x.effectiveFromUtc).slice(0,10):''});}
  if(p==='packages'){const x=await Promise.all([outletAdmin.customers(),outletAdmin.recipes(),outletAdmin.menu(),outletAdmin.taxSettings()]);setPkgCustomers(x[0]||[]);setCustomers(x[0]||[]);setPkgRecipes(x[1]||[]);setPkgMenu(x[2]||[]);setTaxSettings(x[3]);if(x[3])setTaxForm({restaurantGstRate:x[3].restaurantGstRate,restaurantGstMode:x[3].restaurantGstMode});}
  if(p==='billing')setBilling(await outletAdmin.billing());
 }catch(e){fail(e)}finally{setBusy(false)}};
 useEffect(()=>{const onExpired=()=>{auth.logout();setUser(null);setVerificationApp(null);setShowLogin(true);setError('Your session has expired. Please sign in again.');setActive('dashboard')};window.addEventListener('healthapp-auth-expired',onExpired);return()=>window.removeEventListener('healthapp-auth-expired',onExpired)},[]);
 useEffect(()=>{if(!user)return;let cancelled=false;(async()=>{try{setVerificationLoading(true);const o=await outletAdmin.outlet();if(cancelled)return;const status=o?.status||'Pending';setVerificationApp(status==='Pending'?{status:'PendingVerification'}:null);if(status==='Active')setActive(a=>a==='dashboard'?'settings':a);if(status==='Live'||status==='Active')load(status==='Active'?'settings':active)}catch(e){if(!cancelled){if(e?.status===401){auth.logout();setUser(null);setVerificationApp(null);setShowLogin(true);setError('Your session has expired. Please sign in again.')}else{setVerificationApp({status:'PendingVerification'});setError(e?.message||'Unable to load outlet status.')}}}finally{if(!cancelled)setVerificationLoading(false)}})();return()=>{cancelled=true}},[user,active]);
 useEffect(()=>{if(user&&active==='delivery-areas')load('delivery-areas')},[city]);
 const nav=p=>{if(p!=='deliveries')setDeliveryFilter('all');setActive(p);setError('')};
 const openPendingDeliveries=()=>{setDeliveryFilter('pending');setActive('deliveries');setError('')};
 const emptyCustomerForm=()=>({firstName:'',lastName:'',email:'',password:'',weightKg:'',heightCm:'',dateOfBirth:'',goal:'WeightLoss',activityLevel:'Moderate',diet:'',allergyIds:[]});
 const openCreateCustomer=()=>{setCustomerEditorForm(emptyCustomerForm());setCustomerEditorOpen(true)};
 const openCustomerProfile=async customer=>{try{setCustomerProfileOpen(true);setCustomerProfileLoading(true);setCustomerProfile(await outletAdmin.customerProfile(customer.id));}catch(e){fail(e);setCustomerProfileOpen(false)}finally{setCustomerProfileLoading(false)}};
 const editCustomerProfile=()=>{const p=customerProfile?.profile;setCustomerEditorForm({firstName:customerProfile?.customer?.firstName||'',lastName:customerProfile?.customer?.lastName||'',email:customerProfile?.customer?.email||'',password:'',weightKg:p?.weightKg??'',heightCm:p?.heightCm??'',dateOfBirth:p?.dateOfBirth?.slice?.(0,10)||'',goal:p?.goal||'WeightLoss',activityLevel:p?.activityLevel||'Moderate',diet:p?.diet||'',allergyIds:(p?.allergies||[]).map(a=>a.id)});setCustomerEditorOpen(true)};
 const saveCustomer=async e=>{e.preventDefault();setCustomerEditorSaving(true);setError('');try{if(customerProfile){const updated=await outletAdmin.updateCustomerProfile(customerProfile.customer.id,{weightKg:customerEditorForm.weightKg===''?null:Number(customerEditorForm.weightKg),heightCm:customerEditorForm.heightCm===''?null:Number(customerEditorForm.heightCm),dateOfBirth:customerEditorForm.dateOfBirth||null,goal:customerEditorForm.goal,activityLevel:customerEditorForm.activityLevel,diet:customerEditorForm.diet||'',allergyIds:customerEditorForm.allergyIds||[]});setCustomerProfile(updated);setCustomerEditorOpen(false);notify('Customer profile updated')}else{const created=await outletAdmin.createCustomer({firstName:customerEditorForm.firstName,lastName:customerEditorForm.lastName,email:customerEditorForm.email,password:customerEditorForm.password,weightKg:customerEditorForm.weightKg===''?null:Number(customerEditorForm.weightKg),heightCm:customerEditorForm.heightCm===''?null:Number(customerEditorForm.heightCm),dateOfBirth:customerEditorForm.dateOfBirth||null,goal:customerEditorForm.goal,activityLevel:customerEditorForm.activityLevel,diet:customerEditorForm.diet||'',allergyIds:customerEditorForm.allergyIds||[]});setCustomers(v=>[created,...v.filter(x=>x.id!==created.id)]);setPkgCustomers(v=>[created,...v.filter(x=>x.id!==created.id)]);setCustomerEditorOpen(false);notify('Customer created with health profile');await openCustomerProfile(created)}}catch(e){fail(e)}finally{setCustomerEditorSaving(false)}};

 const openSubscription=async id=>{try{setBusy(true);setSelectedSub(await outletAdmin.subscriptionDetail(id))}catch(e){fail(e)}finally{setBusy(false)}};
 const refreshKitchen=async date=>{try{setBusy(true);setKitchen(await outletAdmin.kitchen(date))}catch(e){fail(e)}finally{setBusy(false)}};
 const refreshIngredientConsumption=async date=>{try{setBusy(true);setIngredientConsumption(await outletAdmin.ingredientConsumption(date))}catch(e){fail(e)}finally{setBusy(false)}};
 const refreshRoutes=async(date,mealSlot=deliveryRouteMealSlot)=>{try{setBusy(true);const x=await Promise.all([outletAdmin.deliveryRoutes(date,mealSlot),outletAdmin.drivers()]);setRoutePlan(x[0]);setDrivers(x[1]);setSelectedDriverIds(ids=>ids.filter(id=>x[1].some(d=>d.id===id)));}catch(e){fail(e)}finally{setBusy(false)}};
 const refreshDriverRun=async(date,mealSlot=driverRunSlot)=>{try{setDriverRunLoading(true);setDriverRun(await outletAdmin.myDriverRoute(date,mealSlot))}catch(e){fail(e)}finally{setDriverRunLoading(false)}};
 const startDriverRoute=async routeId=>{try{setDriverRunLoading(true);setDriverRun(await outletAdmin.startDriverRoute(routeId))}catch(e){fail(e)}finally{setDriverRunLoading(false)}};
 const completeDriverStop=async(stopId)=>{try{setDriverRunLoading(true);setDriverRun(await outletAdmin.completeDriverStop(stopId))}catch(e){fail(e)}finally{setDriverRunLoading(false)}};

 const planRoutes=async()=>{try{if(!selectedDriverIds.length)return fail({message:'Select at least one active in-house driver.'});setBusy(true);setRoutePlan(await outletAdmin.planDeliveryRoutes({date:deliveryRouteDate,mealSlot:deliveryRouteMealSlot,driverIds:selectedDriverIds}));notify('Delivery routes planned')}catch(e){fail(e)}finally{setBusy(false)}};
 const dispatchRoute=async route=>{setConfirmDialog({variant:'warning',title:'Dispatch this route?',message:'Route for '+route.driverName+' has '+(route.stops?.length||0)+' stops. Dispatching will mark it ready for driver execution.',confirmLabel:'Dispatch route',onConfirm:async()=>{try{setBusy(true);setRoutePlan(await outletAdmin.dispatchRoute(route.id));notify('Route dispatched to driver')}catch(e){fail(e)}finally{setBusy(false)}}});};
 const manualPlanRoutes=async(assignments,errorMessage)=>{if(errorMessage)return fail({message:errorMessage});try{setBusy(true);setRoutePlan(await outletAdmin.planDeliveryRoutes({date:deliveryRouteDate,mealSlot:deliveryRouteMealSlot,driverIds:selectedDriverIds,manualAssignments:assignments}));notify('Manual routes saved and road order calculated')}catch(e){fail(e)}finally{setBusy(false)}}; const saveDriver=async e=>{e.preventDefault();try{const d=await outletAdmin.createDriver(driverForm);setDriverOpen(false);setDriverForm({firstName:'',lastName:'',email:'',password:''});setDrivers(x=>[...x,d]);setSelectedDriverIds(x=>[...x,d.id]);try{setStaff(await outletStaff.list())}catch{}notify('Driver added')}catch(e){fail(e)}};
 const openCreateStaff=()=>{setStaffEditing(null);setStaffError('');setStaffForm({firstName:'',lastName:'',email:'',password:'',role:'OutletManager'});setStaffIsActive(true);setStaffOpen(true)};
 const openEditStaff=member=>{setStaffEditing(member);setStaffError('');setStaffForm({firstName:(member.name||'').split(' ')[0]||'',lastName:(member.name||'').split(' ').slice(1).join(' '),email:member.email||'',password:'',role:member.role||'OutletManager'});setStaffIsActive(member.isActive!==false);setStaffOpen(true)};
 const saveStaff=async e=>{e.preventDefault();try{setStaffSaving(true);setStaffError('');const payload={...staffForm,isActive:staffIsActive,password:staffEditing?(staffForm.password||null):staffForm.password};const x=staffEditing?await outletStaff.update(staffEditing.id,payload):await outletStaff.create(staffForm);setStaff(v=>staffEditing?v.map(s=>s.id===x.id?x:s):[...v,x]);setStaffOpen(false);setStaffEditing(null);notify(staffEditing?'Staff account updated':'Staff account created')}catch(e){setStaffError(e?.message||'Unable to save staff account.')}finally{setStaffSaving(false)}};


 const signIn=async e=>{e.preventDefault();try{const x=await auth.login(login);setUser(x.user);setActive(x.user?.role==='KitchenStaff'?'kitchen':x.user?.role==='Driver'?'driver':'dashboard');setShowLogin(false);setError('')}catch(e){fail(e)}};
 const filtered=useMemo(()=>recipes.filter(r=>(category==='All'||r.category===category)&&(!search||r.name.toLowerCase().includes(search.toLowerCase()))),[recipes,category,search]);

 const openNew=()=>{setEditRecipe(null);setRecipeForm({...emptyRecipe,ingredients:[],allergenIds:[]});setRecipeOpen(true)};
 const openEdit=r=>{setEditRecipe(r);setRecipeForm({...emptyRecipe,...r,ingredients:(r.ingredients||[]).map(x=>({ingredientId:x.ingredientId,quantity:x.quantity,largeQuantity:x.largeQuantity||x.quantity,unit:x.unit})),allergenIds:(r.allergens||[]).map(x=>x.id)});setRecipeOpen(true)};
 const upload=async file=>{if(!file)return;if(!file.type.startsWith('image/'))return fail({message:'Only image files are allowed.'});if(file.size>10000000)return fail({message:'Image must be 10 MB or smaller. It will be resized and compressed automatically.'});try{setUploading(true);const r=await outletAdmin.uploadRecipeImage(file);setRecipeForm(f=>({...f,imageUrl:r.url}));notify('Image uploaded')}catch(e){fail(e)}finally{setUploading(false)}};
 const saveRecipe=async e=>{e.preventDefault();try{const normalizedIngredients=(recipeForm.ingredients||[]).filter(x=>x.ingredientId&&Number(x.quantity)>0).map(x=>({ingredientId:x.ingredientId,quantity:Number(x.quantity),largeQuantity:Number(x.largeQuantity)>0?Number(x.largeQuantity):Number(x.quantity),unit:x.unit||ingredients.find(i=>i.id===x.ingredientId)?.defaultUnit||'g'}));const nutrition=calculateRecipeNutrition({...recipeForm,ingredients:normalizedIngredients},ingredients);if(!normalizedIngredients.length)throw new Error('Add at least one ingredient so nutrition can be calculated.');if(nutrition.missing.length)throw new Error(`Nutrition cannot be calculated for: ${nutrition.missing.join(', ')}.`);const p={...recipeForm,calories:Math.round(nutrition.regular.calories),proteinGrams:Math.round(nutrition.regular.proteinGrams),carbsGrams:Math.round(nutrition.regular.carbsGrams),fatGrams:Math.round(nutrition.regular.fatGrams),fiberGrams:Math.round(nutrition.regular.fiberGrams),sugarGrams:Math.round(nutrition.regular.sugarGrams),largeCalories:Math.round(nutrition.large.calories),largeProteinGrams:Math.round(nutrition.large.proteinGrams),largeCarbsGrams:Math.round(nutrition.large.carbsGrams),largeFatGrams:Math.round(nutrition.large.fatGrams),largeFiberGrams:Math.round(nutrition.large.fiberGrams),largeSugarGrams:Math.round(nutrition.large.sugarGrams),pricePerMeal:Number(recipeForm.pricePerMeal)||0,largePricePerMeal:Number(recipeForm.largePricePerMeal)||0,ingredients:normalizedIngredients,allergenIds:recipeForm.allergenIds||[]};if(editRecipe)await outletAdmin.updateRecipe(editRecipe.id,p);else await outletAdmin.createRecipe(p);setRecipeOpen(false);await load('recipes');notify(editRecipe?'Recipe updated':'Recipe created')}catch(e){fail(e)}};
 const executeRemoveRecipe=async r=>{try{await outletAdmin.deleteRecipe(r.id);await load('recipes');notify('Recipe deleted')}catch(e){fail(e)}};
 const removeRecipe=r=>setConfirmDialog({variant:'danger',title:'Delete recipe?',message:`${r.name} will be removed from the outlet menu. This action cannot be undone.`,confirmLabel:'Delete recipe',onConfirm:()=>executeRemoveRecipe(r)});
 const addPrice=async e=>{e.preventDefault();const km=Number(priceForm.maxDistanceKm),fee=Number(priceForm.fee);if(km<=0)return fail({message:'Distance must be greater than 0.'});if(fee<0)return fail({message:'Fee cannot be negative.'});if(pricing.some(x=>Number(x.maxDistanceKm)===km))return fail({message:'This distance slab already exists.'});try{await outletAdmin.addPricingRule({maxDistanceKm:km,fee});setPricingOpen(false);setPriceForm({maxDistanceKm:'',fee:''});await load('pricing');notify('Delivery slab added')}catch(e){fail(e)}};
 const executeRemovePrice=async r=>{try{await outletAdmin.deletePricingRule(r.id);await load('pricing');notify('Delivery slab removed')}catch(e){fail(e)}};
 const removePrice=r=>setConfirmDialog({variant:'danger',title:'Delete delivery slab?',message:`The ≤ ${r.maxDistanceKm} km delivery pricing slab will be removed.`,confirmLabel:'Delete slab',onConfirm:()=>executeRemovePrice(r)});
 const saveAreas=async()=>{try{await outletAdmin.saveDeliveryAreas(selectedAreas);await load('delivery-areas');notify('Delivery areas saved')}catch(e){fail(e)}};
 const saveTier=async e=>{e.preventDefault();const p={minMeals:Number(tierForm.minMeals),maxMeals:tierForm.maxMeals===''?null:Number(tierForm.maxMeals),oneWeekPercent:Number(tierForm.oneWeekPercent),twoWeeksPercent:Number(tierForm.twoWeeksPercent),oneMonthPercent:Number(tierForm.oneMonthPercent),isActive:true};try{if(editTier)await outletAdmin.updateDiscountTier(editTier.id,p);else await outletAdmin.addDiscountTier(p);setTierOpen(false);await load('discounts');notify('Discount tier saved')}catch(e){fail(e)}};
 const executeRemoveTier=async r=>{try{await outletAdmin.deleteDiscountTier(r.id);await load('discounts');notify('Discount tier removed')}catch(e){fail(e)}};
 const removeTier=r=>setConfirmDialog({variant:'danger',title:'Delete discount tier?',message:'This negotiated subscription discount tier will be removed.',confirmLabel:'Delete tier',onConfirm:()=>executeRemoveTier(r)});
 const loadPackageAddresses=async customerId=>{setPkgCustomerId(customerId);setPkgQuote(null);setPkgConfirmedAllergies([]);setPkgSelections({});setPkgDayAddresses({});setPkgMealAddresses({});setPkgPortions({});if(!customerId){setPkgAddresses([]);return}try{setPkgAddresses(await outletAdmin.customerAddresses(customerId)||[])}catch(e){fail(e)}};
 const createPackageCustomer=async e=>{e.preventDefault();try{const x=await outletAdmin.createCustomer(pkgNewCustomerForm);setPkgCustomers(v=>[x,...v]);setCustomers(v=>[x,...v]);setPkgNewCustomerOpen(false);setPkgNewCustomerForm({firstName:'',lastName:'',email:'',password:''});await loadPackageAddresses(x.id);setPkgAddressForm(f=>({...f,contactName:(x.firstName+' '+x.lastName).trim(),city:dash?.outlet?.city||''}));setPkgAddressOpen(true);notify('Customer created. Add their delivery address.')}catch(e){fail(e)}};
 const savePackageAddress=async e=>{e.preventDefault();try{const payload={...pkgAddressForm,pincode:pkgAddressForm.pincode||'',locality:pkgAddressForm.locality||'',addressLine1:pkgAddressForm.addressLine1,addressLine2:pkgAddressForm.addressLine2||'',contactName:pkgAddressForm.contactName||'',contactPhone:pkgAddressForm.contactPhone||'',latitude:Number(pkgAddressForm.latitude),longitude:Number(pkgAddressForm.longitude),cityAreaId:pkgAddressForm.cityAreaId||null,isDefault:Boolean(pkgAddressForm.isDefault)};if(!Number.isFinite(payload.latitude)||!Number.isFinite(payload.longitude))throw new Error('Pick the exact delivery point on the map.');const x=await outletAdmin.createCustomerAddress(pkgCustomerId,payload);setPkgAddresses(v=>[x,...v.filter(a=>a.id!==x.id)]);if(pkgAddressTarget?.type==='meal')setPkgMealAddresses(v=>({...v,[packageKey(pkgAddressTarget.date,pkgAddressTarget.slot)]:x.id}));else if(pkgAddressTarget?.type==='day')setPkgDayAddresses(v=>({...v,[pkgAddressTarget.date]:x.id}));else if(pkgDeliveryMode==='OneDeliveryPerDay')setPkgDayAddresses(v=>Object.fromEntries(packageDates.map(d=>[d,x.id])));setPkgAddressTarget(null);setPkgAddressOpen(false);notify('Delivery address added')}catch(e){fail(e)}};
 const pickPackageAddress=async(lat,lng)=>{setPkgAddressForm(f=>({...f,latitude:lat,longitude:lng}));try{const g=await locations.reverseGeocode(lat,lng);setPkgAddressForm(f=>({...f,city:g?.city||f.city,pincode:g?.pincode||f.pincode,locality:g?.suburb||g?.neighbourhood||f.locality,addressLine1:[g?.houseNumber,g?.road].filter(Boolean).join(' ')||f.addressLine1,addressLine2:g?.suburb||g?.neighbourhood||f.addressLine2,latitude:lat,longitude:lng}));}catch{}};
 const packageDates=useMemo(()=>{const n=packageDurationDays[pkgDuration]||7;return Array.from({length:n},(_,i)=>addDays(pkgStartDate,i))},[pkgDuration,pkgStartDate]);
 const packageMenuFor=(date,slot)=>pkgMenu.filter(x=>Number(x.dayOfWeek)===dayId(date)&&Number(x.mealSlotValue)===Number(slot));
 const packageSelectionsPayload=useMemo(()=>Object.entries(pkgSelections).flatMap(([k,value])=>{
   const [date,slot]=k.split('_');
   const items=Array.isArray(value)?value:(value?.recipeId?[value]:value?[{recipeId:value}]:[]);
   const addressId=pkgDeliveryMode==='OneDeliveryPerDay'?(pkgDayAddresses[date]||null):(pkgMealAddresses[k]||null);
   return items.filter(x=>x?.recipeId).map(x=>({mealDate:date,mealSlot:Number(slot),recipeId:x.recipeId,portionSize:Number(pkgPortions[packageItemKey(date,Number(slot),x.recipeId)]||1),addressId}));
 }),[pkgSelections,pkgDayAddresses,pkgMealAddresses,pkgDeliveryMode,pkgPortions]);
 const selectedPackageCount=packageSelectionsPayload.length;
 const packageQuote=async()=>{try{if(!pkgCustomerId)throw new Error('Select a customer.');if(!selectedPackageCount)throw new Error('Select at least one item.');if(!packageSelectionsPayload.every(x=>x.addressId))throw new Error('Select an address for every scheduled item.');const q=await outletAdmin.packageQuote({customerId:pkgCustomerId,outletId:dash?.outlet?.id,deliveryMode:pkgDeliveryMode,duration:pkgDuration,selections:packageSelectionsPayload,discountType:pkgDiscountType,discountValue:Number(pkgDiscountValue)||0,discountReason:pkgDiscountReason||'',confirmedAllergyRecipeIds:pkgConfirmedAllergies,deliveryCity:dash?.outlet?.city||''});setPkgQuote(q);if(q.allergyWarnings?.length&&!q.requiresAllergyConfirmation)setPkgConfirmedAllergies(q.allergyWarnings.map(x=>x.recipeId));notify(q.requiresAllergyConfirmation?'Review the allergy warnings before sending.':'Quote calculated.');return q}catch(e){fail(e);return null}};
 const createPackage=async()=>{try{const q=pkgQuote||await packageQuote();if(!q)return;const warnings=q.allergyWarnings||[];if(q.requiresAllergyConfirmation&&!warnings.every(w=>pkgConfirmedAllergies.includes(w.recipeId)))throw new Error('Review and confirm every allergy warning before sending this package.');const s=await outletAdmin.createPackage({customerId:pkgCustomerId,deliveryMode:pkgDeliveryMode,duration:pkgDuration,selections:packageSelectionsPayload,discountType:pkgDiscountType,discountValue:Number(pkgDiscountValue)||0,discountReason:pkgDiscountReason||'',confirmedAllergyRecipeIds:pkgConfirmedAllergies,deliveryCity:dash?.outlet?.city||''});setSubs(v=>[s,...v.filter(x=>x.id!==s.id)]);setActive('subscriptions');notify('Package sent to customer for review and acceptance.')}catch(e){fail(e)}};
 const openMarkPaid=id=>{setManualPaymentPackageId(id);setManualPaymentMethod('Cash');};
 const markPackagePaid=async()=>{try{if(!manualPaymentPackageId)return;const s=await outletAdmin.markPackagePaid(manualPaymentPackageId,{paymentMethod:manualPaymentMethod});setSubs(v=>v.map(x=>x.id===manualPaymentPackageId?s:x));setManualPaymentPackageId('');await load('subscriptions');notify('Payment recorded and package activated.')}catch(e){fail(e)}};
 const openReviewPackage=id=>{const item=subs.find(x=>x.id===id);setReviewPackageId(id);setReviewPackageForm({finalMealAmount:item?.grossMealAmount!=null?String(item.grossMealAmount):'',discountReason:''});};
 const confirmReviewPackage=async e=>{e.preventDefault();try{if(!reviewPackageId)return;const amount=reviewPackageForm.finalMealAmount===''?null:Number(reviewPackageForm.finalMealAmount);if(amount!==null&&(!Number.isFinite(amount)||amount<0))throw new Error('Enter a valid final meal amount.');const s=await outletAdmin.confirmCustomerPackage(reviewPackageId,{finalMealAmount:amount,discountType:'None',discountValue:0,discountReason:reviewPackageForm.discountReason||'Outlet reviewed package'});setSubs(v=>v.map(x=>x.id===reviewPackageId?s:x));setReviewPackageId('');await load('subscriptions');notify('Package confirmed. Customer payment is now available.')}catch(e){fail(e)}};
 const saveTax=async e=>{e.preventDefault();const rate=Number(taxForm.restaurantGstRate);if(!Number.isFinite(rate)||rate<0||rate>100)return fail({message:'Restaurant GST rate must be between 0% and 100%.'});try{const x=await outletAdmin.updateTaxSettings({restaurantGstRate:rate,restaurantGstMode:taxForm.restaurantGstMode,isGstRegistered:Boolean(taxForm.isGstRegistered),isComposition:Boolean(taxForm.isComposition),taxOperatingMode:taxForm.taxOperatingMode,gstin:taxForm.gstin||'',pan:taxForm.pan||'',effectiveFromUtc:taxForm.effectiveFromUtc||null});setTaxSettings(x);setTaxForm({restaurantGstRate:x?.restaurantGstRate??rate,restaurantGstMode:x?.restaurantGstMode||taxForm.restaurantGstMode,isGstRegistered:Boolean(x?.isGstRegistered),isComposition:Boolean(x?.isComposition),taxOperatingMode:x?.taxOperatingMode||taxForm.taxOperatingMode,gstin:x?.gstin||'',pan:x?.pan||'',effectiveFromUtc:x?.effectiveFromUtc?String(x.effectiveFromUtc).slice(0,10):''});notify('Tax and GST settings saved as a new version')}catch(e){fail(e)}};

 if(user&&!verificationLoading&&verificationApp&&verificationApp.status!=='Approved')return <OutletVerificationCenter user={user} onLogout={()=>{auth.logout();setUser(null);setVerificationApp(null)}}/>;
 if(!user&&showLogin)return <div className="loginPage"><div className="loginCard"><div className="brand"><span className="brandMark">H</span><div><b>HealthApp</b><small>Outlet management</small></div></div><button type="button" className="linkBtn landingBackHome" onClick={()=>{setError('');setShowLogin(false)}}>← Back to home</button><h1>Welcome back</h1><p>Run your meal business from one workspace.</p><form onSubmit={signIn}><Field label="Email"><input value={login.email} onChange={e=>setLogin({...login,email:e.target.value})}/></Field><Field label="Password"><input type="password" value={login.password} onChange={e=>setLogin({...login,password:e.target.value})}/></Field><button className="primary full">Sign in</button>{error&&<div className="error">{error}</div>}<small className="demo">Demo account? Use the email and password from your 7-day demo email.</small><div className="registerPrompt"><span>New to HealthApp?</span><button type="button" className="linkBtn" onClick={()=>{window.location.href=BROCCOLY_URL+'/register'}}>Register your outlet →</button></div></form></div></div>;
 if(legalDoc&&['saas-terms','dpa','acceptable-use'].includes(legalDoc))return <OutletLegalDocuments documentId={legalDoc} outletName={dash?.outlet?.name||'Your outlet'} onBack={()=>{window.location.href=window.location.pathname}}/>;
  if(!user)return <><LandingPage onLogin={()=>{setError('');setShowLogin(true)}} onRegister={()=>{window.location.href=BROCCOLY_URL+'/register'}} onDemo={()=>setDemoOpen(true)}/>{demoOpen&&<DemoRequestModal onClose={()=>setDemoOpen(false)}/>}</>;

 const navs=[['dashboard','⌂','Dashboard'],['kitchen','▦','Kitchen'],['recipes','◈','Recipes'],['menu','☷','Weekly Menu'],['ingredient-usage','◉','Ingredient Usage'],['customers','♙','Customers'],['packages','✚','Create Package'],['subscriptions','◫','Subscriptions'],['orders','▤','Orders'],['deliveries','⌁','Deliveries'],['routes','⇢','Delivery Routes'],['team','♟','Team'],['settings','⚙','Settings'],['driver','⌖','My Deliveries']];
 const outletRole=String(user?.role||'OutletAdmin');
 const allowedNavs=OUTLET_ROLE_NAVS[outletRole]||OUTLET_ROLE_NAVS.OutletAdmin;
 const defaultPage=allowedNavs[0]||'dashboard';

 const effectiveActive=allowedNavs.includes(active)?active:defaultPage;
 const title=navs.find(n=>n[0]===effectiveActive)?.[2]||'Dashboard';
 return <div className="appShell"><aside className="sidebar"><div className="sideBrand"><span className="brandMark">{(dash?.outlet?.name||"H").slice(0,1).toUpperCase()}</span><div><b>{dash?.outlet?.name||"HealthApp"}</b><small>Outlet portal</small></div></div><div className="outletMini"><div className="avatar">{(dash?.outlet?.name||'F')[0]}</div><div><b>{dash?.outlet?.name||'FitFood Bengaluru'}</b><span>{OUTLET_ROLE_LABELS[outletRole]||'Outlet staff'}</span></div></div><div className="sidebarNavGroups">{OUTLET_NAV_GROUPS.map(group=>{const items=group.items.filter(id=>allowedNavs.includes(id)&&(!group.roles||group.roles.includes(outletRole)));if(!items.length)return null;return <div className="navGroup" key={group.key}><div className="navLabel">{group.label}</div>{items.map(id=>{const n=navs.find(x=>x[0]===id);return n?<button className={effectiveActive===n[0]?'navItem active':'navItem'} key={n[0]} onClick={()=>nav(n[0])}><span>{n[1]}</span>{n[2]}</button>:null})}</div>})}</div><div className="sideBottom"><div className="secure">● API connected</div><button className="logoutBtn" onClick={()=>{auth.logout();setUser(null)}}>Log out</button></div></aside>
 <section className="main"><header className="topbar"><div><h2>{title}</h2><span>{dash?.outlet?.city||'Bengaluru'}, {dash?.outlet?.state||'Karnataka'}</span></div><div className="topbarRight">{user?.isDemo&&<span className="demoModePill">● DEMO MODE</span>}<div className="topUser"><div className="avatar sm">{(user.firstName||'A')[0]}</div><div><b>{user.firstName} {user.lastName}</b><span>{user.email}</span></div></div></div></header>
 <main className="content">{error&&<div className="statusBanner error"><span><b>⚠ Something needs attention</b>{error}</span><button onClick={()=>setError('')}>×</button></div>}
 {effectiveActive==='dashboard'&&<Dashboard dash={dash} recipes={recipes} pricing={pricing} nav={nav} openSubscription={openSubscription} onOpenPending={openPendingDeliveries}/>}
 {effectiveActive==='recipes'&&<Recipes items={filtered} total={recipes.length} search={search} setSearch={setSearch} category={category} setCategory={setCategory} openNew={openNew} openEdit={openEdit} remove={removeRecipe}/>}
 {effectiveActive==='menu'&&<MenuPage recipes={recipes} menu={menu} setMenu={setMenu} onSave={async()=>{try{await outletAdmin.saveMenu(menu.map(x=>({recipeId:x.recipeId,dayOfWeek:Number(x.dayOfWeek),mealSlot:Number(x.mealSlotValue||({'Morning':1,'Afternoon':2,'Evening':3,'Night':4}[x.mealSlot]||0)),isAvailable:x.isAvailable,displayOrder:x.displayOrder||0,optionGroup:x.optionGroup||'Main',isRequired:x.isRequired!==false,maxSelections:Math.max(1,Number(x.maxSelections)||1)})));notify('Weekly menu saved')}catch(e){fail(e)}}}/>}
 {effectiveActive==='customers'&&<CustomersPage customers={customers} onCreate={openCreateCustomer} onOpen={openCustomerProfile}/>}
 {effectiveActive==='driver'&&<DriverRunPage data={driverRun} loading={driverRunLoading} date={driverRunDate} setDate={d=>{setDriverRunDate(d);refreshDriverRun(d,driverRunSlot)}} mealSlot={driverRunSlot} setMealSlot={s=>{setDriverRunSlot(s);refreshDriverRun(driverRunDate,s)}} onStart={startDriverRoute} onCompleteStop={completeDriverStop}/>} {effectiveActive==='team'&&<TeamPage staff={staff} loading={staffLoading} onRefresh={()=>load('team')} search={staffSearch} setSearch={setStaffSearch} roleFilter={staffRoleFilter} setRoleFilter={setStaffRoleFilter} onCreate={openCreateStaff} onEdit={openEditStaff}/>} {effectiveActive==='packages'&&<OutletPackageBuilder customers={pkgCustomers} recipes={pkgRecipes} menu={pkgMenu} customerId={pkgCustomerId} addresses={pkgAddresses} duration={pkgDuration} setDuration={setPkgDuration} deliveryMode={pkgDeliveryMode} setDeliveryMode={setPkgDeliveryMode} startDate={pkgStartDate} setStartDate={setPkgStartDate} dates={packageDates} selections={pkgSelections} setSelections={setPkgSelections} dayAddresses={pkgDayAddresses} setDayAddresses={setPkgDayAddresses} mealAddresses={pkgMealAddresses} setMealAddresses={setPkgMealAddresses} portions={pkgPortions} setPortions={setPkgPortions} discountType={pkgDiscountType} setDiscountType={setPkgDiscountType} discountValue={pkgDiscountValue} setDiscountValue={setPkgDiscountValue} discountReason={pkgDiscountReason} setDiscountReason={setPkgDiscountReason} quote={pkgQuote} selectedCount={selectedPackageCount} onCustomerChange={loadPackageAddresses} onNewCustomer={()=>setPkgNewCustomerOpen(true)} quotePackage={packageQuote} createPackage={createPackage} confirmedAllergies={pkgConfirmedAllergies} setConfirmedAllergies={setPkgConfirmedAllergies} onAddAddress={(target=null)=>{const cst=pkgCustomers.find(x=>x.id===pkgCustomerId);setPkgAddressTarget(target);setPkgAddressForm({city:dash?.outlet?.city||'',pincode:'',locality:'',label:'Home',addressLine1:'',addressLine2:'',contactName:[cst?.firstName,cst?.lastName].filter(Boolean).join(' '),contactPhone:'',latitude:'',longitude:'',cityAreaId:null,isDefault:false});setPkgAddressOpen(true)}}/>} {effectiveActive==='subscriptions'&&<SubscriptionsPage items={subs} customers={customers} onOpen={openSubscription} onMarkPaid={openMarkPaid} onConfirm={openReviewPackage} onCreate={()=>nav('packages')}/>} 
 {effectiveActive==='kitchen'&&<KitchenPage data={kitchen} date={kitchenDate} setDate={setKitchenDate} refresh={refreshKitchen}/>}
 {effectiveActive==='ingredient-usage'&&<IngredientConsumptionPage data={ingredientConsumption} date={ingredientConsumptionDate} setDate={d=>{setIngredientConsumptionDate(d);refreshIngredientConsumption(d)}} refresh={()=>refreshIngredientConsumption(ingredientConsumptionDate)}/>}
 {effectiveActive==='orders'&&<Page title="Orders" text="Orders generated from customer subscriptions." content={<Table columns={['Order','Customer','Status','Delivery date','Total']} rows={orders.map(x=>[String(x.id).slice(0,8)+'…',String(x.customerId).slice(0,8)+'…',x.status,new Date(x.deliveryDate).toLocaleDateString(),money(x.total)])} empty="No orders yet."/>}/>}
 {effectiveActive==='deliveries'&&<Page title={deliveryFilter==='pending'?'Pending deliveries':'Deliveries'} text={deliveryFilter==='pending'?'Deliveries that still need action today.':'Scheduled delivery jobs for this outlet.'} content={<div className="deliveryPageContent"><div className="deliveryFilterBar"><div><b>{deliveryFilter==='pending'?'Pending deliveries':'All deliveries'}</b><small>{deliveryFilter==='pending'?deliveries.filter(x=>['Scheduled','Preparing','OutForDelivery'].includes(x.status)).length+' deliveries need action':deliveries.length+' scheduled delivery jobs'}</small></div>{deliveryFilter==='pending'&&<button className="secondary smallBtn" onClick={()=>setDeliveryFilter('all')}>Show all deliveries</button>}</div><Table columns={['Customer','Address','Date','Slot','Fee','Status']} rows={deliveries.filter(x=>deliveryFilter!=='pending'||['Scheduled','Preparing','OutForDelivery'].includes(x.status)).map(x=>[x.customerName,x.address,new Date(x.scheduledDate).toLocaleDateString(),x.mealSlot,money(x.deliveryFee),x.status])} empty={deliveryFilter==='pending'?'No pending deliveries today.':'No deliveries yet.'}/></div>}/>}
 {effectiveActive==='routes'&&<DeliveryRoutesPage plan={routePlan} drivers={drivers} date={deliveryRouteDate} mealSlot={deliveryRouteMealSlot} setDate={d=>{setDeliveryRouteDate(d);refreshRoutes(d,deliveryRouteMealSlot)}} setMealSlot={s=>{setDeliveryRouteMealSlot(s);refreshRoutes(deliveryRouteDate,s)}} selectedDriverIds={selectedDriverIds} setSelectedDriverIds={setSelectedDriverIds} planRoutes={planRoutes} dispatchRoute={dispatchRoute} manualPlanRoutes={manualPlanRoutes} addDriver={()=>setDriverOpen(true)}/>}
 {active==='delivery-areas'&&<Areas city={city} setCity={setCity} areas={areas} selected={selectedAreas} setSelected={setSelectedAreas} save={saveAreas}/>}
 {active==='pricing'&&<Pricing pricing={pricing} add={()=>setPricingOpen(true)} remove={removePrice}/>}
 {active==='discounts'&&<Discounts tiers={tiers} add={()=>{setEditTier(null);setTierForm({minMeals:1,maxMeals:9,oneWeekPercent:0,twoWeeksPercent:0,oneMonthPercent:0});setTierOpen(true)}} edit={t=>{setEditTier(t);setTierForm({...t,maxMeals:t.maxMeals??''});setTierOpen(true)}} remove={removeTier}/>}
 {active==='tax'&&<TaxSettingsPage settings={taxSettings} form={taxForm} setForm={setTaxForm} save={saveTax}/>}
 {effectiveActive==='billing'&&<Billing billing={billing}/>} 
 {effectiveActive==='settings'&&<OutletSettings user={user} onNavigate={nav}/>} 
 {busy&&<div className="loadingBar"><span/></div>}</main></section>
 {confirmDialog&&<StandardConfirmModal request={confirmDialog} onClose={()=>setConfirmDialog(null)} onConfirm={async()=>{const fn=confirmDialog.onConfirm;setConfirmDialog(null);await fn()}}/>} {toast&&<div className={'statusToast '+toastType}><span>{toastType==='success'?'✓':toastType==='warning'?'⚠':toastType==='info'?'ℹ':'×'}</span><div><b>{toastType==='success'?'Success':toastType==='warning'?'Warning':toastType==='info'?'Info':'Error'}</b><small>{toast}</small></div><button onClick={()=>setToast('')}>×</button></div>}
 {customerEditorOpen&&<CustomerEditorModal form={customerEditorForm} setForm={setCustomerEditorForm} allergens={allergens} saving={customerEditorSaving} editing={Boolean(customerProfile)} onClose={()=>setCustomerEditorOpen(false)} onSave={saveCustomer}/>}
 {customerProfileOpen&&<CustomerProfileModal profile={customerProfile} loading={customerProfileLoading} onClose={()=>setCustomerProfileOpen(false)} onEdit={editCustomerProfile} onCreatePackage={()=>{setCustomerProfileOpen(false);nav('packages')}}/>}
 {recipeOpen&&<Modal title={editRecipe?'Edit recipe':'Create new recipe'} onClose={()=>setRecipeOpen(false)} wide><RecipeForm form={recipeForm} setForm={setRecipeForm} ingredients={ingredients} allergens={allergens} upload={upload} uploading={uploading} submit={saveRecipe} cancel={()=>setRecipeOpen(false)}/></Modal>}
 {pkgNewCustomerOpen&&<Modal title="Create customer" onClose={()=>setPkgNewCustomerOpen(false)}><form onSubmit={createPackageCustomer} className="formGrid"><Field label="First name"><input value={pkgNewCustomerForm.firstName} onChange={e=>setPkgNewCustomerForm({...pkgNewCustomerForm,firstName:e.target.value})} required/></Field><Field label="Last name"><input value={pkgNewCustomerForm.lastName} onChange={e=>setPkgNewCustomerForm({...pkgNewCustomerForm,lastName:e.target.value})} required/></Field><Field label="Email"><input type="email" value={pkgNewCustomerForm.email} onChange={e=>setPkgNewCustomerForm({...pkgNewCustomerForm,email:e.target.value})} required/></Field><Field label="Initial password" help="The customer can sign in with these credentials later."><input type="password" minLength="6" value={pkgNewCustomerForm.password} onChange={e=>setPkgNewCustomerForm({...pkgNewCustomerForm,password:e.target.value})} required/></Field><div className="modalActions"><button type="button" className="secondary" onClick={()=>setPkgNewCustomerOpen(false)}>Cancel</button><button className="primary">Create customer</button></div></form></Modal>}
 {reviewPackageId&&<Modal title="Review & finalise customer package" onClose={()=>setReviewPackageId('')}><form onSubmit={confirmReviewPackage} className="formGrid"><div className="packageReviewIntro span2"><b>Confirm the customer's requested meal package</b><span>Enter the final meal subtotal approved by the outlet. HealthApp will recalculate applicable GST, delivery, platform fee and settlement from this amount. Leave it blank to use the calculated meal amount.</span></div><Field label="Final meal amount (₹)" help="This is the meal subtotal before delivery and applicable taxes/fees."><input type="number" min="0" step=".01" value={reviewPackageForm.finalMealAmount} onChange={e=>setReviewPackageForm({...reviewPackageForm,finalMealAmount:e.target.value})}/></Field><Field label="Review note"><input maxLength="500" value={reviewPackageForm.discountReason} onChange={e=>setReviewPackageForm({...reviewPackageForm,discountReason:e.target.value})} placeholder="Approved after menu/package review"/></Field><div className="modalActions span2"><button type="button" className="secondary" onClick={()=>setReviewPackageId('')}>Cancel</button><button className="primary">Confirm package & enable payment</button></div></form></Modal>}
 {manualPaymentPackageId&&<Modal title="Record manual payment" onClose={()=>setManualPaymentPackageId('')}><form onSubmit={e=>{e.preventDefault();markPackagePaid()}} className="formGrid"><Field label="Payment method"><select value={manualPaymentMethod} onChange={e=>setManualPaymentMethod(e.target.value)}><option value="Cash">Cash</option><option value="UPI">UPI</option><option value="BankTransfer">Bank transfer</option><option value="Manual">Other manual</option></select></Field><div className="packageManualPaymentNote span2"><b>This activates the package immediately.</b><span>The payment is recorded against the customer package and scheduled deliveries are created.</span></div><div className="modalActions span2"><button type="button" className="secondary" onClick={()=>setManualPaymentPackageId('')}>Cancel</button><button className="primary">Mark paid & activate</button></div></form></Modal>}
 {pkgAddressOpen&&<OutletPackageAddressModal form={pkgAddressForm} setForm={setPkgAddressForm} onMapPick={pickPackageAddress} onSave={savePackageAddress} onClose={()=>setPkgAddressOpen(false)} city={dash?.outlet?.city||''}/>} 
 {pricingOpen&&<Modal title="Add delivery pricing slab" onClose={()=>setPricingOpen(false)}><form onSubmit={addPrice} className="formGrid"><Field label="Maximum distance (km)" help="Example: 2, 5, 10, 15, 20."><input type="number" min=".1" step=".1" value={priceForm.maxDistanceKm} onChange={e=>setPriceForm({...priceForm,maxDistanceKm:e.target.value})} required/></Field><Field label="Delivery fee (₹)"><input type="number" min="0" step=".01" value={priceForm.fee} onChange={e=>setPriceForm({...priceForm,fee:e.target.value})} required/></Field><div className="modalActions"><button type="button" className="secondary" onClick={()=>setPricingOpen(false)}>Cancel</button><button className="primary">Add slab</button></div></form></Modal>}
 {selectedSub&&<Modal title="Subscription details" onClose={()=>setSelectedSub(null)} wide><SubscriptionDetail data={selectedSub}/></Modal>}
 {staffOpen&&<Modal title={staffEditing?'Edit staff account':'Add outlet staff'} onClose={()=>{if(!staffSaving){setStaffOpen(false);setStaffError('')}}}><form onSubmit={saveStaff} className="formGrid">{staffError&&<div className="statusBanner error span2"><span>{staffError}</span><button type="button" onClick={()=>setStaffError('')}>×</button></div>}<Field label="First name"><input value={staffForm.firstName} onChange={e=>setStaffForm({...staffForm,firstName:e.target.value})} required/></Field><Field label="Last name"><input value={staffForm.lastName} onChange={e=>setStaffForm({...staffForm,lastName:e.target.value})} required/></Field><Field label="Email"><input type="email" value={staffForm.email} onChange={e=>setStaffForm({...staffForm,email:e.target.value})} required/></Field><Field label={staffEditing?'New password (optional)':'Temporary password'} help={staffEditing?'Leave blank to keep the current password.':'At least 6 characters.'}><input type="password" value={staffForm.password} onChange={e=>setStaffForm({...staffForm,password:e.target.value})} minLength="6" required={!staffEditing}/></Field><Field label="Role"><select value={staffForm.role} onChange={e=>setStaffForm({...staffForm,role:e.target.value})}><option value="OutletManager">Outlet Manager</option><option value="KitchenStaff">Kitchen Staff</option><option value="Driver">Driver</option></select></Field>{staffEditing&&<label className="field check"><span>Account status</span><span><input type="checkbox" checked={staffIsActive} onChange={e=>setStaffIsActive(e.target.checked)}/> Active</span></label>}<div className="modalActions"><button type="button" className="secondary" onClick={()=>{setStaffOpen(false);setStaffError('')}}>Cancel</button><button className="primary" disabled={staffSaving}>{staffSaving?(staffEditing?'Saving…':'Creating…'):(staffEditing?'Save changes':'Create staff account')}</button></div></form></Modal>}
 {driverOpen&&<Modal title="Add in-house driver" onClose={()=>setDriverOpen(false)}><form onSubmit={saveDriver} className="formGrid"><Field label="First name"><input value={driverForm.firstName} onChange={e=>setDriverForm({...driverForm,firstName:e.target.value})} required/></Field><Field label="Last name"><input value={driverForm.lastName} onChange={e=>setDriverForm({...driverForm,lastName:e.target.value})} required/></Field><Field label="Email"><input type="email" value={driverForm.email} onChange={e=>setDriverForm({...driverForm,email:e.target.value})} required/></Field><Field label="Password"><input type="password" value={driverForm.password} onChange={e=>setDriverForm({...driverForm,password:e.target.value})} minLength="6" required/></Field><div className="modalActions"><button type="button" className="secondary" onClick={()=>setDriverOpen(false)}>Cancel</button><button className="primary">Add driver</button></div></form></Modal>}
 {tierOpen&&<Modal title={editTier?'Edit discount tier':''} onClose={()=>setTierOpen(false)}><form onSubmit={saveTier} className="formGrid"><Field label="Minimum meals"><input type="number" min="1" value={tierForm.minMeals} onChange={e=>setTierForm({...tierForm,minMeals:e.target.value})}/></Field><Field label="Maximum meals"><input type="number" min="1" placeholder="Blank for open-ended" value={tierForm.maxMeals} onChange={e=>setTierForm({...tierForm,maxMeals:e.target.value})}/></Field><Field label="1 week %"><input type="number" min="0" max="100" step=".01" value={tierForm.oneWeekPercent} onChange={e=>setTierForm({...tierForm,oneWeekPercent:e.target.value})}/></Field><Field label="2 weeks %"><input type="number" min="0" max="100" step=".01" value={tierForm.twoWeeksPercent} onChange={e=>setTierForm({...tierForm,twoWeeksPercent:e.target.value})}/></Field><Field label="1 month %"><input type="number" min="0" max="100" step=".01" value={tierForm.oneMonthPercent} onChange={e=>setTierForm({...tierForm,oneMonthPercent:e.target.value})}/></Field><div className="modalActions"><button type="button" className="secondary" onClick={()=>setTierOpen(false)}>Cancel</button><button className="primary">Save tier</button></div></form></Modal>}
 </div>
}
function TeamPage({staff,onCreate,onEdit,loading,onRefresh,search, setSearch,roleFilter,setRoleFilter}){
 const roleText=role=>OUTLET_ROLE_LABELS[role]||role;
 const roles=['All','OutletManager','KitchenStaff','Driver','OutletAdmin'];
 const filtered=staff.filter(x=>{
  const matchesRole=roleFilter==='All'||x.role===roleFilter;
  const q=search.trim().toLowerCase();
  const matchesSearch=!q||[x.name,x.email,roleText(x.role)].some(v=>String(v||'').toLowerCase().includes(q));
  return matchesRole&&matchesSearch;
 });
 const counts=staff.reduce((acc,x)=>{acc.total++;if(x.isActive)acc.active++;acc[x.role]=(acc[x.role]||0)+1;return acc},{total:0,active:0});
 return <div className="page">
  <div className="pageHead">
   <div><span className="eyebrow">OUTLET TEAM</span><h1>Staff & roles</h1><p>Give each team member only the workspace needed for their job.</p></div>
   <div className="pageActions"><button className="secondary" type="button" onClick={onRefresh} disabled={loading}>↻ {loading?'Refreshing…':'Refresh'}</button><button className="primary" onClick={onCreate}>+ Add staff</button></div>
  </div>
  <div className="teamSummary">
   <div><b>{counts.total}</b><span>Staff accounts</span></div>
   <div><b>{counts.active}</b><span>Active</span></div>
   <div><b>{counts.OutletManager||0}</b><span>Managers</span></div>
   <div><b>{counts.KitchenStaff||0}</b><span>Kitchen</span></div>
   <div><b>{counts.Driver||0}</b><span>Drivers</span></div>
  </div>
  <section className="panel teamPanel">
   <div className="teamRoleGuide">
    <div><b>Outlet Admin</b><span>Full control, including billing, settings, staff and operational workflows.</span></div>
    <div><b>Outlet Manager</b><span>Runs customers, packages, subscriptions, kitchen, menu and delivery operations.</span></div>
    <div><b>Kitchen Staff</b><span>Focused on kitchen production, recipes and the weekly menu.</span></div>
    <div><b>Driver</b><span>Only sees assigned delivery runs and driver-specific delivery actions.</span></div>
   </div>
   <div className="teamToolbar">
    <input value={search} onChange={e=>setSearch(e.target.value)} placeholder="Search staff by name or email"/>
    <select value={roleFilter} onChange={e=>setRoleFilter(e.target.value)}>{roles.map(r=><option key={r} value={r}>{r==='All'?'All roles':roleText(r)}</option>)}</select>
    <span>{filtered.length} of {staff.length} shown</span>
   </div>
   <div className="teamList">
    {loading&&!staff.length&&<div className="teamLoading"><span className="teamSpinner"></span><b>Loading staff accounts…</b></div>}
    {!loading&&staff.length>0&&filtered.map(x=><article className="teamMember" key={x.id}>
     <div className="teamAvatar">{(x.name||'S').slice(0,1).toUpperCase()}</div>
     <div className="teamMemberMain"><b>{x.name}</b><span>{x.email}</span></div>
     <span className="pill">{roleText(x.role)}</span>
     <span className={x.isActive?'teamActive':'teamInactive'}>{x.isActive?'Active':'Inactive'}</span>
     {x.role!=='OutletAdmin'&&<button className="secondary smallBtn" onClick={()=>onEdit(x)}>Edit</button>}
    </article>)}
    {!loading&&staff.length>0&&!filtered.length&&<Empty title="No matching staff" text="Try a different name, email or role filter."/>}
    {!loading&&!staff.length&&<Empty title="No staff accounts yet" text="Add an outlet manager, kitchen staff member or driver."/>}
   </div>
  </section>
 </div>
}
function Dashboard({dash,recipes,pricing,nav,openSubscription,onOpenPending}) {
 const stats=[
  ['kitchen','Today’s meal boxes',dash?.todayMealBoxes??0],
  ['routes','Delivery points',dash?.todayDeliveryPoints??0],
  ['subscriptions','Active subscriptions',dash?.activeSubscriptions??0],
  ['customers','New customers · 7d',dash?.newCustomers??0],
  ['subscriptions','New subscriptions · 7d',dash?.newSubscriptions??0],
  ['orders','7-day sales',money(dash?.sales7d??0)]
 ];
 const slotRows=dash?.todayDeliverySlots||[];
 const pending=dash?.todayPendingDeliveries??0;
 return <div className="page">
  <div className="hero">
   <div><div className="eyebrow">OUTLET OPERATIONS</div><h1>Today at {dash?.outlet?.name||'your outlet'}</h1><p>Run production, deliveries and customer activity from one operational view.</p></div>
   <div className="heroActions"><button className="secondary" onClick={()=>nav('routes')}>Plan deliveries</button><button className="primary" onClick={()=>nav('kitchen')}>Open kitchen</button></div>
  </div>
  <div className="statGrid dashboardStats">{stats.map(s=><button className="statCard" key={s[1]} onClick={()=>nav(s[0])}><div><span>{s[1]}</span><b>{s[2]}</b></div><i>→</i></button>)}</div>

  <div className="dashboardGrid">
   <section className="panel deliveryOverview">
    <div className="panelHead"><div><h3>Today’s delivery windows</h3><p>Live workload by scheduled meal window</p></div><button type="button" className={pending>0?'pill amber clickableStatus':'pill green clickableStatus'} onClick={()=>pending>0&&onOpenPending?.()} disabled={pending===0} title={pending>0?'Open pending deliveries':'No pending deliveries'}>{pending} pending</button></div>
    <div className="deliverySlotList">
     {slotRows.map(s=><div className="deliverySlotRow" key={s.mealSlot}>
      <div><b>{s.mealSlot}</b><span>{s.deliveryWindow}</span></div>
      <strong>{s.deliveryJobs}</strong>
      <div className="deliverySlotProgress"><span style={{width:s.deliveryJobs ? (Math.round((s.completedJobs/s.deliveryJobs)*100)+'%') : '0%'}}/></div>
      <span className="slotCompleted">{s.completedJobs} done</span>
     </div>)}
    </div>
    <div className="deliveryOverviewFooter"><span>{dash?.todayDeliveryJobs??0} delivery jobs</span><span>{dash?.todayDeliveryPoints??0} physical stops</span><button className="linkBtn" onClick={()=>nav('routes')}>Open routes →</button></div>
   </section>

   <section className="panel operationsSummary">
    <div className="panelHead"><div><h3>Operations summary</h3><p>What needs attention today</p></div></div>
    <div className="opsMetric"><div className="opsIcon">□</div><div><b>{dash?.todayMealBoxes??0}</b><span>meal boxes to prepare</span></div><button className="linkBtn" onClick={()=>nav('kitchen')}>Kitchen →</button></div>
    <div className="opsMetric"><div className="opsIcon">↗</div><div><b>{dash?.todayDeliveryJobs??0}</b><span>delivery jobs scheduled</span></div><button className="linkBtn" onClick={()=>nav('deliveries')}>Deliveries →</button></div>
    <div className="opsMetric"><div className="opsIcon">⌖</div><div><b>{dash?.todayDeliveryPoints??0}</b><span>physical delivery stops</span></div><button className="linkBtn" onClick={()=>nav('routes')}>Routes →</button></div>
    <button type="button" className="opsMetric opsMetricAction" onClick={()=>pending>0&&onOpenPending?.()} disabled={pending===0} title={pending>0?'Open pending deliveries':'No pending deliveries'}><div className="opsIcon">!</div><div><b>{pending}</b><span>pending deliveries</span></div><span className={pending>0?'pill amber':'pill green'}>{pending>0?'Needs action':'On track'}</span></button>
   </section>
  </div>

  <div className="twoCol">
   <section className="panel">
    <div className="panelHead"><div><h3>Latest customer activity</h3><p>Most recent subscription starts</p></div><button className="linkBtn" onClick={()=>nav('subscriptions')}>Open subscriptions →</button></div>
    {(dash?.recentSubscriptions||[]).map(s=><button className="miniRow clickable" key={s.id} onClick={()=>openSubscription(s.id)}>
      <div className="avatar sm">{(s.customerName||'C')[0]}</div>
      <div><b>{s.customerName}</b><span>{s.planName} · {s.mealCount} meals · {new Date(s.startDate).toLocaleDateString()}</span></div>
      <span className="pill green">{s.status}</span>
    </button>)}
    {!(dash?.recentSubscriptions||[]).length&&<Empty title="No recent subscription activity" text="New customer packages will appear here."/>}
   </section>

   <section className="panel">
    <div className="panelHead"><div><h3>Business pulse · last 7 days</h3><p>Commercial activity across the outlet</p></div></div>
    <div className="pulseGrid">
     <div><span>Sales</span><b>{money(dash?.sales7d??0)}</b></div>
     <div><span>Orders</span><b>{dash?.orders7d??0}</b></div>
     <div><span>New customers</span><b>{dash?.newCustomers??0}</b></div>
     <div><span>New subscriptions</span><b>{dash?.newSubscriptions??0}</b></div>
    </div>
    <div className="panelCallout"><b>Active customer base</b><span>{dash?.activeCustomers??0} customers across {dash?.activeSubscriptions??0} active subscriptions.</span></div>
   </section>
  </div>

  <div className="twoCol">
   <section className="panel"><div className="panelHead"><div><h3>Recipe catalogue</h3><p>{dash?.recipes??recipes.length} active meals customers can select</p></div><button className="linkBtn" onClick={()=>nav('recipes')}>Manage →</button></div>{recipes.slice(0,6).map(r=><div className="miniRow" key={r.id}><div className="thumb">{r.imageUrl?<img src={img(r.imageUrl)} alt=""/>:r.name[0]}</div><div><b>{r.name}</b><span>{r.category} · {r.calories} kcal · {money(r.pricePerMeal)}</span></div><span className="pill green">Active</span></div>)}{!recipes.length&&<Empty title="No recipes" text="Create your first meal."/>}</section>
   <section className="panel"><div className="panelHead"><div><h3>Delivery pricing</h3><p>Distance-based fees</p></div><button className="linkBtn" onClick={()=>nav('settings')}>Configure →</button></div>{pricing.map(p=><div className="priceRow" key={p.id}><span>Up to {p.maxDistanceKm} km</span><b>{money(p.fee)}</b></div>)}{!pricing.length&&<Empty title="No slabs" text="Add the first pricing rule."/>}</section>
  </div>
 </div>
}
function PackageMapClick({onPick}){useMapEvents({click:e=>onPick(e.latlng.lat,e.latlng.lng)});return null}
function OutletPackageAddressModal({form,setForm,onMapPick,onSave,onClose,city}){const lat=Number(form.latitude),lng=Number(form.longitude);const center=Number.isFinite(lat)&&Number.isFinite(lng)?[lat,lng]:[20.5937,78.9629];return <Modal title="Add customer delivery address" onClose={onClose} wide><div className="packageAddressMap"><div className="packageAddressMapHint"><div><b>Pin the exact delivery point</b><span>Click the customer's exact delivery location. Coordinates are saved with the address.</span></div><strong>{Number.isFinite(lat)&&Number.isFinite(lng)?lat.toFixed(6)+', '+lng.toFixed(6):'Not pinned'}</strong></div><div className="packageAddressMapFrame"><MapContainer center={center} zoom={13} scrollWheelZoom className="packageAddressMap"><TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" attribution="&copy; OpenStreetMap contributors"/><PackageMapClick onPick={onMapPick}/>{Number.isFinite(lat)&&Number.isFinite(lng)&&<CircleMarker center={center} radius={9}/>}</MapContainer></div></div><form onSubmit={onSave} className="formGrid"><Field label="Delivery city"><input value={form.city||city||''} onChange={e=>setForm({...form,city:e.target.value})} required/></Field><Field label="Locality / Area"><input value={form.locality||''} onChange={e=>setForm({...form,locality:e.target.value})}/></Field><Field label="Pincode"><input value={form.pincode||''} onChange={e=>setForm({...form,pincode:e.target.value})}/></Field><Field label="Label"><select value={form.label} onChange={e=>setForm({...form,label:e.target.value})}><option>Home</option><option>Office</option><option>Gym</option><option>Other</option></select></Field><Field label="Contact name"><input value={form.contactName||''} onChange={e=>setForm({...form,contactName:e.target.value})} required/></Field><Field label="Phone"><input value={form.contactPhone||''} onChange={e=>setForm({...form,contactPhone:e.target.value})} required/></Field><Field label="Address line 1"><input value={form.addressLine1||''} onChange={e=>setForm({...form,addressLine1:e.target.value})} placeholder="House / flat number and street" required/></Field><Field label="Address line 2"><input value={form.addressLine2||''} onChange={e=>setForm({...form,addressLine2:e.target.value})} placeholder="Colony / landmark"/></Field><div className="coordinateReadout span2"><span>Exact coordinates</span><b>{Number.isFinite(lat)&&Number.isFinite(lng)?lat.toFixed(6)+', '+lng.toFixed(6):'Pick a point on the map'}</b></div><label className="checkLabel span2"><input type="checkbox" checked={Boolean(form.isDefault)} onChange={e=>setForm({...form,isDefault:e.target.checked})}/> Set as default address</label><div className="modalActions"><button type="button" className="secondary" onClick={onClose}>Cancel</button><button className="primary">Save address</button></div></form></Modal>}

function OutletPackageMultiSelect({options,selectedIds,onChange}){
 const [open,setOpen]=useState(false);
 const rootRef=useRef(null);
 const selected=(selectedIds||[]).map(id=>options.find(x=>x.recipeId===id)).filter(Boolean);
 const groups=useMemo(()=>{
   const map={};
   for(const item of options){
     const group=String(item.optionGroup||item.mealType||'Meal').trim()||'Meal';
     (map[group]??=[]).push(item);
   }
   return map;
 },[options]);
 const toggle=id=>{
   const current=selectedIds||[];
   if(current.includes(id)){onChange(current.filter(x=>x!==id));return;}
   const item=options.find(x=>x.recipeId===id);
   if(!item)return;
   const group=String(item.optionGroup||item.mealType||'Meal').trim()||'Meal';
   const groupItems=groups[group]||[];
   const max=Math.max(1,...groupItems.map(x=>Number(x.maxSelections||1)));
   const count=current.filter(x=>groupItems.some(g=>g.recipeId===x)).length;
   if(count>=max)return;
   onChange([...current,id]);
 };
 useEffect(()=>{
   if(!open)return;
   const close=e=>{if(!rootRef.current?.contains(e.target))setOpen(false)};
   document.addEventListener('mousedown',close);
   return()=>document.removeEventListener('mousedown',close);
 },[open]);
 return <div className={'outletPackageMultiSelect '+(open?'open':'')} ref={rootRef}>
   <div className="outletPackageMultiTrigger" role="button" tabIndex={0} aria-expanded={open} onClick={()=>setOpen(v=>!v)} onKeyDown={e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();setOpen(v=>!v)}}}>
     {selected.length?<div className="outletPackageSelectedList">{selected.map(item=><span className="outletPackageSelectedChip" key={item.recipeId}>{item.recipeName}<button type="button" aria-label={'Remove '+item.recipeName} onClick={e=>{e.stopPropagation();toggle(item.recipeId)}}>×</button></span>)}</div>:<span className="outletPackagePlaceholder">Choose meal, juice, snack, curd…</span>}
     <span className="outletPackageChevron">{open?'⌃':'⌄'}</span>
   </div>
   {open&&<div className="outletPackageMultiMenu">
     <div className="outletPackageMultiHint">Select the main meal and any configured add-ons. Each option group enforces its maximum selections.</div>
     {Object.entries(groups).map(([group,items])=>{
       const max=Math.max(1,...items.map(x=>Number(x.maxSelections||1)));
       const count=(selectedIds||[]).filter(id=>items.some(x=>x.recipeId===id)).length;
       return <section className="outletPackageOptionGroup" key={group}>
         <div className="outletPackageOptionGroupHead"><div><b>{group}</b><small>{items.some(x=>x.isRequired)?'Required':'Optional'} · up to {max}</small></div><strong>{count}/{max}</strong></div>
         <div className="outletPackageOptionList">{items.map(item=>{
           const active=(selectedIds||[]).includes(item.recipeId);
           const disabled=!active&&count>=max;
           return <button type="button" key={item.recipeId} className={active?'outletPackageOption active':'outletPackageOption'} disabled={disabled} onClick={()=>toggle(item.recipeId)}>
             <span className="outletPackageOptionCheck">{active?'✓':'+'}</span>
             <span><b>{item.recipeName}</b><small>{item.mealType||group} · ₹{Number(item.pricePerMeal||0).toFixed(2)}</small></span>
           </button>;
         })}</div>
       </section>;
     })}
   </div>}
 </div>;
}

function OutletPackageBuilder({customers,recipes,menu,customerId,addresses,duration,setDuration,deliveryMode,setDeliveryMode,startDate,setStartDate,dates,selections,setSelections,dayAddresses,setDayAddresses,mealAddresses,setMealAddresses,portions,setPortions,discountType,setDiscountType,discountValue,setDiscountValue,discountReason,setDiscountReason,quote,selectedCount,onCustomerChange,onNewCustomer,quotePackage,createPackage,confirmedAllergies,setConfirmedAllergies,onAddAddress}){
 const menuFor=(date,slot)=>menu.filter(x=>Number(x.dayOfWeek)===dayId(date)&&Number(x.mealSlotValue)===Number(slot));
 const setSlotSelections=(date,slot,items)=>setSelections(v=>({...v,[packageKey(date,slot)]:items.map(recipeId=>({recipeId}))}));
 const setPortion=(date,slot,recipeId,value)=>setPortions(v=>({...v,[packageItemKey(date,slot,recipeId)]:Number(value)}));
 const setDay=(date,value)=>setDayAddresses(v=>({...v,[date]:value}));
 const setMealAddress=(date,slot,value)=>setMealAddresses(v=>({...v,[packageKey(date,slot)]:value}));
 return <div className="page"><div className="pageHead"><div><span className="eyebrow">OUTLET SALES</span><h1>Create customer package</h1><p>Build a custom schedule for a new or existing customer, apply a negotiated discount, review GST and send the package for acceptance.</p></div><div className="packageBuilderBadge"><b>{selectedCount}</b><span>items selected</span></div></div>
 <section className="panel packageCustomerPanel"><div className="panelHead"><div><h3>1. Customer</h3><p>Select an existing customer or create a new one.</p></div><button className="secondary" type="button" onClick={onNewCustomer}>+ New customer</button></div><div className="packageCustomerGrid"><label className="field"><span>Customer</span><select value={customerId} onChange={e=>onCustomerChange(e.target.value)}><option value="">Choose customer…</option>{customers.map(x=><option key={x.id} value={x.id}>{x.firstName} {x.lastName} · {x.email}</option>)}</select></label><div className="packageAddressSummary"><span>Delivery addresses</span>{customerId?(<>{addresses.length?<select value={dayAddresses[dates[0]]||''} onChange={e=>setDayAddresses(v=>Object.fromEntries(dates.map(d=>[d,e.target.value])))}><option value="">Choose default address…</option>{addresses.map(a=><option key={a.id} value={a.id}>{a.label} · {a.areaName||a.city}</option>)}</select>:<button type="button" className="linkBtn" onClick={onAddAddress}>+ Add customer address</button>}<button type="button" className="linkBtn" onClick={()=>onAddAddress(null)}>Manage / add address</button></>):<small>Select a customer first.</small>}</div></div></section>
 <section className="panel packageSetupPanel"><div className="panelHead"><div><h3>2. Package setup</h3><p>Set dates, delivery mode and your negotiated customer discount.</p></div></div><div className="formGrid"><Field label="Duration"><select value={duration} onChange={e=>{setDuration(e.target.value);setSelections({});setDayAddresses({});setMealAddresses({});setPortions({})}}><option value="ThreeDays">3 days</option><option value="FiveDays">5 days</option><option value="OneWeek">1 week</option><option value="TwoWeeks">2 weeks</option><option value="OneMonth">4 weeks</option></select></Field><Field label="Start date"><input type="date" value={startDate} onChange={e=>{setStartDate(e.target.value);setSelections({});setDayAddresses({});setMealAddresses({});setPortions({})}}/></Field><div className="packageDeliveryModeField"><span className="packageFieldLabel">Delivery frequency</span><div className="packageDeliveryModeCards">
 <button type="button" className={deliveryMode==='OneDeliveryPerDay'?'packageDeliveryModeCard selected':'packageDeliveryModeCard'} onClick={()=>{setDeliveryMode('OneDeliveryPerDay');setMealAddresses({})}}><span className="packageModeIcon">1×</span><span><b>One delivery per day</b><small>Combine that day's selected meals into one delivery.</small></span><i>{deliveryMode==='OneDeliveryPerDay'?'✓':'○'}</i></button>
 <button type="button" className={deliveryMode==='IndividualMealDelivery'?'packageDeliveryModeCard selected':'packageDeliveryModeCard'} onClick={()=>{setDeliveryMode('IndividualMealDelivery');setDayAddresses({})}}><span className="packageModeIcon">🍽</span><span><b>Each meal separately</b><small>Each selected meal slot can have its own delivery address.</small></span><i>{deliveryMode==='IndividualMealDelivery'?'✓':'○'}</i></button>
 </div></div><Field label="Discount type"><select value={discountType} onChange={e=>setDiscountType(e.target.value)}><option value="None">No negotiated discount</option><option value="Percent">Percentage</option><option value="Fixed">Fixed amount</option></select></Field>{discountType!=='None'&&<Field label={discountType==='Percent'?'Discount %':'Discount amount (₹)'}><input type="number" min="0" step=".01" max={discountType==='Percent'?100:undefined} value={discountValue} onChange={e=>setDiscountValue(e.target.value)}/></Field>}{discountType!=='None'&&<Field label="Discount reason" help="Required for audit when a negotiated discount is applied."><input value={discountReason} onChange={e=>setDiscountReason(e.target.value)} placeholder="Existing customer · cash negotiation…" required={discountType!=='None'}/></Field>}</div></section>
 <section className="panel packageSchedulePanel"><div className="panelHead"><div><h3>3. Meal schedule</h3><p>Select a main meal plus any configured juice, snack, curd or other items for each day & slot.</p></div><span className="count">{dates.length} days</span></div>{dates.map(date=><div className="packageDayRow" key={date}><div className="packageDayHead"><div className="packageDayTitle"><span className="packageDayNumber">{dayId(date)}</span><div><b>{dayName(dayId(date))}</b><span>{date}</span></div></div>{deliveryMode==='OneDeliveryPerDay'&&<label className="field packageDayAddressField"><span>Delivery address for this day</span><div className="packageAddressControl"><select value={dayAddresses[date]||''} onChange={e=>setDay(date,e.target.value)}><option value="">Choose address…</option>{addresses.map(a=><option key={a.id} value={a.id}>{a.label} · {a.areaName||a.city}</option>)}</select><button type="button" className="packageAddAddressBtn" onClick={()=>onAddAddress({type:'day',date})}>+ Add address</button></div></label>}{deliveryMode==='IndividualMealDelivery'&&<div className="packageModeHint"><b>Meal-by-meal delivery</b><span>Select an address inside each chosen meal slot.</span></div>}</div><div className="packageSlotGrid">{[['Morning',1],['Afternoon',2],['Evening',3],['Night',4]].map(([label,slot])=>{const opts=menuFor(date,slot);const k=packageKey(date,slot);const selectedIds=Array.isArray(selections[k])?selections[k].filter(Boolean).map(x=>typeof x==='string'?x:x.recipeId):(selections[k]?.recipeId?[selections[k].recipeId]:[]);return <div className={opts.length?'packageSlotCard':'packageSlotCard disabled'} key={slot}><div><b>{label}</b><small>{opts.length?(selectedIds.length?selectedIds.length+' items selected':opts.length+' choices'):'No menu'}</small></div>{opts.length&&<><OutletPackageMultiSelect options={opts} selectedIds={selectedIds} onChange={ids=>setSlotSelections(date,slot,ids)}/>{selectedIds.length>0&&<div className="packageSelectedItems">{selectedIds.map(recipeId=>{const item=opts.find(x=>x.recipeId===recipeId);const pk=packageItemKey(date,slot,recipeId);return <div className="packageSelectedItem" key={recipeId}><div><b>{item?.recipeName||'Selected item'}</b><small>{item?.mealType||item?.optionGroup||'Item'}</small></div><label><span>Portion</span><select value={portions[pk]||1} onChange={e=>setPortion(date,slot,recipeId,e.target.value)}><option value="1">Regular</option><option value="2">Large</option></select></label></div>})}</div>}{deliveryMode==='IndividualMealDelivery'&&selectedIds.length>0&&<label className="packageMealAddressField"><span>Delivery address</span><div className="packageAddressControl"><select className="packageItemAddressSelect" value={mealAddresses[k]||''} onChange={e=>setMealAddress(date,slot,e.target.value)}><option value="">Choose address…</option>{addresses.map(a=><option key={a.id} value={a.id}>{a.label} · {a.areaName||a.city}</option>)}</select><button type="button" className="packageAddAddressBtn" onClick={()=>onAddAddress({type:'meal',date,slot})}>+ Add address</button></div></label>}</>}</div>})}</div></div>)}</section>
 <section className="packageBottomGrid"><section className="panel packageWarningsPanel">{quote?.allergyWarnings?.length?<><div className="panelHead"><div><h3>4. Allergy & safety review</h3><p>These warnings come from the customer's saved allergies and the recipe ingredient master.</p></div></div>{quote.allergyWarnings.map(w=><label className="packageWarningRow" key={w.recipeId}><input type="checkbox" checked={confirmedAllergies.includes(w.recipeId)} onChange={e=>setConfirmedAllergies(e.target.checked?[...confirmedAllergies,w.recipeId]:confirmedAllergies.filter(id=>id!==w.recipeId))}/><div><b>{w.recipeName}</b><span>{w.message}</span><small>{w.matchedIngredients?.join(', ')}</small></div></label>)}</>:<div className="packageSafe"><b>Safety check</b><span>Calculate the quote to check this customer's saved allergy profile.</span></div>}</section><aside className="panel packageQuotePanel"><div className="panelHead"><div><span className="eyebrow">5. PRICE & GST</span><h3>Customer total</h3></div></div>{quote?<div className="packageQuoteRows"><div><span>Gross meals</span><b>{money(quote.grossMealAmount)}</b></div><div><span>Negotiated discount</span><b>− {money(quote.discountAmount)}</b></div><div><span>{quote.restaurantGstMode==='Inclusive'?'Taxable meal value':'Net meals'}</span><b>{money(quote.restaurantTaxableAmount)}</b></div><div><span>Restaurant GST · {Number(quote.restaurantGstRate)}% ({quote.restaurantGstMode})</span><b>{money(quote.restaurantGstAmount)}</b></div><div><span>Delivery</span><b>{money(quote.deliveryFee)}</b></div><div><span>HealthApp service fee</span><b>{money(quote.platformServiceFee)}</b></div><div><span>Service fee GST</span><b>{money(quote.platformServiceGst)}</b></div><div className="quoteGrand"><span>Total payable</span><strong>{money(quote.totalCharged)}</strong></div>{quote.restaurantGstMode==='Inclusive'&&<div className="taxIncludedNote">✓ GST is included in meal prices and reported separately.</div>}</div>:<div className="packageQuoteEmpty">Calculate the package quote to see discount, GST, delivery and the final customer total.</div>}<div className="packageBuilderActions"><button className="secondary" type="button" onClick={quotePackage} disabled={!customerId||!selectedCount}>Calculate quote</button><button className="primary big" type="button" onClick={createPackage} disabled={!quote||Boolean(quote.requiresAllergyConfirmation&&!quote.allergyWarnings?.every(w=>confirmedAllergies.includes(w.recipeId)))}>Send package to customer →</button></div><small>Online payment is completed by the customer. For cash/manual payment, use Mark paid from Subscriptions after the package is sent.</small></aside></section></div>}
function SubscriptionsPage({items,customers,onOpen,onMarkPaid,onConfirm,onCreate}) {
 const names=new Map(customers.map(x=>[x.id,(x.firstName+' '+x.lastName).trim()]));
 return <div className="page"><div className="pageHead"><div><span className="eyebrow">CUSTOMER PACKAGES</span><h1>Subscriptions</h1><p>Review customer-created packages, confirm pricing, record payment and activate delivery.</p></div><button className="primary" onClick={onCreate}>+ Create package</button></div>
 <section className="panel">{items.length?<div className="subscriptionList">{items.map(x=>{
   const review=x.packageStatus==='PendingOutletReview';
   const paymentPending=x.packageStatus==='PaymentPending'||(x.isOutletCreated&&x.packageStatus==='SentToCustomer');
   const paid=String(x.status).toLowerCase()==='active'||x.packageStatus==='Active';
   return <div className="subscriptionCard packageListCard" key={x.id} onClick={()=>onOpen(x.id)}>
     <div className="subscriptionMain"><div className="avatar">{(names.get(x.customerId)||'C').slice(0,1).toUpperCase()}</div><div><b>{x.planName}</b><span>{names.get(x.customerId)||'Customer'} · {x.isOutletCreated?'Outlet-created package':'Customer-created package'}</span></div></div>
     <div className="packageListMeta"><span className={paid?'pill green':review?'pill amber':'pill'}>{paid?'PAID / ACTIVE':review?'AWAITING PRICE REVIEW':paymentPending?'PAYMENT PENDING':x.status}</span><b>{x.mealsPerWeek} meals</b><span>{new Date(x.nextDeliveryDate).toLocaleDateString()}</span>{review&&<button type="button" className="primary smallBtn" onClick={e=>{e.stopPropagation();onConfirm(x.id)}}>Confirm & finalise price</button>}{paymentPending&&!paid&&<button type="button" className="secondary smallBtn" onClick={e=>{e.stopPropagation();onMarkPaid(x.id)}}>Mark paid</button>}<i>→</i></div>
   </div>;
 })}</div>:<Empty title="No subscriptions yet" text="Customer-created packages awaiting review will appear here."/>}</section></div>
}

function SubscriptionDetail({data}) {
 return <div className="subscriptionDetail">
  <div className="detailHeader"><div><span className="eyebrow">CUSTOMER PACKAGE</span><h2>{data.planName}</h2><p>{data.customerName} · {data.customerEmail||'No email'}</p></div><span className="pill green">{data.status}</span></div>
  <div className="detailStats">
   <div><span>Start</span><b>{new Date(data.startDate).toLocaleDateString()}</b></div><div><span>End</span><b>{new Date(data.endDate).toLocaleDateString()}</b></div><div><span>Meals</span><b>{data.mealsPerWeek}</b></div><div><span>Delivery mode</span><b>{data.deliveryMode}</b></div>
  </div>
  <div className="financialStrip"><div><span>Gross meal prices</span><b>{money(data.mealAmount)}</b></div><div><span>Discount</span><b>− {money(data.discountAmount)}</b></div><div><span>Taxable meal value</span><b>{money(data.restaurantTaxableAmount??data.mealAmount)}</b></div><div><span>Restaurant GST · {Number(data.restaurantGstRate||0)}% ({data.restaurantGstMode||'Exclusive'})</span><b>{money(data.restaurantGstAmount)}</b></div><div><span>Package value incl. GST</span><strong>{money(data.packageAmountWithGst)}</strong></div><div><span>Delivery fees</span><b>{money(data.deliveryFee)}</b></div></div>
  <h3>Meal schedule</h3>
  <div className="scheduleTable"><div className="scheduleRow header"><span>Date</span><span>Time</span><span>Meal</span><span>Portion</span><span>Address</span><span>Status</span></div>
   {data.meals.map(m=><div className="scheduleRow" key={m.selectionId}><span>{new Date(m.mealDate).toLocaleDateString()}</span><span>{m.mealSlotName}<small>{m.deliveryWindow}</small></span><span><b>{m.mealName}</b><small>{m.category}</small></span><span>{m.portionSize}</span><span><b>{m.addressLabel||'Address'}</b><small>{m.address + (m.areaName ? ', ' + m.areaName : '') + (m.pincode ? ' ' + m.pincode : '')}</small><small>{m.contactPhone}</small></span><span className="pill">{m.status}</span></div>)}
  </div>
 </div>
}

function KitchenPage({data,date,setDate,refresh}) {
 const [printOpen,setPrintOpen]=useState(false);
 const [printMode,setPrintMode]=useState('');
 const [printSlots,setPrintSlots]=useState([2,3]);
 const slots=[
  {value:1,name:'Morning'},
  {value:2,name:'Afternoon'},
  {value:3,name:'Evening'},
  {value:4,name:'Night'}
 ];
 const selectedLabels=useMemo(()=>data?.labels?.filter(l=>printSlots.includes(Number(l.mealSlot)))||[],[data,printSlots]);
 const production=useMemo(()=>{
  const map=new Map();
  selectedLabels.forEach(l=>{
   const key=[l.mealName,l.category,l.portionSize].join('|');
   const current=map.get(key);
   map.set(key,{mealName:l.mealName,category:l.category,portionSize:l.portionSize,quantity:(current?.quantity||0)+1});
  });
  return [...map.values()].sort((a,b)=>a.mealName.localeCompare(b.mealName)||a.portionSize.localeCompare(b.portionSize));
 },[selectedLabels]);
 const toggleSlot=value=>setPrintSlots(x=>x.includes(value)?x.filter(v=>v!==value):[...x,value]);
 const doPrint=mode=>{
  if(mode==='kitchen'&&!production.length)return;
  if(mode==='labels'&&!selectedLabels.length)return;
  setPrintMode(mode);
  setPrintOpen(false);
 };
 useEffect(()=>{
  if(!printMode)return;
  const timer=setTimeout(()=>{window.print();setPrintMode('')},120);
  return()=>clearTimeout(timer);
 },[printMode]);
 return <div className="page">
  <div className="pageHead no-print"><div><span className="eyebrow">KITCHEN OPERATIONS</span><h1>Kitchen & Labels</h1><p>Daily production quantities and concise delivery labels for {new Date(date).toLocaleDateString()}.</p></div><div className="pageActions"><input type="date" value={date} onChange={e=>{setDate(e.target.value);refresh(e.target.value)}}/><button className="secondary" onClick={()=>refresh(date)}>Refresh</button><button className="primary" onClick={()=>setPrintOpen(true)} disabled={!data}>Print options</button></div></div>
  {!data?<Empty title="No kitchen report loaded" text="Select a date and refresh the report."/>:<>
   <div className="statGrid no-print">
    <div className="statCard static"><div><span>Meal boxes</span><b>{data.totalMeals}</b></div></div>
    <div className="statCard static"><div><span>Customers</span><b>{data.uniqueCustomers}</b></div></div>
    <div className="statCard static"><div><span>Subscriptions</span><b>{data.activeSubscriptions}</b></div></div>
   </div>
   <section className="panel no-print">
    <div className="panelHead"><div><h3>Production plan</h3><p>Prepare total quantities across the selected delivery windows.</p></div><span className="pill">{production.reduce((sum,x)=>sum+x.quantity,0)} meals</span></div>
    {production.length?<div className="productionTable"><div className="productionTableRow header"><span>Meal</span><span>Category</span><span>Portion</span><span>Qty</span></div>{production.map(p=><div className="productionTableRow" key={p.mealName+p.category+p.portionSize}><b>{p.mealName}</b><span>{p.category}</span><span>{p.portionSize}</span><strong>{p.quantity}</strong></div>)}</div>:<Empty title="No Afternoon / Evening meals" text="Choose another date or delivery window."/>}
   </section>
   <section className="no-print slotSummary"><span>Print window:</span>{slots.filter(s=>printSlots.includes(s.value)).map(s=><button key={s.value} className="slotChip active" onClick={()=>toggleSlot(s.value)}>{s.name} ×</button>)}{slots.filter(s=>!printSlots.includes(s.value)).map(s=><button key={s.value} className="slotChip" onClick={()=>toggleSlot(s.value)}>+ {s.name}</button>)}</section>

   <section className={printMode==='kitchen'?'kitchenPrintArea printTarget':'kitchenPrintArea'}>
    <div className="kitchenPrintHeader"><div className="healthLogo printLogo">H</div><div><b>HealthApp · Kitchen Production</b><span>{data.outletName} · {new Date(date).toLocaleDateString()} · {printSlots.map(v=>slots.find(s=>s.value===v)?.name).join(' + ')}</span></div></div>
    <table className="kitchenPrintTable"><thead><tr><th>Meal</th><th>Category</th><th>Portion</th><th>Qty</th></tr></thead><tbody>{production.map(p=><tr key={p.mealName+p.category+p.portionSize}><td>{p.mealName}</td><td>{p.category}</td><td>{p.portionSize}</td><td><b>{p.quantity}</b></td></tr>)}</tbody></table>
    <div className="kitchenPrintFooter">Production total: <b>{production.reduce((sum,x)=>sum+x.quantity,0)} meal boxes</b></div>
   </section>

   <section className={printMode==='labels'?'labelPrintArea printTarget':'labelPrintArea'}>
    <div className="labelsPrintHeader"><div className="healthLogo printLogo">H</div><div><b>{data.outletName}</b><span>Delivery labels · {new Date(date).toLocaleDateString()} · {printSlots.map(v=>slots.find(s=>s.value===v)?.name).join(' + ')}</span></div></div>
    <div className="labelGrid">{selectedLabels.map(l=><article className="labelCard" key={l.selectionId}>
      <div className="labelTop"><div className="labelOutletBrand">{l.logoUrl?<img src={img(l.logoUrl)} alt=""/>:<div className="outletLogo">{(l.outletName||'O')[0]}</div>}<b>{l.outletName}</b></div><span>{l.subscriptionPlanName}</span></div>
      <div className="labelCustomerCompact"><span>{l.addressLabel||'DELIVERY'}</span><b>{l.customerName}</b></div>
      <div className="labelMealCompact"><b>{l.mealName}</b><span>{l.category} · {l.portionSize}</span></div>
      <div className="labelMetaRow"><span>{l.mealSlotName}</span><b>{l.deliveryWindow}</b></div>
      <div className="labelAddressCompact"><b>{l.address}</b><span>{[l.areaName,l.pincode].filter(Boolean).join(' · ')}</span>{l.customerPhone&&<span>☎ {l.customerPhone}</span>}</div>
      <div className="labelFooterCompact"><span>HealthApp</span><b>#{String(l.selectionId).slice(0,6).toUpperCase()}</b></div>
    </article>)}</div>
   </section>
  </>}
  {printOpen&&<Modal title="Print options" onClose={()=>setPrintOpen(false)}>
   <div className="printOptions">
    <div className="printOptionBlock"><div><b>Delivery windows</b><small>Select the meal windows for both the kitchen sheet and labels.</small></div><div className="printSlotChoices">{slots.map(s=><button type="button" key={s.value} className={printSlots.includes(s.value)?'printSlotChoice checked':'printSlotChoice'} onClick={()=>toggleSlot(s.value)}>{printSlots.includes(s.value)?'✓':'+'} {s.name}</button>)}</div></div>
    <div className="printChoice"><div><b>Kitchen production sheet</b><small>One consolidated table: meal, category, portion and total quantity.</small></div><button className="primary" onClick={()=>doPrint('kitchen')} disabled={!production.length}>Print kitchen</button></div>
    <div className="printChoice"><div><b>Meal-box labels</b><small>{selectedLabels.length} concise labels for the selected windows.</small></div><button className="primary" onClick={()=>doPrint('labels')} disabled={!selectedLabels.length}>Print labels</button></div>
   </div>
  </Modal>}
 </div>
}


function IngredientConsumptionPage({data,date,setDate,refresh}) {
 return <div className="page">
  <div className="pageHead no-print">
   <div><span className="eyebrow">KITCHEN & STOCK REPORTING</span><h1>Ingredient Usage</h1><p>Ingredient quantities attributable to meals that were actually delivered on {new Date(date).toLocaleDateString()}.</p></div>
   <div className="pageActions"><input type="date" value={date} onChange={e=>{setDate(e.target.value)}}/><button className="secondary" onClick={refresh}>Refresh</button></div>
  </div>
  {!data?<Empty title="No usage report loaded" text="Select a date and refresh the report."/>:<>
   <div className="statGrid no-print">
    <div className="statCard static"><div><span>Delivered deliveries</span><b>{data.deliveredDeliveryCount}</b></div></div>
    <div className="statCard static"><div><span>Delivered meals</span><b>{data.deliveredMealCount}</b></div></div>
    <div className="statCard static"><div><span>Ingredients used</span><b>{data.ingredients?.length||0}</b></div></div>
   </div>
   <section className="panel">
    <div className="panelHead"><div><h3>Daily ingredient consumption</h3><p>Calculated from delivered delivery records and their scheduled recipe selections. Skipped, cancelled, rescheduled, expired and unused meals are excluded.</p></div><span className="pill green">{data.outletName}</span></div>
    {data.ingredients?.length?<div className="ingredientUsageTable"><div className="ingredientUsageRow header"><span>Ingredient</span><span>Quantity used</span><span>Meals</span><span>Deliveries</span><span>Recipes</span></div>{data.ingredients.map(x=><div className="ingredientUsageRow" key={x.ingredientId}><b>{x.ingredientName}</b><strong>{Number(x.quantity||0).toLocaleString('en-IN',{maximumFractionDigits:3})} {x.unit}</strong><span>{x.mealCount}</span><span>{x.deliveryCount}</span><small>{(x.recipeNames||[]).join(', ')}</small></div>)}</div>:<Empty title="No delivered meals for this date" text="Ingredient usage appears here once delivery status reaches Delivered."/>}
   </section>
   <section className="panel no-print usageFutureNote"><span className="eyebrow">READY FOR INVENTORY</span><b>This report is the consumption layer for future stock management.</b><p>When inventory is added, these same quantities can be posted as stock consumption transactions without changing the recipe or delivery workflow.</p></section>
  </>}
 </div>
}

function MapBounds({coords}){const map=useMap();useEffect(()=>{if(!coords.length)return;const bounds=coords.map(x=>[x[0],x[1]]);map.fitBounds(bounds,{padding:[30,30],maxZoom:13})},[map,coords]);return null}
function DeliveryRoutesPage({plan,drivers,date,mealSlot,setDate,setMealSlot,selectedDriverIds,setSelectedDriverIds,planRoutes,dispatchRoute,addDriver,manualPlanRoutes}){
 const routeColor=i=>`hsl(${(i*67)%360} 65% 42%)`;
 const assigned=new Set((plan?.routes||[]).flatMap(r=>r.stops.map(s=>s.addressId)));
 const bounds=plan?[[plan.outletLatitude,plan.outletLongitude],...(plan.points||[]).filter(p=>!assigned.has(p.addressId)).map(p=>[p.latitude,p.longitude]),...(plan.routes||[]).flatMap(r=>(r.geometry||[]).map(p=>[p[1],p[0]]))]:[];
 const slot=SLOTS.find(s=>s[1]===Number(mealSlot))||SLOTS[1];
 const slotName=slot[0];
 const slotWindow=slotName==='Afternoon'?'12:00–14:00':slotName==='Evening'?'17:00–19:00':slotName==='Morning'?'07:00–09:00':'20:00–22:00';
 const points=plan?.points||[];
 const unassigned=points.filter(p=>!assigned.has(p.addressId));

 const [manualMode,setManualMode]=useState(false);
 const [manualAssignments,setManualAssignments]=useState({});
 const selectedDrivers=drivers.filter(d=>selectedDriverIds.includes(d.id));
 const initializeManual=()=>{const next={};for(const route of plan?.routes||[])next[route.driverId]=(route.stops||[]).map(s=>s.addressId);setManualAssignments(next);setManualMode(true)};
 const manualAssignedIds=new Set(Object.values(manualAssignments).flat());
 const manualPool=points.filter(p=>!manualAssignedIds.has(p.addressId));
 const manualDriverPoints=id=>points.filter(p=>(manualAssignments[id]||[]).includes(p.addressId));
 const removeManualPoint=id=>setManualAssignments(prev=>Object.fromEntries(Object.entries(prev).map(([driver,ids])=>[driver,ids.filter(x=>x!==id)])));
 const dropManualPoint=(e,driverId)=>{e.preventDefault();const id=e.dataTransfer.getData('text/plain');if(!id)return;setManualAssignments(prev=>{const next={...prev};for(const k of Object.keys(next))next[k]=(next[k]||[]).filter(x=>x!==id);next[driverId]=[...(next[driverId]||[]),id];return next})};
 const saveManual=async()=>{if(!selectedDrivers.length)return manualPlanRoutes(null,'Select at least one active driver.');if(manualPool.length)return manualPlanRoutes(null,'Assign every delivery stop to a driver before saving manual routes.');const assignments=selectedDrivers.map(d=>({driverId:d.id,addressIds:manualAssignments[d.id]||[]})).filter(x=>x.addressIds.length);if(!assignments.length)return manualPlanRoutes(null,'Assign at least one delivery stop to a driver.');await manualPlanRoutes(assignments,'')};
 return <div className="page">
  <div className="pageHead"><div><span className="eyebrow">LAST-MILE OPERATIONS</span><h1>Delivery Routes</h1><p>Choose a delivery window, review the stops, then use automatic planning or manually assign stops before dispatch.</p></div><div className="pageActions"><input type="date" value={date} onChange={e=>setDate(e.target.value)}/><button className="secondary" onClick={addDriver}>+ Driver</button><button className={manualMode?'secondary':'secondary'} onClick={manualMode?()=>setManualMode(false):initializeManual}>{manualMode?'Back to routes':'Manual assignment'}</button><button className="primary" onClick={planRoutes} disabled={!drivers.length||!points.length||manualMode}>Optimize routes</button>{manualMode&&<button className="primary" onClick={saveManual} disabled={!selectedDrivers.length||manualPool.length>0}>Save manual routes</button>}</div></div>
  <section className="panel routeControls">
   <div className="panelHead"><div><h3>Delivery window</h3><p>{slotName} deliveries for {new Date(date+'T00:00:00').toLocaleDateString()} · {slotWindow}</p></div><span className="count">{plan?.totalDeliveryPoints||0} stops · {plan?.totalDeliveries||0} delivery jobs</span></div>
   <div className="routeSlotPicker">{SLOTS.map(s=><button type="button" key={s[1]} className={Number(mealSlot)===s[1]?'routeSlotChoice active':'routeSlotChoice'} onClick={()=>setMealSlot(s[1])}><b>{s[0]}</b><small>{s[0]==='Morning'?'07:00–09:00':s[0]==='Afternoon'?'12:00–14:00':s[0]==='Evening'?'17:00–19:00':'20:00–22:00'}</small></button>)}</div>
   <div className="panelHead routeDriverHead"><div><h3>Drivers</h3><p>Select active drivers created under Team. You can let the planner distribute stops or assign them manually.</p></div><span className="count">{selectedDriverIds.length} selected · {drivers.length} active</span></div>
   <div className="driverPicker">{drivers.map(d=><label key={d.id} className={selectedDriverIds.includes(d.id)?'driverChoice checked':'driverChoice'}><input type="checkbox" checked={selectedDriverIds.includes(d.id)} onChange={e=>setSelectedDriverIds(e.target.checked?[...selectedDriverIds,d.id]:selectedDriverIds.filter(x=>x!==d.id))} disabled={manualMode}/><span><b>{d.name}</b><small>{d.email}</small></span></label>)}</div>
   {!drivers.length&&<div className="notice"><b>No drivers yet.</b><span>Add an in-house driver, then calculate the route.</span></div>}
  </section>
  {!plan?<Empty title="No route plan loaded" text="Select a date and delivery window to load delivery points."/>:<>
   {manualMode&&<section className="panel manualAssignmentPanel">
    <div className="panelHead"><div><span className="eyebrow">MANUAL DISPATCH PREPARATION</span><h3>Assign delivery stops to drivers</h3><p>Drag a stop into a driver. A stop represents one physical delivery address; multiple delivery jobs at the same address stay together.</p></div><span className={manualPool.length?'pill amber':'pill green'}>{manualPool.length?manualPool.length+' unassigned':'All stops assigned'}</span></div>
    <div className="manualAssignmentGrid">
      <section className="manualPool" onDragOver={e=>e.preventDefault()} onDrop={e=>{e.preventDefault();const id=e.dataTransfer.getData('text/plain');if(id)removeManualPoint(id)}}>
        <div className="manualColumnHead"><b>Unassigned stops</b><small>{manualPool.length} remaining</small></div>
        {manualPool.map(p=><article draggable key={p.addressId} onDragStart={e=>e.dataTransfer.setData('text/plain',p.addressId)} className="manualStopCard"><div className="manualStopNumber">•</div><div><b>{p.customerName}</b><span>{p.deliveryCount} job{p.deliveryCount===1?'':'s'} · {p.address}</span></div></article>)}
        {!manualPool.length&&<div className="manualEmpty">All delivery stops have a driver.</div>}
      </section>
      <div className="manualDriverColumns">{selectedDrivers.map(d=><section key={d.id} className="manualDriverColumn" onDragOver={e=>e.preventDefault()} onDrop={e=>dropManualPoint(e,d.id)}>
        <div className="manualColumnHead"><div><b>{d.name}</b><small>Drop stops here</small></div><span>{manualDriverPoints(d.id).length}</span></div>
        <div className="manualColumnBody">{manualDriverPoints(d.id).map(p=><article draggable key={p.addressId} onDragStart={e=>e.dataTransfer.setData('text/plain',p.addressId)} className="manualStopCard assigned"><div className="manualStopNumber">✓</div><div><b>{p.customerName}</b><span>{p.deliveryCount} job{p.deliveryCount===1?'':'s'} · {p.address}</span></div><button type="button" className="iconBtn small" onClick={()=>removeManualPoint(p.addressId)} aria-label="Remove stop">×</button></article>)}{!manualDriverPoints(d.id).length&&<div className="manualDropHint">Drop delivery stops here</div>}</div>
      </section>)}</div>
    </div>
    <div className="manualAssignmentFooter"><span>After saving, HealthApp will calculate the best road order <b>within each driver's assigned stops</b>.</span><button className="secondary" type="button" onClick={()=>setManualAssignments({})}>Clear assignments</button></div>
   </section>}
   <div className="statGrid routeStats"><div className="statCard static"><div><span>Delivery points</span><b>{plan.totalDeliveryPoints}</b></div></div><div className="statCard static"><div><span>Delivery jobs</span><b>{plan.totalDeliveries}</b></div></div><div className="statCard static"><div><span>Driver routes</span><b>{plan.routes?.length||0}</b></div></div><div className="statCard static"><div><span>Unassigned</span><b>{plan.unassignedPoints}</b></div></div></div>
   <section className="mapRouteGrid">
    <div className="panel mapPanel"><div className="panelHead"><div><h3>{plan.outletName} → delivery points</h3><p>{slotName} · {slotWindow} · blue marker is the outlet; every delivery point represents one address.</p></div><span className="routeLegend"><i/>Outlet <em/>Unassigned</span></div>
     <MapContainer center={[plan.outletLatitude,plan.outletLongitude]} zoom={12} scrollWheelZoom className="deliveryMap">
      <TileLayer attribution="&copy; OpenStreetMap contributors" url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"/>
      <MapBounds coords={bounds}/>
      <CircleMarker center={[plan.outletLatitude,plan.outletLongitude]} radius={12} pathOptions={{color:'#14532d',fillColor:'#14532d',fillOpacity:1,weight:3}}><Popup><b>{plan.outletName}</b><br/>Dispatch / outlet location<br/><small>{plan.totalDeliveryPoints} delivery points · {plan.totalDeliveries} delivery jobs</small></Popup></CircleMarker>
      {(plan.routes||[]).map((route,ri)=><React.Fragment key={route.id}><Polyline positions={(route.geometry||[]).map(p=>[p[1],p[0]])} pathOptions={{color:routeColor(ri),weight:6,opacity:.8}}/>{(route.stops||[]).map(stop=><CircleMarker key={stop.id} center={[stop.latitude,stop.longitude]} radius={9} pathOptions={{color:routeColor(ri),fillColor:routeColor(ri),fillOpacity:.9,weight:2}}><Popup><b>Stop {stop.stopSequence} · {stop.customerName}</b><br/>{stop.deliveryCount} delivery job{stop.deliveryCount===1?'':'s'} at this address.<br/>{stop.address}<br/><small>{route.driverName} · {route.mealSlot} · {route.deliveryWindow}</small></Popup></CircleMarker>)}</React.Fragment>)}
      {unassigned.map(p=><CircleMarker key={p.deliveryId} center={[p.latitude,p.longitude]} radius={8} pathOptions={{color:'#64748b',fillColor:'#64748b',fillOpacity:.85,weight:2}}><Popup><b>{p.customerName}</b><br/>{p.deliveryCount} delivery job{p.deliveryCount===1?'':'s'} at this address.<br/>{p.address}<br/><small>Unassigned · {p.mealSlot}</small></Popup></CircleMarker>)}
     </MapContainer>
     <div className="mapNote">Map tiles: OpenStreetMap · Road routing: {plan.routes?.[0]?.routingSource||'not calculated yet'} · Optimization: {plan.optimizationSource||'not calculated yet'} · planned travel {plan.plannedDistanceKm||0} km / ≈ {Math.round(plan.plannedDurationMinutes||0)} min.</div>
    </div>
    <div className="panel routeListPanel"><div className="panelHead"><div><h3>Driver routes</h3><p>Routes can be optimized automatically or manually assigned and then road-ordered per driver.</p></div></div>
     {(plan.routes||[]).map((route,ri)=><article className="routeCard" key={route.id}><div className="routeCardHead"><div><span className="routeColor" style={{background:routeColor(ri)}}/><div><b>{route.driverName}</b><small>Route {ri+1} · {route.mealSlot} · {route.deliveryWindow} · {route.stops.length} stops</small></div></div><span className="pill green">{route.status}</span></div><div className="routeMetrics"><b>{route.totalDistanceKm} km</b><span>≈ {Math.round(route.totalDurationMinutes)} min</span><span>{route.routingSource}</span></div><div className="stopList">{route.stops.map(stop=><div className="stopRow" key={stop.id}><strong>{stop.stopSequence}</strong><div><b>{stop.customerName}</b><span>{stop.deliveryCount} delivery job{stop.deliveryCount===1?'':'s'} · {stop.address}</span></div></div>)}</div><div className="routeCardActions">{route.status==='Planned'?<button type="button" className="primary smallBtn" onClick={()=>dispatchRoute(route)}>Dispatch route →</button>:route.status==='Dispatched'?<span className="routeDispatchedNote">✓ Dispatched to driver</span>:null}</div></article>)}
     {!(plan.routes||[]).length&&<Empty title="Routes not calculated yet" text={points.length?"Choose drivers and calculate the "+slotName.toLowerCase()+" routes.":"There are no eligible deliveries for this window."}/>}
    </div>
   </section>
  </>}
 </div>
}
function Page({title,text,content}){return <div className="page"><div className="pageHead"><div><h1>{title}</h1><p>{text}</p></div></div><section className="panel">{content}</section></div>}
function CustomersPage({customers,onCreate,onOpen}){return <div className="page"><div className="pageHead"><div><span className="eyebrow">CUSTOMER RELATIONSHIPS</span><h1>Customers</h1><p>Manage customers who contact the outlet directly, including their health profile, allergies and delivery addresses.</p></div><button className="primary" onClick={onCreate}>+ Add customer</button></div><section className="panel customerManagerPanel"><div className="customerManagerHead"><div><b>{customers.length}</b><span>connected customer{customers.length===1?'':'s'}</span></div><small>Click a customer to view their full profile.</small></div>{customers.length?<div className="customerManagerList">{customers.map(x=><button type="button" className="customerManagerRow" key={x.id} onClick={()=>onOpen(x)}><div className="customerAvatar">{(x.firstName||'C')[0]}{(x.lastName||'')[0]||''}</div><div className="customerManagerInfo"><b>{x.firstName} {x.lastName}</b><span>{x.email}</span></div><span className="customerManagerRole">{x.role}</span><strong>›</strong></button>)}</div>:<Empty title="No customers yet" text="Add a customer here when an outlet staff member takes a phone order or package request."/>}</section></div>}

function CustomerEditorModal({form,setForm,allergens,saving,editing,onClose,onSave}){const set=(k,v)=>setForm(f=>({...f,[k]:v}));const toggle=id=>set('allergyIds',(form.allergyIds||[]).includes(id)?(form.allergyIds||[]).filter(x=>x!==id):[...(form.allergyIds||[]),id]);return <Modal title={editing?'Edit customer health profile':'Add customer'} onClose={onClose} wide><form onSubmit={onSave}><div className="customerEditorIntro"><div><span className="eyebrow">{editing?'PROFILE UPDATE':'PHONE / WALK-IN CUSTOMER'}</span><h3>{editing?'Update health profile':'Create the customer record now'}</h3><p>Capture the customer details while they are on the phone, then create their package from the outlet.</p></div></div><div className="customerEditorSection"><h4>Customer details</h4><div className="formGrid"><Field label="First name"><input value={form.firstName} onChange={e=>set('firstName',e.target.value)} required={!editing}/></Field><Field label="Last name"><input value={form.lastName} onChange={e=>set('lastName',e.target.value)} required={!editing}/></Field><Field label="Email"><input type="email" value={form.email} onChange={e=>set('email',e.target.value)} required={!editing}/></Field>{!editing&&<Field label="Temporary password" help="Give this to the customer if they need to sign in later."><input type="password" minLength="6" value={form.password} onChange={e=>set('password',e.target.value)} required/></Field>}</div></div><div className="customerEditorSection"><h4>Health profile</h4><div className="formGrid"><Field label="Weight (kg)"><input type="number" min="1" step=".1" value={form.weightKg} onChange={e=>set('weightKg',e.target.value)}/></Field><Field label="Height (cm)"><input type="number" min="1" step=".1" value={form.heightCm} onChange={e=>set('heightCm',e.target.value)}/></Field><Field label="Date of birth"><input type="date" value={form.dateOfBirth} onChange={e=>set('dateOfBirth',e.target.value)}/></Field><Field label="Goal"><select value={form.goal} onChange={e=>set('goal',e.target.value)}><option value="WeightLoss">Weight Loss</option><option value="MuscleGain">Muscle Gain</option><option value="GLP1Support">GLP-1 Support</option><option value="HighPerformance">High Performance</option></select></Field><Field label="Activity level"><select value={form.activityLevel} onChange={e=>set('activityLevel',e.target.value)}><option value="Sedentary">Sedentary</option><option value="Light">Light</option><option value="Moderate">Moderate</option><option value="Active">Active</option><option value="VeryActive">Very Active</option></select></Field><Field label="Diet"><select value={form.diet} onChange={e=>set('diet',e.target.value)}><option value="">Not specified</option><option value="Veg">Vegetarian</option><option value="NonVeg">Non-vegetarian</option><option value="Vegan">Vegan</option><option value="Eggetarian">Eggetarian</option><option value="Pescatarian">Pescatarian</option></select></Field></div><div className="customerAllergyEditor"><span>Allergies</span><p>Select from the HealthApp allergen master list. These choices drive meal safety warnings.</p><div className="customerAllergyOptions">{allergens.map(a=><label key={a.id} className={(form.allergyIds||[]).includes(a.id)?'checked':''}><input type="checkbox" checked={(form.allergyIds||[]).includes(a.id)} onChange={()=>toggle(a.id)}/><span>{a.name}</span></label>)}</div></div></div><div className="modalActions"><button type="button" className="secondary" onClick={onClose}>Cancel</button><button className="primary" disabled={saving}>{saving?(editing?'Saving…':'Creating…'):(editing?'Save profile':'Create customer')}</button></div></form></Modal>}

function CustomerProfileModal({profile,loading,onClose,onEdit,onCreatePackage}){return <Modal title="Customer profile" onClose={onClose} wide>{loading||!profile?<div className="customerProfileLoading">Loading customer profile…</div>:<div className="customerProfileView"><div className="customerProfileHero"><div className="customerProfileAvatar">{(profile.customer.firstName||'C')[0]}{(profile.customer.lastName||'')[0]||''}</div><div><span className="eyebrow">CUSTOMER</span><h3>{profile.customer.firstName} {profile.customer.lastName}</h3><p>{profile.customer.email}</p></div><span className="pill green">{profile.customer.role}</span></div><div className="customerProfileGrid"><section><h4>Health & preferences</h4><div className="customerProfileStats"><div><span>Weight</span><b>{profile.profile?.weightKg?profile.profile.weightKg+' kg':'Not set'}</b></div><div><span>Height</span><b>{profile.profile?.heightCm?profile.profile.heightCm+' cm':'Not set'}</b></div><div><span>BMI</span><b>{profile.profile?.bmi??'—'}</b></div><div><span>Date of birth</span><b>{profile.profile?.dateOfBirth?new Date(profile.profile.dateOfBirth).toLocaleDateString('en-IN'):'Not set'}</b></div></div><div className="customerProfileFacts"><div><span>Goal</span><b>{profile.profile?.goal||'Not set'}</b></div><div><span>Activity</span><b>{profile.profile?.activityLevel||'Not set'}</b></div><div><span>Diet</span><b>{profile.profile?.diet||'Not specified'}</b></div></div><div className="customerProfileAllergies"><span>Allergies</span><div>{profile.profile?.allergies?.length?profile.profile.allergies.map(a=><span key={a.id}>{a.name}</span>):<small>No allergies recorded</small>}</div></div></section><section><div className="customerProfileSectionHead"><h4>Delivery addresses</h4><span>{profile.addresses?.length||0}</span></div>{profile.addresses?.length?<div className="customerProfileAddresses">{profile.addresses.map(a=><div className={a.isDefault?'default':''} key={a.id}><b>{a.label}{a.isDefault?' · Default':''}</b><span>{[a.addressLine1,a.addressLine2,a.areaName,a.city,a.pincode].filter(Boolean).join(', ')}</span><small>{a.contactName} · {a.contactPhone}</small></div>)}</div>:<div className="customerProfileEmpty">No delivery address yet. Add one from Create Package.</div>}</section></div><div className="modalActions"><button className="secondary" onClick={onEdit}>Edit health profile</button><button className="primary" onClick={onCreatePackage}>Create package →</button></div></div>}</Modal>}

function Recipes({items,total,search,setSearch,category,setCategory,openNew,openEdit,remove}){
 const[mealType,setMealType]=useState('All');
 const visible=useMemo(()=>items.filter(r=>mealType==='All'||String(r.mealType||'Meal')===mealType),[items,mealType]);
 return <div className="page recipePage">
  <div className="pageHead">
   <div><span className="eyebrow">MENU LIBRARY</span><h1>Recipes & menu items</h1><p>Create reusable food items with a dietary category, meal type, ingredient quantities, nutrition, pricing and allergen metadata.</p></div>
   <button className="primary" onClick={openNew}>+ New menu item</button>
  </div>
  <section className="panel recipeFilterPanel">
   <div className="recipeFilterIntro"><div><b>Menu library</b><span>{visible.length} of {total} recipes shown</span></div><span className="recipeFilterHint">Meal type is separate from dietary category.</span></div>
   <div className="recipeFilters">
    <label><span>Search</span><input className="search" value={search} onChange={e=>setSearch(e.target.value)} placeholder="Search meals, juices, snacks…"/></label>
    <label><span>Dietary category</span><select value={category} onChange={e=>setCategory(e.target.value)}><option>All</option><option>Veg</option><option>NonVeg</option><option>Vegan</option></select></label>
    <label><span>Meal type</span><select value={mealType} onChange={e=>setMealType(e.target.value)}><option>All</option>{MEAL_TYPES.map(([value,label])=><option value={value} key={value}>{label}</option>)}</select></label>
   </div>
  </section>
  {!visible.length?<Empty title="No matching menu items" text="Try a different filter or create a new menu item."/>:<div className="recipeGrid">{visible.map(r=><article className="recipeCard" key={r.id}>
   <div className="recipeImage">{r.imageUrl?<img src={img(r.imageUrl)} alt=""/>:<div className="placeholderImage">{r.name?.[0]||'M'}</div>}<span className="badge green">Active</span></div>
   <div className="recipeBody">
    <div className="recipeTitle"><div><h3>{r.name}</h3><div className="recipeTypeRow"><span className="recipeMealTypeBadge">{r.mealType||'Meal'}</span><span className="recipeDietBadge">{r.category||'Veg'}</span></div></div><b>{money(r.pricePerMeal)}</b></div>
    <p>{r.description||'No description yet.'}</p>
    <div className="nutrition"><span><b>{r.calories||0}</b>kcal</span><span><b>{r.proteinGrams||0}g</b>protein</span><span><b>{r.carbsGrams||0}g</b>carbs</span><span><b>{r.fatGrams||0}g</b>fat</span></div>
    <div className="recipeMeta"><span>{r.ingredients?.length||0} ingredients</span><span>{r.allergens?.length||0} allergy flags</span></div>
    <div className="cardActions"><button className="secondary" onClick={()=>openEdit(r)}>Edit</button><button className="dangerText" onClick={()=>remove(r)}>Delete</button></div>
   </div>
  </article>)}</div>}
 </div>
}

function IngredientPicker({value,ingredients,onChange}){
 const[selected,setSelected]=useState(ingredients.find(x=>x.id===value)?.name||'');
 const[open,setOpen]=useState(false);
 const matches=useMemo(()=>{const q=selected.trim().toLowerCase();return(ingredients||[]).filter(i=>!q||String(i.name).toLowerCase().includes(q)).slice(0,12)},[ingredients,selected]);
 useEffect(()=>{setSelected(ingredients.find(x=>x.id===value)?.name||'')},[value,ingredients]);
 return <div className="ingredientPicker" onBlur={e=>{if(!e.currentTarget.contains(e.relatedTarget))setOpen(false)}}>
  <input value={selected} onFocus={()=>setOpen(true)} onChange={e=>{setSelected(e.target.value);setOpen(true)}} placeholder="Type to search ingredient…" autoComplete="off"/>
  {open&&<div className="ingredientPickerMenu">{matches.length?matches.map(item=><button type="button" key={item.id} onMouseDown={e=>e.preventDefault()} onClick={()=>{setSelected(item.name);setOpen(false);onChange(item)}}><span><b>{item.name}</b><small>{item.defaultUnit||'g'} · {item.nutritionSource?'Nutrition ready':'Nutrition missing'}</small></span>{item.nutritionSource&&<i>✓</i>}</button>):<div className="ingredientPickerEmpty">No matching ingredients. Add the ingredient to your master list first.</div>}</div>}
 </div>;
}

function NutritionSummary({title,data,active,onSelect}){
 return <button type="button" className={active?'portionNutritionCard active':'portionNutritionCard'} onClick={()=>onSelect?.()}>
  <div className="portionNutritionCardHead"><div><span>{title}</span><b>{Math.round(data.calories)} kcal</b></div>{active&&<em>Selected</em>}</div>
  <div className="portionMacroGrid"><span><b>{Number(data.proteinGrams).toFixed(1)}g</b><small>Protein</small></span><span><b>{Number(data.carbsGrams).toFixed(1)}g</b><small>Carbs</small></span><span><b>{Number(data.fatGrams).toFixed(1)}g</b><small>Fat</small></span><span><b>{Number(data.fiberGrams).toFixed(1)}g</b><small>Fiber</small></span><span><b>{Number(data.sugarGrams).toFixed(1)}g</b><small>Sugar</small></span></div>
 </button>;
}

function RecipeForm({form,setForm,ingredients,allergens,upload,uploading,submit,cancel}){
 const set=(k,v)=>setForm(f=>({...f,[k]:v}));
 const[portion,setPortion]=useState('regular');
 const addIngredient=()=>{const first=ingredients[0];set('ingredients',[...(form.ingredients||[]),{ingredientId:first?.id||'',quantity:100,largeQuantity:125,unit:first?.defaultUnit||'g'}])};
 const updateIngredient=(idx,k,v)=>set('ingredients',(form.ingredients||[]).map((x,i)=>i===idx?{...x,[k]:v}:x));
 const removeIngredient=idx=>set('ingredients',(form.ingredients||[]).filter((_,i)=>i!==idx));
 const chooseIngredient=(idx,item)=>{updateIngredient(idx,'ingredientId',item.id);updateIngredient(idx,'unit',item.defaultUnit||'g')};
 const toggleAllergen=id=>set('allergenIds',(form.allergenIds||[]).includes(id)?(form.allergenIds||[]).filter(x=>x!==id):[...(form.allergenIds||[]),id]);
 const nutrition=calculateRecipeNutrition(form,ingredients);
 const hasIngredients=(form.ingredients||[]).some(x=>x.ingredientId&&Number(x.quantity)>0);
 return <form onSubmit={submit}>
  <div className="recipeEditor">
   <div className="imageUpload"><div className="uploadPreview">{form.imageUrl?<img src={img(form.imageUrl)} alt="preview"/>:<div><span>＋</span><p>Upload menu item photo</p><small>JPG, PNG, WEBP · 10 MB · resized automatically</small></div>}</div><label className="uploadBtn">{uploading?'Uploading…':form.imageUrl?'Replace image':'Upload image'}<input type="file" accept="image/jpeg,image/png,image/webp" onChange={e=>upload(e.target.files?.[0])}/></label></div>
   <div className="formGrid">
    <Field label="Menu item name"><input value={form.name} onChange={e=>set('name',e.target.value)} placeholder="Paneer Power Bowl, Fresh Orange Juice…" required/></Field>
    <Field label="Meal type" help="Meal, Juice, Snack, Starter, etc."><select value={form.mealType||'Meal'} onChange={e=>set('mealType',e.target.value)}>{MEAL_TYPES.map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></Field>
    <Field label="Dietary category"><select value={form.category} onChange={e=>set('category',e.target.value)}><option>Veg</option><option>NonVeg</option><option>Vegan</option></select></Field>
    <Field label="Regular price (₹)"><input type="number" min="0" step=".01" value={form.pricePerMeal} onChange={e=>set('pricePerMeal',e.target.value)}/></Field>
    <Field label="Large price (₹)"><input type="number" min="0" step=".01" value={form.largePricePerMeal} onChange={e=>set('largePricePerMeal',e.target.value)}/></Field>
    <div className="span2 recipeNutritionCalculated">
      <div className="recipeNutritionHead"><div><span className="eyebrow">PORTION NUTRITION</span><b>Automatically calculated from ingredient quantities</b><small>Regular and Large are calculated independently. Adjust the Large quantities below when the larger portion uses more ingredients.</small></div></div>
      <div className="portionNutritionTabs"><button type="button" className={portion==='regular'?'selected':''} onClick={()=>setPortion('regular')}>Regular</button><button type="button" className={portion==='large'?'selected':''} onClick={()=>setPortion('large')}>Large</button></div>
      <div className="portionNutritionCards"><NutritionSummary title="Regular portion" data={nutrition.regular} active={portion==='regular'} onSelect={()=>setPortion('regular')}/><NutritionSummary title="Large portion" data={nutrition.large} active={portion==='large'} onSelect={()=>setPortion('large')}/></div>
      {!hasIngredients&&<div className="recipeNutritionNotice">Add ingredients to calculate both portion nutrition profiles.</div>}
      {!!nutrition.missing.length&&<div className="recipeNutritionWarning">Nutrition is unavailable for: {nutrition.missing.join(', ')}. Use gram/kg quantities and configure the ingredient nutrition reference.</div>}
    </div>
    <div className="span2 ingredientEditor">
      <div className="editorTitle"><div><b>Ingredients & portion quantities</b><small>Search by typing. Regular and Large quantities are stored separately; nutrition follows each quantity.</small></div><button type="button" className="secondary small" onClick={addIngredient}>+ Add ingredient</button></div>
      <div className="portionQuantityBar"><button type="button" className={portion==='regular'?'active':''} onClick={()=>setPortion('regular')}><b>Regular</b><span>Standard serving</span></button><button type="button" className={portion==='large'?'active':''} onClick={()=>setPortion('large')}><b>Large</b><span>Large serving</span></button></div>
      {(form.ingredients||[]).map((x,idx)=>{
       const item=ingredients.find(i=>i.id===x.ingredientId);
       const qtyKey=portion==='large'?'largeQuantity':'quantity';
       return <div className="ingredientRow" key={idx}>
        <div className="ingredientSelectWrap"><IngredientPicker value={x.ingredientId} ingredients={ingredients} onChange={item=>chooseIngredient(idx,item)}/>{item&&<small className="ingredientNutritionHint">{item.nutritionSource?i18nNutrition(item):'Nutrition reference not configured'}</small>}</div>
        <label className="ingredientQtyField"><span>{portion==='large'?'Large':'Regular'} quantity</span><input type="number" min="0.001" step="0.001" value={x[qtyKey]??''} onChange={e=>updateIngredient(idx,qtyKey,e.target.value)} placeholder="100"/></label>
        <label className="ingredientUnitField"><span>Unit</span><input value={x.unit||''} onChange={e=>updateIngredient(idx,'unit',e.target.value)} placeholder="g"/></label>
        <button type="button" className="dangerText ingredientRemove" onClick={()=>removeIngredient(idx)}>Remove</button>
        <div className="ingredientOtherQty"><span>{portion==='large'?'Regular':'Large'}: <b>{Number(x[portion==='large'?'quantity':'largeQuantity']||0)} {x.unit||''}</b></span></div>
       </div>
      })}
      {!(form.ingredients||[]).length&&<div className="editorEmpty">No ingredients yet. Add an ingredient and start typing its name.</div>}
    </div>
    <div className="span2 allergyEditor"><div className="editorTitle"><div><b>Recipe-level allergens</b><small>Ingredient-linked allergens are calculated automatically.</small></div></div><div className="allergenChoices">{allergens.map(a=><label key={a.id} className={(form.allergenIds||[]).includes(a.id)?'allergenChoice checked':'allergenChoice'}><input type="checkbox" checked={(form.allergenIds||[]).includes(a.id)} onChange={()=>toggleAllergen(a.id)}/><span>{a.name}</span></label>)}</div>{!allergens.length&&<div className="editorEmpty">No allergen master values available.</div>}</div>
    <Field label="Tags"><input value={form.tags||''} onChange={e=>set('tags',e.target.value)} placeholder="High protein, low carb…"/></Field><Field label="Image URL"><input value={form.imageUrl||''} onChange={e=>set('imageUrl',e.target.value)} placeholder="https://…"/></Field>
   </div>
  </div>
  <div className="modalActions"><button type="button" className="secondary" onClick={cancel}>Cancel</button><button className="primary" disabled={uploading||!hasIngredients||!!nutrition.missing.length}>{form.id?'Save changes':'Create menu item'}</button></div>
 </form>
}
function i18nNutrition(item){
 return `${Number(item.caloriesPer100g||0)} kcal · ${Number(item.proteinGramsPer100g||0).toFixed(1)}g protein / 100g`;
}

function MenuPage({recipes,menu,setMenu,onSave}){
 const[day,setDay]=useState(1),[slot,setSlot]=useState(1),[rid,setRid]=useState(''),[optionGroup,setOptionGroup]=useState('Meal'),[optionGroupTouched,setOptionGroupTouched]=useState(false),[isRequired,setIsRequired]=useState(true),[requiredTouched,setRequiredTouched]=useState(false),[maxSelections,setMaxSelections]=useState(1),[mealTypeFilter,setMealTypeFilter]=useState('All');
 const filteredRecipes=useMemo(()=>recipes.filter(r=>mealTypeFilter==='All'||String(r.mealType||'Meal')===mealTypeFilter),[recipes,mealTypeFilter]);
 useEffect(()=>{if(!rid&&filteredRecipes[0])setRid(filteredRecipes[0].id);else if(rid&&!filteredRecipes.some(r=>r.id===rid))setRid(filteredRecipes[0]?.id||'')},[filteredRecipes,rid]);
 const selectedRecipe=recipes.find(x=>x.id===rid);
 useEffect(()=>{if(selectedRecipe){if(!optionGroupTouched)setOptionGroup(String(selectedRecipe.mealType||'Meal').trim()||'Meal');if(!requiredTouched)setIsRequired(String(selectedRecipe.mealType||'Meal').trim().toLowerCase()==='meal')}},[selectedRecipe,optionGroupTouched,requiredTouched]);
 const add=()=>{const r=selectedRecipe;if(!r)return;if(menu.some(x=>Number(x.dayOfWeek)===day&&Number(x.mealSlot)===slot&&x.recipeId===rid))return;setMenu([...menu,{id:'tmp-'+Date.now(),recipeId:rid,recipeName:r.name,mealType:r.mealType||'Meal',dayOfWeek:day,mealSlot:slot,isAvailable:true,displayOrder:0,optionGroup:optionGroup.trim()||'Main',isRequired,maxSelections:Math.max(1,Number(maxSelections)||1)}])};
 const updateItem=(id,patch)=>setMenu(menu.map(x=>x.id===id?{...x,...patch}:x));
 return <div className="page menuPage">
  <div className="pageHead menuPageHead"><div><span className="eyebrow">CUSTOMER MENU</span><h1>Weekly Menu</h1><p>Build a clear weekly schedule from your menu library. Assign meal types, option groups, required choices and maximum selections without squeezing everything into one row.</p></div><button className="primary" onClick={onSave}>Save weekly menu</button></div>
  <section className="panel menuOptionConfig menuComposer">
   <div className="menuComposerHead"><div><span className="menuSectionKicker">ADD TO WEEKLY MENU</span><h3>Configure what customers can choose in each slot</h3><p>Use groups such as Meal, Juice, Snack and Curd. Customers can select one item per group by default, or you can allow more with Max choices.</p></div><div className="menuLegend"><span><i className="menuLegendDot required"></i>Required</span><span><i className="menuLegendDot optional"></i>Optional</span></div></div>
   <div className="menuComposerGrid">
    <label className="menuField"><span>Day</span><select value={day} onChange={e=>setDay(Number(e.target.value))}>{DAYS.map((d,i)=><option value={i} key={d}>{d}</option>)}</select></label>
    <label className="menuField"><span>Meal slot</span><select value={slot} onChange={e=>setSlot(Number(e.target.value))}>{SLOTS.map(s=><option value={s[1]} key={s[1]}>{s[0]}</option>)}</select></label>
    <label className="menuField"><span>Meal type</span><select value={mealTypeFilter} onChange={e=>setMealTypeFilter(e.target.value)}><option>All</option>{MEAL_TYPES.map(([value,label])=><option value={value} key={value}>{label}</option>)}</select></label>
    <label className="menuField menuFieldWide"><span>Recipe / menu item</span><select value={rid} onChange={e=>{setRid(e.target.value);const next=recipes.find(r=>r.id===e.target.value);if(next){if(!optionGroupTouched)setOptionGroup(String(next.mealType||'Meal').trim()||'Meal');if(!requiredTouched)setIsRequired(String(next.mealType||'Meal').trim().toLowerCase()==='meal')}}}><option value="">Choose menu item…</option>{filteredRecipes.map(r=><option value={r.id} key={r.id}>{r.name} · {r.mealType||'Meal'} · {r.category||'Veg'}</option>)}</select></label>
    <div className="menuSelectedType">{selectedRecipe?<><span>SELECTED TYPE</span><b>{selectedRecipe.mealType||'Meal'}</b><small>{selectedRecipe.category||'Veg'} · ₹{Number(selectedRecipe.pricePerMeal||0).toFixed(2)}</small></>:<span>Select a menu item</span>}</div>
    <label className="menuField"><span>Option group</span><input value={optionGroup} onChange={e=>{setOptionGroupTouched(true);setOptionGroup(e.target.value)}} placeholder="Meal / Juice / Snack / Curd" maxLength={50}/></label>
    <label className="menuField"><span>Max choices</span><input type="number" min="1" max="20" value={maxSelections} onChange={e=>setMaxSelections(e.target.value)}/><small className="menuFieldHelp">Set 1 for one-per-group. Increase it when customers can choose multiple items from the same group.</small></label>
    <label className="menuRequiredCard"><input type="checkbox" checked={isRequired} onChange={e=>{setRequiredTouched(true);setIsRequired(e.target.checked)}}/><span><b>{isRequired?'Required option':'Optional option'}</b><small>{isRequired?'Customer must choose from this group.':'Customer may skip this group.'}</small></span></label>
    <button className="primary menuAddButton" onClick={add} disabled={!selectedRecipe}>+ Add item to {DAYS[day]}</button>
   </div>
  </section>
  <section className="menuWeekPanel panel">
   <div className="menuWeekPanelHead"><div><span className="menuSectionKicker">WEEKLY SCHEDULE</span><h3>Monday to Sunday menu</h3></div><span className="menuWeekHint">Select a day card to focus it. The schedule scrolls on smaller screens.</span></div>
   <div className="menuWeekScroller"><div className="menuWeek">{DAYS.map((d,i)=>{const dayItems=menu.filter(m=>Number(m.dayOfWeek)===i);return <section className={i===day?'menuDayCard selected':'menuDayCard'} key={d} onClick={()=>setDay(i)}>
      <div className="menuDayHead"><div><span className="menuDayIndex">{String(i+1).padStart(2,'0')}</span><div><b>{d}</b><small>{dayItems.length?dayItems.length+' scheduled items':'No items yet'}</small></div></div><span className="menuDayCount">{dayItems.filter(m=>m.isAvailable).length}</span></div>
      <div className="menuDayList">{dayItems.length?dayItems.map(m=><article className="menuScheduleItem" key={m.id}>
        <div className="menuScheduleTop"><div><span className="menuSlotPill">{((SLOTS.find(s=>s[1]===Number(m.mealSlot))||[])[0])||m.mealSlot}</span><span className="menuTypePill">{m.mealType||'Meal'}</span></div><button className="iconBtn small" type="button" aria-label="Remove menu item" onClick={e=>{e.stopPropagation();setMenu(menu.filter(x=>x.id!==m.id))}}>×</button></div>
        <b className="menuScheduleName">{m.recipeName||((recipes.find(r=>r.id===m.recipeId)||{}).name)||'Menu item'}</b>
        <div className="menuScheduleMeta"><span>{m.optionGroup||'Main'}</span><span>{m.isRequired!==false?'Required':'Optional'}</span><span>Max {m.maxSelections||1}</span></div>
        <div className="menuInlineSettings"><label><span>Group</span><input value={m.optionGroup||'Main'} maxLength={50} aria-label="Option group" onClick={e=>e.stopPropagation()} onChange={e=>updateItem(m.id,{optionGroup:e.target.value||'Main'})}/></label><label><span>Max</span><input type="number" min="1" max="20" value={m.maxSelections||1} aria-label="Maximum selections" onClick={e=>e.stopPropagation()} onChange={e=>updateItem(m.id,{maxSelections:Number(e.target.value)||1})}/></label><label className="inlineCheck"><input type="checkbox" checked={m.isRequired!==false} onClick={e=>e.stopPropagation()} onChange={e=>updateItem(m.id,{isRequired:e.target.checked})}/><span>Required</span></label><label className="inlineCheck"><input type="checkbox" checked={m.isAvailable!==false} onClick={e=>e.stopPropagation()} onChange={e=>updateItem(m.id,{isAvailable:e.target.checked})}/><span>Published</span></label></div>
      </article>):<div className="menuDayEmpty"><span>＋</span><b>No menu items</b><small>Select {d} above and add an item to start.</small></div>}</div>
    </section>})}</div></div>
  </section>
 </div>
}

function Pricing({pricing,add,remove}){return <div className="page"><div className="pageHead"><div><h1>Delivery Pricing</h1><p>Configure distance-based delivery fees for customers.</p></div><button className="primary" onClick={add}>+ Add distance slab</button></div><div className="pricingExplain"><span>Customer address</span><b>→</b><span>Distance calculated</span><b>→</b><span>Matching slab fee</span></div><section className="panel">{pricing.length?<div className="pricingTable"><div className="ptHead"><span>Up to distance</span><span>Fee</span><span>Action</span></div>{pricing.map(p=><div className="ptRow" key={p.id}><b>≤ {p.maxDistanceKm} km</b><strong>{money(p.fee)}</strong><button className="dangerText" onClick={()=>remove(p)}>Delete</button></div>)}</div>:<Empty title="No pricing slabs" text="Start with 2 km = ₹10, then add larger distance slabs."/>}</section></div>}
function Discounts({tiers,add,edit,remove}){return <div className="page"><div className="pageHead"><div><h1>Subscription Discounts</h1><p>Discounts based on committed meal quantity and package duration.</p></div><button className="primary" onClick={add}>+ Add tier</button></div><section className="panel"><div className="discountTable"><div className="dtRow header"><span>Meals</span><span>1W</span><span>2W</span><span>1M</span><span>Actions</span></div>{tiers.map(t=><div className="dtRow" key={t.id}><b>{t.minMeals} – {t.maxMeals??'∞'}</b><span>{t.oneWeekPercent}%</span><span>{t.twoWeeksPercent}%</span><span>{t.oneMonthPercent}%</span><div><button className="secondary smallBtn" onClick={()=>edit(t)}>Edit</button><button className="dangerText" onClick={()=>remove(t)}>Delete</button></div></div>)}</div>{!tiers.length&&<Empty title="No discount tiers" text="Create your first quantity-based discount."/>}</section></div>}
function TaxSettingsPage({settings,form,setForm,save}){const rate=Number(form.restaurantGstRate)||0;const sample=236;const applicable=form.taxOperatingMode==='EcoSection9_5'||(form.isGstRegistered&&!form.isComposition);const taxable=applicable&&form.restaurantGstMode==='Inclusive'?Math.round(sample*100/(100+rate)*100)/100:sample;const gst=applicable?(form.restaurantGstMode==='Inclusive'?Math.round((sample-taxable)*100)/100:Math.round(sample*rate/100*100)/100):0;return <div className="page"><div className="pageHead"><div><span className="eyebrow">TAX CONFIGURATION</span><h1>Tax & GST</h1><p>Tax applicability is resolved first. Inclusive/exclusive only controls the calculation when GST is applicable.</p></div></div><div className="taxSettingsGrid"><section className="panel"><div className="panelHead"><div><h3>Restaurant GST</h3><p>Outlet tax profile is effective-dated and versioned.</p></div>{settings&&<span className="pill green">Saved · {settings.taxOperatingMode||'DirectOutletSupplier'}</span>}</div><form onSubmit={save} className="formGrid"><Field label="GST registration"><select value={form.isGstRegistered?'Registered':'NotRegistered'} onChange={e=>setForm({...form,isGstRegistered:e.target.value==='Registered'})}><option value="NotRegistered">Not GST registered</option><option value="Registered">GST registered</option></select></Field><Field label="Composition scheme"><select value={form.isComposition?'Yes':'No'} onChange={e=>setForm({...form,isComposition:e.target.value==='Yes'})} disabled={!form.isGstRegistered}><option value="No">No</option><option value="Yes">Yes</option></select></Field><Field label="Tax operating mode"><select value={form.taxOperatingMode} onChange={e=>setForm({...form,taxOperatingMode:e.target.value})}><option value="DirectOutletSupplier">Direct outlet supplier</option><option value="EcoSection9_5">ECO · Section 9(5)</option></select></Field><Field label="Restaurant GST rate"><input type="number" min="0" max="100" step=".01" value={form.restaurantGstRate} onChange={e=>setForm({...form,restaurantGstRate:e.target.value})}/></Field><Field label="GSTIN"><input value={form.gstin} onChange={e=>setForm({...form,gstin:e.target.value.toUpperCase()})} placeholder="Required when registered"/></Field><Field label="PAN"><input value={form.pan} onChange={e=>setForm({...form,pan:e.target.value.toUpperCase()})}/></Field><Field label="Effective from"><input type="date" value={form.effectiveFromUtc} onChange={e=>setForm({...form,effectiveFromUtc:e.target.value})}/></Field><div className="span2"><span className="field"><span>Price treatment</span><div className="taxModeCards"><button type="button" className={form.restaurantGstMode==='Exclusive'?'taxModeCard selected':'taxModeCard'} onClick={()=>setForm({...form,restaurantGstMode:'Exclusive'})}><b>GST Exclusive</b><span>Only adds GST when the resolved rule is applicable.</span></button><button type="button" className={form.restaurantGstMode==='Inclusive'?'taxModeCard selected':'taxModeCard'} onClick={()=>setForm({...form,restaurantGstMode:'Inclusive'})}><b>GST Inclusive</b><span>Extracts GST only when the resolved rule is applicable.</span></button></div></span></div>{!applicable&&<div className="financeTaxWarning"><b>No restaurant GST will be charged in this mode</b><span>For a direct outlet that is not GST registered (or is composition), customer meal/package prices do not gain restaurant GST merely because Inclusive is selected.</span></div>}</form></section><aside className="panel taxPreview"><span className="eyebrow">LIVE EXAMPLE</span><h3>{applicable?(form.restaurantGstMode==='Inclusive'?'Inclusive price breakdown':'Exclusive price breakdown'):'No restaurant GST'}</h3><p>Example meal price: <b>₹{sample.toFixed(2)}</b>{applicable?' at '+rate+'% GST.':'; customer price stays as configured.'}</p><div className="taxPreviewRows"><div><span>{form.restaurantGstMode==='Inclusive'&&applicable?'Displayed / gross price':'Meal price'}</span><b>{money(sample)}</b></div><div><span>Taxable value</span><b>{money(taxable)}</b></div><div><span>Restaurant GST</span><b>{money(gst)}</b></div><div className="total"><span>Customer meal amount</span><strong>{money(applicable&&form.restaurantGstMode!=='Inclusive'?sample+gst:sample)}</strong></div></div><small>{!applicable?'Tax applicability is false; Inclusive/Exclusive does not add restaurant GST.':form.restaurantGstMode==='Inclusive'?'The customer sees the configured price; reports split it into taxable value and GST.':'GST is added on top of the configured meal price at checkout.'}</small></aside></div></div>}
function Billing({billing}){return <div className="page"><div className="pageHead"><div><h1>Billing</h1><p>Current outlet SaaS subscription and commission details.</p></div></div>{billing?<div className="billingGrid"><section className="panel"><span className="eyebrow">CURRENT PLAN</span><h2>{billing.planName}</h2><div className="bigMoney">{money(billing.subscriptionFee)}<small> / {billing.billingCycle}</small></div><div className="kv"><span>Commission</span><b>{billing.transactionFeePercent}%</b></div><div className="kv"><span>Included customers</span><b>{billing.includedActiveCustomers}</b></div><div className="kv"><span>Active customers</span><b>{billing.activeCustomers}</b></div></section><section className="panel"><h3>Plan details</h3><div className="kv"><span>Setup fee</span><b>{money(billing.setupFee)}</b></div><div className="kv"><span>Renewal</span><b>{new Date(billing.renewalDate).toLocaleDateString()}</b></div><div className="kv"><span>Status</span><span className="pill green">{billing.status}</span></div></section></div>:<Empty title="Billing unavailable" text="The outlet has no active SaaS subscription."/>}</div>}
createRoot(document.getElementById('root')).render(<App/>);
